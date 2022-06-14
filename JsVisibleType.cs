using System;
using System.Collections.Generic;
using System.Diagnostics;

using Jint.Native;
using Jint.Native.Object;

namespace JintTest
{
	[DebuggerDisplay("JsVisibleType {_name.ToString()} ({_realType.FullName,nq})")]
	internal sealed class JsVisibleType
	{
		internal readonly Type RealType;
		internal readonly JsString Name;
		internal readonly JsVisibleMember[] Members;
		internal readonly JsVisibleType? SuperType;
		internal readonly List<JsVisibleType> SubTypes;

		internal JsVisibleType(Type realType, JsString name, JsVisibleMember[] members, JsVisibleType? superType)
		{
			RealType = realType;
			Name = name;
			Members = members;
			SuperType = superType;
			SubTypes = new List<JsVisibleType>();
		}

		internal void AddMembers(ObjectInstance instance)
		{
			var engine = instance.Engine;

			for (var i = 0; i < Members.Length; i++)
			{
				var member = Members[i];
				var desc = member.Build(engine, null);
				var key = member.Symbol is null ? member.Name : engine.Realm.Intrinsics.Symbol.Get(member.Symbol);
				instance.DefineOwnProperty(key, desc);
			}
		}
	}
}
