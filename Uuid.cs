using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;

namespace JintTest
{
	internal sealed unsafe class UuidConstructor : AbstractObjectConstructor
	{
		internal static readonly int guidSize = sizeof(Guid);

		public new static readonly JsString Name = new("Uuid");

		[JsProperty]
		public UuidInstance Empty { get; }

		public UuidConstructor(FormScriptingEngine engine, Realm realm, FunctionPrototype prototype) : base(engine, realm, typeof(UuidInstance), Name, prototype)
		{
			Empty = new UuidInstance(this, Guid.Empty);
		}

		[JsMethod]
		public UuidInstance Parse(JsString text)
			=> Guid.TryParse($"{text}", out var result) ? new(this, result) : Empty;

		public override JsValue Call(JsValue thisObject, JsValue[] arguments)
		{
			if (arguments.Length == 0)
				return new UuidInstance(this);

			var text = arguments[0].ToString();
			if (!Guid.TryParse(text, out var result))
				throw _engine.TypeError("Invalid UUID string.");

			return new UuidInstance(this, result);
		}

		public UuidInstance Construct(Guid value)
			=> new(this, value);

		protected override unsafe AbstractObjectInstance Construct(JsValue[] arguments)
		{
			var arg = arguments.At(0);
			if (arg.IsUndefined())
				return new UuidInstance(this);

			if (arg.IsString())
			{
				var str = arg.ToString();
				if (!Guid.TryParse(str, out var result))
					throw _engine.TypeError("Invalid UUID string.");

				return new UuidInstance(this, result);
			}
			else if (arg.IsUint8Array())
			{
				var result = stackalloc byte[guidSize];

				for (int i = 0; i < guidSize; i++)
				{
					var value = arg.Get(i);
					var b = (byte)TypeConverter.ToInt32(value);
					result[i] = b;
				}

				return new UuidInstance(this, *(Guid*)result);
			}

			throw _engine.TypeError($"Expected string or Uint8Array, recieved {arg.Typeof()}.");
		}
	}

	internal class UuidInstance : AbstractObjectInstance, IPrimitiveLike
	{
		internal readonly Guid _value;

		JsString IPrimitiveLike.TypeName => Constructor.Name;

		internal UuidInstance(UuidConstructor constructor) : base(constructor)
		{
			_value = Guid.NewGuid();
		}

		internal UuidInstance(UuidConstructor constructor, Guid value) : base(constructor)
		{
			_value = value;
		}

		[JsMethod]
		public JsValue GetBytes()
		{
			var bytes = _value.ToByteArray();
			return _engine.Realm.Intrinsics.Uint8Array.Construct(bytes);
		}

		[JsMethod]
		public JsValue IsEmpty()
			=> _value == Guid.Empty;

		[JsMethod]
		public JsValue ToJSON()
			=> _value.ToString("B");

		[JsMethod(Name = "toString", Length = 0)]
		public JsValue ToJsString(JsValue noHyphens)
		{
			var format = TypeConverter.ToBoolean(noHyphens) ? "N" : "B";
			return _value.ToString(format);
		}

		[JsMethod(Name = "equals")]
		public JsValue JsEquals(JsValue other)
		{
			return other switch
			{
				UuidInstance v => _value == v._value,
				JsString v => Guid.TryParse($"{v}", out var result) && _value == result,
				_ => false,
			};
		}
	}
}
