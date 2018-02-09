﻿//
// ItemTemplate.cs
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
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Xml.Serialization;
using System.Xml;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Crow.IML;

namespace Crow
{
	/// <summary> Test func on data, return yes if there's children </summary>
	public delegate bool BooleanTestOnInstance(object instance);

	/// <summary>
	/// Derived from Instantiator with sub data fetching facilities for hierarchical data access.
	/// 
	/// ItemTemplate stores the dynamic method for instantiating the control tree defined in a valid IML file.
	/// 
	/// </summary>
	public class ItemTemplate : Instantiator {
		public EventHandler Expand;
		public BooleanTestOnInstance HasSubItems;
		string strDataType;
		string fetchMethodName;

		#region CTOR
		/// <summary>
		/// Initializes a new instance of the <see cref="Crow.ItemTemplate"/> class by parsing the file passed as argument.
		/// </summary>
		/// <param name="path">IML file to parse</param>
		/// <param name="_dataType">type this item will be choosen for, or member of the data item</param>
		/// <param name="_fetchDataMethod">for hierarchical data, method to call for children fetching</param>
		public ItemTemplate(string path, string _dataType = null, string _fetchDataMethod = null)
			: base(path) {
			strDataType = _dataType;
			fetchMethodName = _fetchDataMethod;

		}
		/// <summary>
		/// Initializes a new instance of the <see cref="Crow.ItemTemplate"/> class by parsing the IML fragment passed as arg.
		/// </summary>
		/// <param name="path">IML fragment to parse</param>
		/// <param name="_dataType">type this item will be choosen for, or member of the data item</param>
		/// <param name="_fetchDataMethod">for hierarchical data, method to call for children fetching</param>
		public ItemTemplate (Stream ImlFragment, string _dataType, string _fetchDataMethod)
			:base(ImlFragment)
		{
			strDataType = _dataType;
			fetchMethodName = _fetchDataMethod;
		}
		/// <summary>
		/// Initializes a new instance of the <see cref="Crow.ItemTemplate"/> class using the opened XmlReader in args.
		/// </summary>
		/// <param name="path">XML reader positionned before or at the root node</param>
		/// <param name="_dataType">type this item will be choosen for, or member of the data item</param>
		/// <param name="_fetchDataMethod">for hierarchical data, method to call for children fetching</param>
		public ItemTemplate (XmlReader reader, string _dataType = null, string _fetchDataMethod = null)
			:base(reader)
		{
			strDataType = _dataType;
			fetchMethodName = _fetchDataMethod;
		}
		#endregion

		/// <summary>
		/// Creates the expand delegate.
		/// </summary>
		/// <param name="host">Host.</param>
		public void CreateExpandDelegate (TemplatedGroup host){
			Type dataType = null;
			//if (host.DataTest == "TypeOf"){
				dataType = CompilerServices.tryGetType(strDataType);
//				if (dataType == null) {
//					Debug.WriteLine ("ItemTemplate error: DataType not found: {0}.", strDataType);
//					return;
//				}
//			}
			Type tmpGrpType = typeof(TemplatedGroup);
			Type evtType = typeof(EventHandler);

			PropertyInfo piData = tmpGrpType.GetProperty ("Data");

			MethodInfo evtInvoke = evtType.GetMethod ("Invoke");
			ParameterInfo [] evtParams = evtInvoke.GetParameters ();
			Type handlerArgsType = evtParams [1].ParameterType;

			Type [] args = { CompilerServices.TObject, CompilerServices.TObject, handlerArgsType };

			#region Expand dyn meth
			//DM is bound to templatedGroup root (arg0)
			//arg1 is the sender of the expand event
			DynamicMethod dm = new DynamicMethod ("dyn_expand_" + fetchMethodName,
				typeof (void), args, true);

			System.Reflection.Emit.Label gotoEnd;
			System.Reflection.Emit.Label ifDataIsNull;

			ILGenerator il = dm.GetILGenerator (256);
			il.DeclareLocal(typeof(GraphicObject));

			gotoEnd = il.DefineLabel ();
			ifDataIsNull = il.DefineLabel ();

			il.Emit (OpCodes.Ldarg_1);//load sender of expand event

			il.Emit(OpCodes.Ldstr, "List");
			il.Emit (OpCodes.Callvirt, CompilerServices.miFindByName);
			il.Emit (OpCodes.Stloc_0);

			//check that 'Data' of list is not already set
			il.Emit (OpCodes.Ldloc_0);
			il.Emit (OpCodes.Callvirt, piData.GetGetMethod ());
			il.Emit (OpCodes.Brfalse, ifDataIsNull);
			il.Emit (OpCodes.Br, gotoEnd);

			il.MarkLabel(ifDataIsNull);
			//copy the ref of ItemTemplates list TODO: maybe find another way to share it among the nodes?
			FieldInfo fiTemplates = tmpGrpType.GetField("ItemTemplates");
			il.Emit (OpCodes.Ldloc_0);
			il.Emit (OpCodes.Ldarg_0);
			il.Emit (OpCodes.Ldfld, fiTemplates);
			il.Emit (OpCodes.Stfld, fiTemplates);

			//call 'fetchMethodName' from the dataSource to build the sub nodes list
			il.Emit (OpCodes.Ldloc_0);//push 'List' (of sub nodes) into the stack
			il.Emit (OpCodes.Ldarg_1);//get the dataSource of the sender
			il.Emit (OpCodes.Callvirt, CompilerServices.miGetDataSource);

			if (fetchMethodName != "self") {//special keyword self allows the use of recurent list<<<
				if (dataType == null) {
					//dataTest was not = TypeOF, so we have to get the type of data
					//dynamically and fetch

					il.Emit (OpCodes.Ldstr, fetchMethodName);
					il.Emit (OpCodes.Callvirt, CompilerServices.miGetDataTypeAndFetch);
				}else
					emitGetSubData(il, dataType);			
			}
			//set 'return' from the fetch method as 'data' of the list
			il.Emit (OpCodes.Callvirt, piData.GetSetMethod ());

			il.MarkLabel(gotoEnd);
			il.Emit (OpCodes.Ret);

			Expand = (EventHandler)dm.CreateDelegate (evtType, host);
			#endregion

			#region Items counting dyn method
			//dm is unbound, arg0 is instance of Item container to expand
			dm = new DynamicMethod ("dyn_count_" + fetchMethodName,
				typeof (bool), new Type[] {CompilerServices.TObject}, true);
			il = dm.GetILGenerator (256);

			//get the dataSource of the arg0
			il.Emit (OpCodes.Ldarg_0);
			il.Emit (OpCodes.Callvirt, CompilerServices.miGetDataSource);

			if (fetchMethodName != "self") {//special keyword self allows the use of recurent list<<<
				if (dataType == null) {
					//dataTest was not = TypeOF, so we have to get the type of data
					//dynamically and fetch

					il.Emit (OpCodes.Ldstr, fetchMethodName);
					il.Emit (OpCodes.Callvirt, CompilerServices.miGetDataTypeAndFetch);
				}else
					emitGetSubData(il, dataType);			
			}
			
			il.Emit (OpCodes.Callvirt, CompilerServices.miGetColCount);
			il.Emit (OpCodes.Ldc_I4_0);
			il.Emit (OpCodes.Cgt);
			il.Emit (OpCodes.Ret);
			HasSubItems = (BooleanTestOnInstance)dm.CreateDelegate (typeof(BooleanTestOnInstance));
			#endregion
		}

		//data is on the stack
		void emitGetSubData(ILGenerator il, Type dataType){
			MethodInfo miGetDatas = dataType.GetMethod (fetchMethodName, new Type[] {});
			if (miGetDatas == null)
				miGetDatas = CompilerServices.SearchExtMethod (dataType, fetchMethodName);

			if (miGetDatas == null) {//in last resort, search among properties
				PropertyInfo piDatas = dataType.GetProperty (fetchMethodName);
				if (piDatas == null) {
					FieldInfo fiDatas = dataType.GetField (fetchMethodName);
					if (fiDatas == null)//and among fields
						throw new Exception ("Fetch data member not found in ItemTemplate: " + fetchMethodName);
					il.Emit (OpCodes.Ldfld, fiDatas);
					return;
				}
				miGetDatas = piDatas.GetGetMethod ();
				if (miGetDatas == null)
					throw new Exception ("Read only property for fetching data in ItemTemplate: " + fetchMethodName);
			}

			il.Emit (OpCodes.Callvirt, miGetDatas);
		}
	}
}

