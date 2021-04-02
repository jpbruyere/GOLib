﻿// Copyright (c) 2013-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.ComponentModel;
using Crow.Cairo;
using System.Linq;

namespace Crow
{
	public class TabView : TemplatedGroup
	{
		#region CTOR
		public TabView () { }
		public TabView (Interface iface, string style = null) : base (iface, style) { }
		#endregion

		Orientation orientation;

		[DefaultValue (Orientation.Horizontal)]
		public Orientation Orientation {
			get => orientation;
			set {
				if (orientation == value)
					return;
				orientation = value;
				NotifyValueChangedAuto (orientation);
				NotifyValueChanged ("OppositeOrientation", OppositeOrientation);
			}
		}
		public Orientation OppositeOrientation 
			=> orientation == Orientation.Vertical ? Orientation.Horizontal : Orientation.Vertical;		
		public Measure TabWidth
			=> orientation == Orientation.Vertical ? Measure.Stretched : Measure.Fit;		
		public Measure TabHeight
			=> orientation == Orientation.Horizontal ? Measure.Stretched : Measure.Fit;		
	}
}

