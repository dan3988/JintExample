using System.Reflection;

using Jint;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.ArrayBuffer;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace JintTest
{
	internal static class JsHelper
	{
		private sealed class InternalPropertyDescriptor : PropertyDescriptor
		{
			internal InternalPropertyDescriptor(JsValue value, PropertyFlag flags) : base(value, flags)
			{
			}
		}

		private sealed class InternalGetSetPropertyDescriptor : PropertyDescriptor
		{
			public override JsValue Get { get; }

			public override JsValue Set { get; }

			internal InternalGetSetPropertyDescriptor(JsValue get, JsValue set, PropertyFlag flags) : base(flags)
			{
				Get = get;
				Set = set;
			}
		}

		private sealed class InstanceFunction<T> : FunctionInstance
			where T : ObjectInstance
		{
			private readonly JsString _typeName;
			private readonly Func<T, JsValue[], JsValue> _func;

			internal InstanceFunction(Engine engine, Realm realm, JsString name, JsNumber length, JsString typeName, Func<T, JsValue[], JsValue> func) : base(engine, realm, name)
			{
				_length = new InternalPropertyDescriptor(length, PropertyFlag.Configurable);
				_typeName = typeName;
				_func = func;
			}

			public override JsValue Call(JsValue thisObject, JsValue[] arguments)
			{
				var thisObj = thisObject as T;
				if (thisObj != null)
					return _func.Invoke(thisObj, arguments);

				throw _engine.TypeError("this is not a " + _typeName + " instance.");
			}
		}

		private sealed class EnumInfo
		{
			public readonly JsValue Name;
			public readonly JsValue[] Names;
			public readonly JsValue[] Values;
			public readonly int Length;

			public (JsValue name, JsValue value) this[int index] => (Names[index], Values[index]);

			public EnumInfo(string name, int length)
			{
				Name = name;
				Names = new JsValue[length];
				Values = new JsValue[length];
				Length = length;
			}
		}

		private static readonly char[] nsSplit = { '.' };
		private const Types nullish = Types.Undefined | Types.Null;

		public static readonly JsValue[] EmptyArgs = Array.Empty<JsValue>();

		public static readonly JsNumber Zero = new(0);
		public static readonly JsNumber One = new(1);
		public static readonly JsNumber Two = new(2);
		public static readonly JsNumber NaN = new(double.NaN);

		private static readonly HashSet<char> vowels = new()
		{
			'a',
			'e',
			'i',
			'o',
			'u',
			'A',
			'E',
			'I',
			'O',
			'U'
		};

		private static readonly Dictionary<Type, JsString> typeofLookup = new()
		{
			[typeof(JsUndefined)] = CommonNames.Undefined,
			[typeof(JsNull)] = CommonNames.Object,
			[typeof(JsString)] = CommonNames.String,
			[typeof(JsNumber)] = CommonNames.Number,
			[typeof(JsBoolean)] = CommonNames.Boolean,
			[typeof(JsSymbol)] = CommonNames.Symbol,
			[typeof(ObjectInstance)] = CommonNames.Object,
			[typeof(FunctionInstance)] = CommonNames.Function
		};

		private static readonly Dictionary<Type, EnumInfo> enums = new();

		private static Func<ArrayBufferInstance, byte[]>? byteArrayGettter; 

		internal static string ToJsName(string str)
			=> ToJsName(str.AsMemory());

		internal static string ToJsName(ReadOnlyMemory<char> str)
			=> string.Create(str.Length, str, (span, str) =>
			{
				var src = str.Span;
				span[0] = char.ToLower(src[0]);
				src[1..].CopyTo(span[1..]);
			});

		public static JsString Typeof(this JsValue value)
		{
			return value.Type switch
			{
				Types.Undefined => CommonNames.Undefined,
				Types.Boolean => CommonNames.Boolean,
				Types.String => CommonNames.String,
				Types.Number => CommonNames.Number,
				Types.Symbol => CommonNames.Symbol,
				Types.Object => value is ICallable ? CommonNames.Function : CommonNames.Object,
				_ => CommonNames.Object,
			};
		}

		public static JsString Typeof(this Type type, bool useClassName)
		{
			if (!typeof(JsValue).IsAssignableFrom(type))
				throw new ArgumentException($"Type \"{type}\" does not extend Jint.Native.JsValue.");

			if (typeofLookup.TryGetValue(type, out var t))
				return t;

			if (typeof(ICallable).IsAssignableFrom(type))
				return CommonNames.Function;

			if (useClassName)
				return new(type.Name.EndsWith("Instance") ? type.Name[..^8] : type.Name);

			return CommonNames.Object;
		}

		public const PropertyFlag DefaultValueFlags = PropertyFlag.ConfigurableEnumerableWritable;
		public const PropertyFlag DefaultPropertyFlags = PropertyFlag.Enumerable | PropertyFlag.Configurable;
		public const PropertyFlag DefaultFunctionFlags = PropertyFlag.Writable | PropertyFlag.Configurable;

		public static PropertyDescriptor CreateDescriptor(JsValue value, PropertyFlag flags = DefaultValueFlags)
			=> new InternalPropertyDescriptor(value, flags);

		public static PropertyDescriptor CreateDescriptor(JsValue get, JsValue set, PropertyFlag flags = DefaultPropertyFlags)
			=> new InternalGetSetPropertyDescriptor(get, set, flags);

		public static PropertyDescriptor CreateInstanceFunctionDescriptor<T>(Engine engine, JsString name, JsString typeName, JsNumber length, Func<T, JsValue[], JsValue> func, PropertyFlag flags = DefaultFunctionFlags) where T : ObjectInstance
			=> new InternalPropertyDescriptor(new InstanceFunction<T>(engine, engine.Realm, name, length, typeName, func), flags);

		public static FunctionInstance CreateInstanceFunction<T>(Engine engine, JsString name, JsString typeName, JsNumber length, Func<T, JsValue[], JsValue> func) where T : ObjectInstance
			=> new InstanceFunction<T>(engine, engine.Realm, name, length, typeName, func);

		public static void FastSetProperty(this ObjectInstance @this, JsValue property, JsValue value, PropertyFlag flag = DefaultValueFlags)
			=> @this.FastSetProperty(property, new InternalPropertyDescriptor(value, flag));

		public static void FastSetProperty(this ObjectInstance @this, AbstractObjectConstructor value, PropertyFlag flag = DefaultFunctionFlags)
			=> @this.FastSetProperty(value.Name, new InternalPropertyDescriptor(value, flag));

		public static bool IsVowel(this char c)
			=> vowels.Contains(c);

		public static bool IsNullish(this JsValue value)
			=> (value.Type & nullish) != 0;

		public static T RequireThis<T>(this Engine engine, JsValue name, JsValue thisObject) where T : class
		{
			if (thisObject is T t)
				return t;

			throw TypeError(engine, $"{name} called on incompatible type.");
		}

		public static T GetArgument<T>(this JsValue[] arguments, Engine engine, int index, string? typeName = null)
		{
			var arg = arguments.At(index);
			if (arg is T t)
				return t;

			if (string.IsNullOrEmpty(typeName))
			{
				typeName = typeof(T).Name;
				if (typeName.EndsWith("Instance"))
					typeName = typeName[..^8];
			}

			throw engine.TypeError($"Argument {index + 1} is not {(typeName[0].IsVowel() ? "an" : "a")} {typeName}");
		}

		public static ObjectInstance CreateEnum<T>(this Engine engine) where T : unmanaged, Enum
			=> CreateEnumInternal(engine, typeof(T));

		public static ObjectInstance CreateEnum(this Engine engine, Type enumType)
		{
			if (!enumType.IsEnum)
				throw new ArgumentException($"Type '{enumType.FullName}' is not an enum.", nameof(enumType));

			return CreateEnumInternal(engine, enumType);
		}

		private static ObjectInstance CreateEnumInternal(Engine engine, Type enumType)
		{
			var obj = new ObjectInstance(engine);
			AddEnumValuesInternal(obj, enumType, out _);
			return obj;
		}

		public static void AddEnum<T>(this ObjectInstance target) where T : unmanaged, Enum
			=> AddEnumInternal(target, typeof(T));

		public static void AddEnum(this ObjectInstance target, Type enumType)
		{
			if (!enumType.IsEnum)
				throw new ArgumentException($"Type '{enumType.FullName}' is not an enum.", nameof(enumType));

			AddEnumInternal(target, enumType);
		}

		public static void AddEnumInternal(this ObjectInstance target, Type enumType)
		{
			var obj = new ObjectInstance(target.Engine);
			AddEnumValuesInternal(obj, enumType, out var info);
			target.Set(info.Name, obj);
		}

		public static void AddEnumValues<T>(this ObjectInstance instance) where T : unmanaged, Enum
			=> AddEnumValuesInternal(instance, typeof(T), out _);

		public static void AddEnumValues(this ObjectInstance instance, Type enumType)
		{
			if (!enumType.IsEnum)
				throw new ArgumentException($"Type '{enumType.FullName}' is not an enum.", nameof(enumType));

			AddEnumValuesInternal(instance, enumType, out _);
		}

		private static void AddEnumValuesInternal(ObjectInstance instance, Type enumType, out EnumInfo info)
		{
			if (!enums.TryGetValue(enumType, out info!))
			{
				var values = enumType.GetEnumValues();
				var names = enumType.GetEnumNames();
				info = new EnumInfo(enumType.Name, values.Length);

				for (var i = 0; i < info.Length; i++)
				{
					var value = (IConvertible)values.GetValue(i)!;
					info.Names[i] = names[i];
					info.Values[i] = value.ToInt32(null);
				}

				enums[enumType] = info;
			}

			for (var i = 0; i < info.Length; i++)
			{
				var (name, value) = info[i];
				instance.FastSetProperty(name, value);
				instance.FastSetProperty(value, name);
			}
		}

		public static ArrayInstance CreateArray(this Engine engine, PropertyDescriptor[] props)
		{
			var array = new ArrayInstance(engine, props);
			array.SetPrototypeOf(engine.Realm.Intrinsics.Array.PrototypeObject);
			return array;
		}

		public static ArrayInstance CreateReadOnlyArray(this Engine engine, IReadOnlyCollection<JsValue> values)
			=> CreateReadOnlyArray(engine, values.Count, values);

		public static ArrayInstance CreateReadOnlyArray(this Engine engine, IReadOnlyCollection<string> values)
			=> CreateReadOnlyArray(engine, values.Count, values.Select(v => (JsValue)v));

		public static ArrayInstance CreateReadOnlyArray(this Engine engine, IReadOnlyCollection<double> values)
			=> CreateReadOnlyArray(engine, values.Count, values.Select(v => (JsValue)v));

		private static ArrayInstance CreateReadOnlyArray(this Engine engine, int count, IEnumerable<JsValue> values)
		{
			const PropertyFlag flags = PropertyFlag.Enumerable;

			var i = 0;
			var props = new PropertyDescriptor[count];

			foreach (var value in values)
				props[i++] = CreateDescriptor(value, flags);

			var array = new ArrayInstance(engine, props);
			var lengthDesc = array.GetOwnProperty(CommonNames.Length);
			lengthDesc.Writable = false;
			lengthDesc.Configurable = false;

			array.SetPrototypeOf(engine.Realm.Intrinsics.Array.PrototypeObject);

			return array;
		}

		public static JavaScriptException TypeError(this JsValue value, string message)
			=> value is ObjectInstance obj ? TypeError(obj.Engine, message) : new JavaScriptException(message);

		public static JavaScriptException TypeError(this ObjectInstance value, string message)
			=> TypeError(value.Engine, message);

		public static JavaScriptException TypeError(this Engine engine, string message, Exception? cause = null)
			=> new JavaScriptException(engine.Realm.Intrinsics.TypeError, message, cause);

		public static JavaScriptException TypeError(this Engine engine, string format, object? arg0)
			=> TypeError(engine, string.Format(format, arg0));

		public static JavaScriptException TypeError(this Engine engine, string format, object? arg0, object? arg1)
			=> TypeError(engine, string.Format(format, arg0, arg1));

		public static JavaScriptException TypeError(this Engine engine, string format, params object?[] args)
			=> TypeError(engine, string.Format(format, args));

		public static JsValue GetValue(this ObjectInstance obj, PropertyDescriptor desc)
		{
			if (desc.Get is ICallable getter)
				return getter.Call(obj, EmptyArgs);

			return desc.Value;
		}

		/// <summary>
		/// Get the underlying byte array of an <see cref="ArrayBufferInstance"/> instead of copying the data to a new instance, like <see cref="JsValueExtensions.AsUint8Array(JsValue)"/> does.
		/// </summary>
		public static byte[] GetData(this ArrayBufferInstance buffer)
		{
			if (byteArrayGettter == null)
			{
#nullable disable
				var prop = typeof(ArrayBufferInstance).GetProperty("ArrayBufferData", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				byteArrayGettter = (Func<ArrayBufferInstance, byte[]>)Delegate.CreateDelegate(typeof(Func<ArrayBufferInstance, byte[]>), prop.GetMethod);
#nullable restore
			}

			return byteArrayGettter.Invoke(buffer);
		}
	}
}
