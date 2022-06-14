using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime.Descriptors;

namespace JintTest
{
	internal sealed class ObjectInstancePrototype : ObjectInstance
	{
		private readonly AbstractObjectConstructor _constructor;
		private ObjectInstance _prototype;

		public AbstractObjectConstructor Constructor => _constructor;

		public ObjectInstancePrototype(Engine engine, AbstractObjectConstructor constructor, ObjectInstance prototype) : base(engine)
		{
			_constructor = constructor;
			_prototype = prototype;
		}

		protected override void Initialize()
		{
			this.FastSetProperty(GlobalSymbolRegistry.ToStringTag, _constructor.Name, PropertyFlag.Configurable);
			_constructor.EnsureInitialized();
		}

		protected override ObjectInstance GetPrototypeOf()
			=> _prototype;

		public override bool SetPrototypeOf(JsValue value)
		{
			if (!base.SetPrototypeOf(value))
				return false;

			_prototype = base.GetPrototypeOf();
			return true;
		}
	}
}
