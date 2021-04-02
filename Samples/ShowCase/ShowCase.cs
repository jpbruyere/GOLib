﻿// Copyright (c) 2013-2019  Bruyère Jean-Philippe <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using Crow;
using System.IO;
using System.Text;
using Crow.IML;
using System.Runtime.CompilerServices;
using Glfw;
using System.Diagnostics;
using Crow.Text;
using System.Collections.Generic;
using Encoding = System.Text.Encoding;

namespace ShowCase
{
	class Showcase : SampleBase
	{
		static void Main ()
		{
			DbgLogger.IncludeEvents = DbgEvtType.None;
			DbgLogger.DiscardEvents = DbgEvtType.All;
			DbgLogger.ConsoleOutput = !Configuration.Global.Get<bool> (nameof (DebugLogToFile));

			Environment.SetEnvironmentVariable ("FONTCONFIG_PATH", @"C:\Users\Jean-Philippe\source\vcpkg\installed\x64-windows\tools\fontconfig\fonts");

			using (Showcase app = new Showcase ()) {
				//app.Theme = @"C:\Users\Jean-Philippe\source\Crow\Themes\TestTheme";
				app.Run ();
			}
		}


		public Container crowContainer;

		public string CurrentDir {
			get => Configuration.Global.Get<string> (nameof (CurrentDir));
			set {
				if (CurrentDir == value)
					return;
				Configuration.Global.Set (nameof (CurrentDir), value);
				NotifyValueChanged (CurrentDir);
			}
		}
		public string CurrentFile {
			get => Configuration.Global.Get<string> (nameof (CurrentFile));
			set {
				if (CurrentFile == value)
					return;
				Configuration.Global.Set (nameof (CurrentFile), value);
				NotifyValueChanged (CurrentFile);
			}
		}
		
		public Command CMDNew, CMDOpen, CMDSave, CMDSaveAs, CMDQuit, CMDShowLeftPane,
					CMDUndo, CMDRedo, CMDCut, CMDCopy, CMDPaste, CMDHelp, CMDAbout, CMDOptions;

		const string _defaultFileName = "unnamed.txt";
		string source = "", origSource;		
		TextBox editor;
		Stopwatch reloadChrono = new Stopwatch ();

		public new bool IsDirty => source != origSource;
		public string Source {
			get => source;
			set {
				if (source == value)
					return;
				source = value;
				CMDSave.CanExecute = IsDirty;
				if (!reloadChrono.IsRunning)
					reloadChrono.Restart ();				
				NotifyValueChanged (source);
				NotifyValueChanged ("IsDirty", IsDirty);
			}
		}
		public CommandGroup EditorCommands => new CommandGroup (CMDUndo, CMDRedo, CMDCut, CMDCopy, CMDPaste, CMDSave, CMDSaveAs);

		Stack<TextChange> undoStack = new Stack<TextChange> ();
		Stack<TextChange> redoStack = new Stack<TextChange> ();
		TextSpan selection;
		string SelectedText =>	
				selection.IsEmpty ? "" : Source.AsSpan (selection.Start, selection.Length).ToString ();

		void undo () {
			if (undoStack.TryPop (out TextChange tch)) {
				redoStack.Push (tch.Inverse (source));
				CMDRedo.CanExecute = true;
				apply (tch);
				editor.SetCursorPosition (tch.End + tch.ChangedText.Length);
			}
			if (undoStack.Count == 0)
				CMDUndo.CanExecute = false;
		}
		void redo () {
			if (redoStack.TryPop (out TextChange tch)) {
				undoStack.Push (tch.Inverse (source));
				CMDUndo.CanExecute = true;
				apply (tch);
				editor.SetCursorPosition (tch.End + tch.ChangedText.Length);
			}
			if (redoStack.Count == 0)
				CMDRedo.CanExecute = false;
		}
		void cut () {
			copy ();
			applyChange (new TextChange (selection.Start, selection.Length, ""));
		}
		void copy () {
			Clipboard = SelectedText;
		}
		void paste () {			
			applyChange (new TextChange (selection.Start, selection.Length, Clipboard));
		}
		bool disableTextChangedEvent = false;
		void apply (TextChange change) {
			Span<char> tmp = stackalloc char[source.Length + (change.ChangedText.Length - change.Length)];
			ReadOnlySpan<char> src = source.AsSpan ();
			src.Slice (0, change.Start).CopyTo (tmp);
			if (!string.IsNullOrEmpty (change.ChangedText))
				change.ChangedText.AsSpan ().CopyTo (tmp.Slice (change.Start));
			src.Slice (change.End).CopyTo (tmp.Slice (change.Start + change.ChangedText.Length));
			disableTextChangedEvent = true;
			Source = tmp.ToString ();
			disableTextChangedEvent = false;			
		}	
		
		void initCommands ()
		{
			CMDNew	= new Command ("New", new Action (onNewFile), "#Icons.blank-file.svg");			
			CMDSave = new Command ("Save", new Action (onSave), "#Icons.save.svg", false);
			CMDSaveAs = new Command ("Save As...", new Action (onSaveAs), "#Icons.save.svg");
			CMDQuit = new Command ("Quit", new Action (() => base.Quit ()), "#Icons.exit.svg");
			CMDUndo = new Command ("Undo", new Action (undo),"#Icons.undo.svg", false);
			CMDRedo = new Command ("Redo", new Action (redo),"#Icons.redo.svg", false);
			CMDCut	= new Command ("Cut", new Action (() => cut ()), "#Icons.scissors.svg", false);
			CMDCopy = new Command ("Copy", new Action (() => copy ()), "#Icons.copy-file.svg", false);
			CMDPaste= new Command ("Paste", new Action (() => paste ()), "#Icons.paste-on-document.svg", false);

		}
		void onNewFile () {
			if (IsDirty) {
				MessageBox mb = MessageBox.ShowModal (this, MessageBox.Type.YesNo, "Current file has unsaved changes, are you sure?");
				mb.Yes += (sender, e) => newFile ();
			} else
				newFile ();
		}
		void onSave ()
		{
			if (!File.Exists (CurrentFile)) {
				onSaveAs ();
				return;
			}
			save ();
		}
		void onSaveAs ()
		{
			string dir = Path.GetDirectoryName (CurrentFile);
			if (string.IsNullOrEmpty (dir))
				dir = Directory.GetCurrentDirectory ();
			LoadIMLFragment (@"<FileDialog Width='60%' Height='50%' Caption='Save as ...' CurrentDirectory='" +
				dir + "' SelectedFile='" +
				Path.GetFileName(CurrentFile) + "' OkClicked='saveFileDialog_OkClicked'/>").DataSource = this;
		}
		void saveFileDialog_OkClicked (object sender, EventArgs e)
		{
			FileDialog fd = sender as FileDialog;

			if (string.IsNullOrEmpty (fd.SelectedFileFullPath))
				return;

			if (File.Exists(fd.SelectedFileFullPath)) {
				MessageBox mb = MessageBox.ShowModal (this, MessageBox.Type.YesNo, "File exists, overwrite?");
				mb.Yes += (sender2, e2) => {
					CurrentFile = fd.SelectedFileFullPath;
					save ();
				};
				return;
			}

			CurrentFile = fd.SelectedFileFullPath;
			save ();
		}

		void newFile()
		{
			disableTextChangedEvent = true;
			Source = @"<Label Text='Hello World' Background='MediumSeaGreen' Margin='10'/>";
			disableTextChangedEvent = false;
			resetUndoRedo ();
			if (!string.IsNullOrEmpty (CurrentFile))
				CurrentFile = Path.Combine (Path.GetDirectoryName (CurrentFile), "newfile.crow");
			else
				CurrentFile = Path.Combine (CurrentDir, "newfile.crow");
		}


		void save () {
			using (Stream s = new FileStream(CurrentFile, FileMode.Create)) {
				s.WriteByte (0xEF);
				s.WriteByte (0xBB);
				s.WriteByte (0xBF);
				byte [] buff = Encoding.UTF8.GetBytes (source);
				s.Write (buff, 0, buff.Length);
			}
			origSource = source;
			NotifyValueChanged ("IsDirty", IsDirty);
			CMDSave.CanExecute = false;
		}

		void reloadFromFile () {
			hideError ();
			disableTextChangedEvent = true;
			if (File.Exists (CurrentFile)) {
				using (Stream s = new FileStream (CurrentFile, FileMode.Open)) {
					using (StreamReader sr = new StreamReader (s))
						Source = origSource = sr.ReadToEnd ();
				}
			}
			disableTextChangedEvent = false;
			resetUndoRedo ();
		}
		void reloadFromSource () {
			hideError ();
			Widget g = null;
			try {
				lock (UpdateMutex) {
					Instantiator inst = null;
					using (MemoryStream ms = new MemoryStream (Encoding.UTF8.GetBytes (source)))
						inst = new Instantiator (this, ms);
					g = inst.CreateInstance ();
					crowContainer.SetChild (g);
					g.DataSource = this;
				}
			} catch (InstantiatorException itorex) {				
				showError (itorex);
			} catch (Exception ex) {
				showError (ex);
			}
		}

		void resetUndoRedo () {
			undoStack.Clear ();
			redoStack.Clear ();
			CMDUndo.CanExecute = false;
			CMDRedo.CanExecute = false;			
		}
		void showError (Exception ex) {
			NotifyValueChanged ("ErrorMessage", ex);
			NotifyValueChanged ("ShowError", true);
		}
		void hideError () {
			NotifyValueChanged ("ShowError", false);
		}

		public void goUpDirClick (object sender, MouseButtonEventArgs e)
		{
			string root = Directory.GetDirectoryRoot (CurrentDir);
			if (CurrentDir == root)
				return;
			CurrentDir = Directory.GetParent (CurrentDir).FullName;
		}

		void Dv_SelectedItemChanged (object sender, SelectionChangeEventArgs e)
		{
			FileSystemInfo fi = e.NewValue as FileSystemInfo;
			if (fi == null)
				return;
			if (fi is DirectoryInfo)
				return;

			if (IsDirty) {
				MessageBox mb = MessageBox.ShowModal (this, MessageBox.Type.YesNo, "Current file has unsaved changes, are you sure?");
				mb.Yes += (mbsender, mbe) => { CurrentFile = fi.FullName; reloadFromFile (); };
				return;
			}

			CurrentFile = fi.FullName;
			reloadFromFile ();
		}
		void onTextChanged (object sender, TextChangeEventArgs e) {
			if (disableTextChangedEvent)
				return;
			applyChange (e.Change);
		}
		void applyChange (TextChange change) {
			undoStack.Push (change.Inverse (source));
			redoStack.Clear ();
			CMDUndo.CanExecute = true;
			CMDRedo.CanExecute = false;
			apply (change);
		}
		
		void onSelectedTextChanged (object sender, EventArgs e) {			
			selection = (sender as Label).Selection;
			Console.WriteLine($"selection:{selection.Start} length:{selection.Length}");
			CMDCut.CanExecute = CMDCopy.CanExecute = !selection.IsEmpty;
		}
		void textView_KeyDown (object sender, Crow.KeyEventArgs e) {
			if (Ctrl) {
				if (e.Key == Glfw.Key.W) {
					if (Shift)
						CMDRedo.Execute ();
					else
						CMDUndo.Execute ();
				} else if (e.Key == Glfw.Key.S) {
					onSave ();
				}
			}
		}

		protected override void OnInitialized () {
			initCommands ();

			base.OnInitialized ();

			if (string.IsNullOrEmpty (CurrentDir))
				CurrentDir = Path.Combine (Directory.GetCurrentDirectory (), "Interfaces");

			Load ("#ShowCase.showcase.crow").DataSource = this;
			crowContainer = FindByName ("CrowContainer") as Container;
			editor = FindByName ("tb") as TextBox;

			if (!File.Exists (CurrentFile))
				newFile ();
			//I set an empty object as datasource at this level to force update when new
			//widgets are added to the interface
			crowContainer.DataSource = new object ();
			hideError ();

			reloadFromFile ();
		}

		public override void UpdateFrame () {
            base.UpdateFrame ();
			if (reloadChrono.ElapsedMilliseconds < 200)
				return;
			reloadFromSource ();
			reloadChrono.Reset ();
		}

		public DbgEvtType RecordedEvents {
			get => Configuration.Global.Get<DbgEvtType> (nameof (RecordedEvents));
			set {
				if (RecordedEvents == value)
					return;				
				Configuration.Global.Set (nameof (RecordedEvents), value);				
				if (DebugLogRecording)
					DbgLogger.IncludeEvents = RecordedEvents;
				NotifyValueChanged(RecordedEvents);
			}
		}
		public DbgEvtType DiscardedEvents {
			get => Configuration.Global.Get<DbgEvtType> (nameof (DiscardedEvents));
			set {
				if (DiscardedEvents == value)
					return;
				Configuration.Global.Set (nameof (DiscardedEvents), value);
				if (DebugLogRecording)
					DbgLogger.DiscardEvents = DiscardedEvents;
				NotifyValueChanged(DiscardedEvents);
			}
		}
		public bool DebugLoggingEnabled => DbgLogger.IsEnabled;
		public bool DebugLogToFile {
			get => !DbgLogger.ConsoleOutput;
			set {
				if (DbgLogger.ConsoleOutput != value)
					return;
				DbgLogger.ConsoleOutput = !value;
				Configuration.Global.Set (nameof(DebugLogToFile), DebugLogToFile);
				NotifyValueChanged(DebugLogToFile);
			}
		}
		public string DebugLogFilePath {
			get => Configuration.Global.Get<string> (nameof (DebugLogFilePath));
			set {
				if (CurrentFile == value)
					return;
				Configuration.Global.Set (nameof (DebugLogFilePath), value);
				NotifyValueChanged (DebugLogFilePath);
			}
		}
		bool debugLogRecording;
		public bool DebugLogRecording {
			get => debugLogRecording;
			set {
				if (debugLogRecording == value)
					return;
				debugLogRecording = value;
				NotifyValueChanged(debugLogRecording);
			}
		}		

        public override bool OnKeyDown (Key key) {

            switch (key) {
            case Key.F5:
                Load ("#ShowCase.DebugLog.crow").DataSource = this;
                return true;
            case Key.F6:
				if (DebugLogRecording) {
					DbgLogger.IncludeEvents = DbgEvtType.None;
					DbgLogger.DiscardEvents = DbgEvtType.All;
					if (DebugLogToFile && !string.IsNullOrEmpty(DebugLogFilePath))
	                	DbgLogger.Save (this, DebugLogFilePath);
					DebugLogRecording = false;
 				} else {
					DbgLogger.Reset ();
					DbgLogger.IncludeEvents = RecordedEvents;
					DbgLogger.DiscardEvents = DiscardedEvents;
					DebugLogRecording = true;
				}
                return true;
            }
            return base.OnKeyDown (key);
        }
    }
}