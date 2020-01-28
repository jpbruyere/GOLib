﻿// Copyright (c) 2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Crow.XLib
{
	public class XLibBackend : IBackend
	{
		#region pinvoke
		[DllImport("X11")]
		static extern int XInitThreads();
		[DllImport("X11")]
		internal static extern IntPtr XOpenDisplay(IntPtr displayName);
		[DllImport("X11")]
		internal static extern IntPtr XCloseDisplay(IntPtr disp);
		[DllImport("X11")]
		static extern Int32 XDefaultScreen(IntPtr disp);
		[DllImport("X11")]
		static extern IntPtr XDefaultRootWindow(IntPtr disp);
		[DllImport("X11")]
		static extern UInt32 XDefaultDepth (IntPtr disp, Int32 screen);
		[DllImport("X11")]
		static extern IntPtr XDefaultVisual(IntPtr disp, Int32 screen);
		[DllImport("X11")]
		static extern IntPtr XCreateSimpleWindow(IntPtr disp, IntPtr rootWindow, Int32 x, Int32 y, UInt32 width, UInt32 height,
			UInt32 borderWidth, IntPtr border, IntPtr background);
		[DllImport("X11")]
		static extern IntPtr XCreatePixmap(IntPtr disp, IntPtr rootWindow, UInt32 width, UInt32 height, UInt32 depth);
		[DllImport("X11")]
		static extern IntPtr XFreePixmap(IntPtr disp, IntPtr pixmap);
		[DllImport("X11")]
		static extern IntPtr XFree(IntPtr data);
		[DllImport("X11")]
		static extern Int32 XSelectInput(IntPtr disp, IntPtr win, EventMask eventMask);
		[DllImport("X11")]
		static extern Int32 XMapWindow(IntPtr disp, IntPtr win);
		[DllImport("X11")]
		static extern int XPending (IntPtr disp);
		[DllImport("X11")]
		static extern IntPtr XNextEvent(IntPtr disp, ref XEvent xevent);
		[DllImport("X11")]
		static extern Int32 XSync(IntPtr disp, int discard);
		[DllImport("X11")]
		static extern int XConnectionNumber(IntPtr disp);
		[DllImport("X11")]
		static extern IntPtr XSetErrorHandler(XErrorHandler error_handler);

		#endregion

		IntPtr xDisp, xwinHnd, xDefaultRootWin, xDefaultVisual;
		UInt32 xDefaultDepth;
		Int32 xScreen;
		XErrorHandler errorHnd;

		Interface iFace;
		X11Keyboard keyboard;

		#region IBackend implementation

		public void Init (Interface _iFace)
		{
			iFace = _iFace;
			XInitThreads ();
			xDisp = XOpenDisplay(IntPtr.Zero);
			if (xDisp == IntPtr.Zero)
				throw new NotSupportedException("[XLib] Failed to open display.");

			xScreen = XDefaultScreen(xDisp);

			xDefaultRootWin = XDefaultRootWindow (xDisp);
			xDefaultVisual = XDefaultVisual (xDisp, xScreen);
			xDefaultDepth = XDefaultDepth (xDisp, xScreen);

			xwinHnd = XCreateSimpleWindow (xDisp, xDefaultRootWin,
				0, 0, (uint)iFace.ClientRectangle.Width, (uint)iFace.ClientRectangle.Height, 0, IntPtr.Zero, IntPtr.Zero);
			if (xwinHnd == IntPtr.Zero)
				throw new NotSupportedException("[XLib] Failed to create window.");

			XSelectInput (xDisp, xwinHnd, EventMask.ExposureMask | 
				EventMask.KeyPressMask	| EventMask.KeyReleaseMask | 
				EventMask.PointerMotionMask | EventMask.ButtonPressMask | EventMask.ButtonReleaseMask);

			XMapWindow (xDisp, xwinHnd);

			keyboard = new Crow.XLib.X11Keyboard (xDisp);

			iFace.surf = new Cairo.XlibSurface (xDisp, xwinHnd, xDefaultVisual, iFace.ClientRectangle.Width, iFace.ClientRectangle.Height);

			errorHnd = new XErrorHandler (HandleError);
			XSetErrorHandler (errorHnd);
		}

		public void CleanUp ()
		{
			keyboard.Destroy ();

			XCloseDisplay (xDisp);
		}
		public void Flush () {
			XSync (xDisp, 0);
		}
		public void ProcessEvents ()
		{
			if (XPending (xDisp) > 0) {
				XEvent xevent = new XEvent ();
				XNextEvent (xDisp, ref xevent);

				switch (xevent.type) {
				case XEventName.Expose:
					iFace.ProcessResize (new Rectangle (0, 0, xevent.ExposeEvent.width, xevent.ExposeEvent.height));
					break;
				case XEventName.KeyPress:
					keyboard.HandleEvent ((uint)xevent.KeyEvent.keycode, true);
					break;
				case XEventName.KeyRelease:
					keyboard.HandleEvent ((uint)xevent.KeyEvent.keycode, false);
					break;
				case XEventName.MotionNotify:
					//Debug.WriteLine ("motion: ({0},{1})", xevent.MotionEvent.x, xevent.MotionEvent.y);
					iFace.OnMouseMove (xevent.MotionEvent.x, xevent.MotionEvent.y);
					break;
				case XEventName.ButtonPress:
					//Debug.WriteLine ("button press: {0}", xevent.ButtonEvent.button);
					if (xevent.ButtonEvent.button == 4)
						iFace.OnMouseWheelChanged (Interface.WheelIncrement);
					else if(xevent.ButtonEvent.button == 5)
						iFace.OnMouseWheelChanged (-Interface.WheelIncrement);
					else
						iFace.OnMouseButtonDown ((MouseButton)(xevent.ButtonEvent.button - 1));
					break;
				case XEventName.ButtonRelease:
					//Debug.WriteLine ("button release: {0}", xevent.ButtonEvent.button);
					iFace.OnMouseButtonUp ((MouseButton)(xevent.ButtonEvent.button - 1));
					break;
				default:
					Console.WriteLine (xevent);
					break;
				}
			}
		}
		public bool IsDown (Key key) {
			return false;
		}
		public bool Shift {
			get {
				throw new NotImplementedException ();
			}
		}

		public bool Ctrl {
			get {
				throw new NotImplementedException ();
			}
		}

		public bool Alt {
			get {
				throw new NotImplementedException ();
			}
		}
		public MouseCursor Cursor { set { throw new NotImplementedException (); } }
		#endregion

		int HandleError (IntPtr display, ref XErrorEvent error_event)
		{
			/*if (ErrorExceptions)
				throw new X11Exception (error_event.display, error_event.resourceid,
					error_event.serial, error_event.error_code,
					error_event.request_code, error_event.minor_code);
			else
				Console.WriteLine ("X11 Error encountered: {0}{1}\n",
					X11Exception.GetMessage(error_event.display, error_event.resourceid,
						error_event.serial, error_event.error_code,
						error_event.request_code, error_event.minor_code),
					WhereString());*/
			Debug.WriteLine ("XERROR {0}", error_event.error_code);
			return 0;
		}
	}
}

