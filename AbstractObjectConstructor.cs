using System;
using System.Collections.Generic;

using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace JintTest
{
	internal abstract class AbstractObjectConstructor : FunctionInstance, IConstructor
	{
		private static readonly Dictionary<Type, JsVisibleMember[]> jsMembers = new();

		private static JsVisibleMember[] GetMembers(Type type)
		{
			if (!jsMembers.TryGetValue(type, out var members))
				jsMembers[type] = members = Porter.GetMembers(type);

			return members;
		}

		private static JsVisibleType GetJsType(Type type, JsString name, JsVisibleType? super)
		{
			if (super != null && !super.RealType.IsAssignableFrom(type))
				throw new ArgumentException($"Type '{type}' is not assignable from super classes real type '{super.RealType}'");

			var members = GetMembers(type);
			var jsType = new JsVisibleType(type, name, members, super);
			if (super != null)
				super.SubTypes.Add(jsType);

			return jsType;
		}

		protected internal new readonly FormScriptingEngine _engine;
		private readonly JsVisibleType _instanceType;
		private readonly JsVisibleMember[] _staticMembers;

		protected ObjectInstance _prototype;

		public ObjectInstancePrototype PrototypeObject { get; }

		public new FormScriptingEngine Engine => _engine;

		public JsString Name { get; }

		protected AbstractObjectConstructor(FormScriptingEngine engine, Realm realm, Type instanceType, JsString name, FunctionPrototype prototype) : this(engine, realm, instanceType, name, prototype, realm.Intrinsics.Object.PrototypeObject)
		{
		}

		protected AbstractObjectConstructor(FormScriptingEngine engine, Realm realm, Type instanceType, JsString name, AbstractObjectConstructor prototype) : this(engine, realm, instanceType, name, prototype, prototype.PrototypeObject)
		{
		}

		private AbstractObjectConstructor(FormScriptingEngine engine, Realm realm, Type instanceType, JsString name, FunctionInstance prototype, ObjectInstance prototypeObject) : base(engine, realm, name)
		{
			_engine = engine;
			_instanceType = GetJsType(instanceType, name, (prototype as AbstractObjectConstructor)?._instanceType);
			_staticMembers = GetMembers(GetType());
			_prototype = prototype;
			Name = name;
			PrototypeObject = new ObjectInstancePrototype(engine, this, prototypeObject);
			_prototypeDescriptor = JsHelper.CreateDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
		}

		protected internal new void EnsureInitialized()
			=> base.EnsureInitialized();

		protected sealed override ObjectInstance GetPrototypeOf()
			=> _prototype;

		public sealed override bool SetPrototypeOf(JsValue value)
		{
			if (!base.SetPrototypeOf(value))
				return false;

			_prototype = base.GetPrototypeOf();
			return true;
		}

		protected sealed override void Initialize()
		{
			_instanceType.AddMembers(PrototypeObject);
			this.AddMembers(_staticMembers);

			if (_prototype is AbstractObjectConstructor ctor)
				ctor.EnsureInitialized();
		}

		public override JsValue Call(JsValue thisObject, JsValue[] arguments)
			=> throw _engine.TypeError(Name + " constructor: 'new' is required.");

		protected virtual AbstractObjectInstance Construct(JsValue[] arguments)
			=> throw _engine.TypeError("Illegal constructor.");

		public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
			=> Construct(arguments);
	}
}
