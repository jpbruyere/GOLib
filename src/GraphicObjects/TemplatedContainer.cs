﻿//
// TemplatedContainer.cs
//
// Author:
//       Jean-Philippe Bruyère <jp.bruyere@hotmail.com>
//
// Copyright (c) 2013-2017 Jean-Philippe Bruyère
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Xml.Serialization;
using System.Xml;
using System.Reflection;

namespace Crow
{
	public abstract class TemplatedContainer : TemplatedControl
	{
		#region CTOR
		public TemplatedContainer() : base(){}
		public TemplatedContainer (Interface iface) : base(iface){}
		#endregion

		[XmlAttributeAttribute]public virtual GraphicObject Content{ get; set;}

		#region GraphicObject overrides
		public override GraphicObject FindByName (string nameToFind)
		{
			if (Name == nameToFind)
				return this;

			return Content == null ? null : Content.FindByName (nameToFind);
		}
		public override bool Contains (GraphicObject goToFind)
		{
			if (Content == null)
				return base.Contains (goToFind);

			if (Content == goToFind)
				return true;
			return Content.Contains (goToFind);
		}
		#endregion
	}
}

