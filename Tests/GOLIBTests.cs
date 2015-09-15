#define MONO_CAIRO_DEBUG_DISPOSE


using System;
using System.Runtime.InteropServices;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;

using System.Diagnostics;

//using GGL;
using go;
using System.Threading;


namespace test
{
	class GOLIBTests : OpenTKGameWindow, IValueChange
	{
		#region IValueChange implementation

		public event EventHandler<ValueChangeEventArgs> ValueChanged;

		#endregion

		public GOLIBTests ()
			: base(800, 600,"test")
		{}

		int frameCpt = 0;
		int idx = 0;
		string[] testFiles = {
			"testGroupBox.goml",
			"testBorder.goml",
			"testCheckbox.goml",
			"testWindow.goml",
			"test0.goml",
			"test1.goml",
			"test1.1.goml",
			"test1.2.goml",
			"testLabel.goml",
			"testContainer.goml",
			"testRadioButton.goml",
			"testSpinner.goml",
			"testPopper.goml",
			"testExpandable.goml",
			"testMsgBox.goml",
			"testGrid.goml",
			"fps.goml",
			"testMeter.goml",
			"test6.goml",
			"testHStack.goml",
			"test2.goml",
			"test_stack.goml",
			"testScrollbar.goml",
			"test3.goml",
			"test4.1.goml",
			"test7.goml",
		};

		#region FPS
		int _fps = 0;

		public int fps {
			get { return _fps; }
			set {
				if (_fps == value)
					return;

				_fps = value;

				if (_fps > fpsMax) {
					fpsMax = _fps;
					ValueChanged.Raise(this, new ValueChangeEventArgs ("fpsMax", fpsMax));
				} else if (_fps < fpsMin) {
					fpsMin = _fps;
					ValueChanged.Raise(this, new ValueChangeEventArgs ("fpsMin", fpsMin));
				}

				ValueChanged.Raise(this, new ValueChangeEventArgs ("fps", _fps));
				ValueChanged.Raise (this, new ValueChangeEventArgs ("update",
					this.updateTime.ElapsedMilliseconds.ToString () + " ms"));
			}
		}

		public int fpsMin = int.MaxValue;
		public int fpsMax = 0;

		void resetFps ()
		{
			fpsMin = int.MaxValue;
			fpsMax = 0;
			_fps = 0;
		}
		public string update = "";
		#endregion

		protected override void OnLoad (EventArgs e)
		{
			base.OnLoad (e);
			LoadInterface("Interfaces/" + testFiles[idx]);
		}
		protected override void OnUpdateFrame (FrameEventArgs e)
		{
			base.OnUpdateFrame (e);

			fps = (int)RenderFrequency;


			if (frameCpt > 200) {
				resetFps ();
				frameCpt = 0;
			}
			frameCpt++;
		}
		protected override void OnKeyDown (KeyboardKeyEventArgs e)
		{
			base.OnKeyDown (e);
			if (e.Key == Key.Escape) {
				this.Quit ();
			}
			ClearInterface ();
			idx++;
			if (idx == testFiles.Length)
				idx = 0;
			this.Title = testFiles [idx];
			LoadInterface("Interfaces/" + testFiles[idx]);

		}

		[STAThread]
		static void Main ()
		{
			Console.WriteLine ("starting example");

			using (GOLIBTests win = new GOLIBTests( )) {
				win.Run (30.0);
			}
		}
	}
}