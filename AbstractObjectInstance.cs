using Jint.Native;
using Jint.Native.Object;

namespace JintTest
{
	internal abstract class AbstractObjectInstance : ObjectInstance
	{
		protected new readonly FormScriptingEngine _engine;
		private readonly AbstractObjectConstructor _constructor;
		private bool _prototypeSet;

		internal AbstractObjectConstructor Constructor => _constructor;

		protected AbstractObjectInstance(AbstractObjectConstructor constructor) : base(constructor._engine)
		{
			_engine = constructor._engine;
			_constructor = constructor;
		}

		protected internal new void EnsureInitialized()
			=> base.EnsureInitialized();

		protected override void Initialize()
		{
			base.Initialize();
			_constructor.EnsureInitialized();
		}

		protected sealed override ObjectInstance GetPrototypeOf()
			=> _prototypeSet ? base.GetPrototypeOf() : _constructor.PrototypeObject;

		public sealed override bool SetPrototypeOf(JsValue value)
			=> base.SetPrototypeOf(value) && (_prototypeSet = true);
	}
}
