using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Jint;

namespace JintTest
{
	internal class FormScriptingEngine : Engine
	{
		public WmDateConstructor WmDate { get; }

		public WmTimeConstructor WmTime { get; }

		public UuidConstructor Uuid { get; }

		public FormScriptingEngine()
		{
			var realm = Realm;
			var fnProto = realm.Intrinsics.Function.PrototypeObject;

			WmDate = new(this, realm, fnProto);
			WmTime = new(this, realm, fnProto);
			Uuid = new(this, realm, fnProto);

			realm.GlobalObject.FastSetProperty(WmDate);
			realm.GlobalObject.FastSetProperty(WmTime);
			realm.GlobalObject.FastSetProperty(Uuid);
		}
	}
}
