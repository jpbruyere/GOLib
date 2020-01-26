﻿// Copyright (c) 2013-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using Crow.Cairo;

namespace Crow
{
	/// <summary>
	/// base class for drawing content to paint on backend
	/// </summary>
	public abstract class Fill
	{
		/// <summary>
		/// set content of fill as source for drawing operations on the backend context
		/// </summary>
		/// <param name="ctx">backend context</param>
		/// <param name="bounds">paint operation bounding box, unused for SolidColor</param>
		public abstract void SetAsSource (Context ctx, Rectangle bounds = default(Rectangle));
		public static object Parse (string s){
			if (string.IsNullOrEmpty (s))
				return null;
			if (s.Substring (1).StartsWith ("gradient", StringComparison.Ordinal))
				return (Gradient)Gradient.Parse (s);				
			if (s.EndsWith (".svg", true, System.Globalization.CultureInfo.InvariantCulture) ||
				s.EndsWith (".png", true, System.Globalization.CultureInfo.InvariantCulture) ||
			    s.EndsWith (".jpg", true, System.Globalization.CultureInfo.InvariantCulture) ||
			    s.EndsWith (".jpeg", true, System.Globalization.CultureInfo.InvariantCulture)||
			    s.EndsWith (".bmp", true, System.Globalization.CultureInfo.InvariantCulture) ||
			    s.EndsWith (".gif", true, System.Globalization.CultureInfo.InvariantCulture))
				return Picture.Parse (s);

			return (SolidColor)SolidColor.Parse (s);
		}
		public static implicit operator Color(Fill c){
			SolidColor sc = c as SolidColor;
			return sc == null ? default(Color) : sc.color;
		}
		public static implicit operator Fill(Color c){
			return new SolidColor (c);
		}

	}
}

