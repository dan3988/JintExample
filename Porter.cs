using System.Reflection;

using Jint;
using Jint.Native;
using Jint.Native.Object;

namespace JintTest
{
	internal static class Porter
	{
		private const BindingFlags declaredFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance;

		internal static JsVisibleType BuildType(Type delcaringType, JsVisibleType? superType, JsString typeName)
		{
			var members = GetMembers(delcaringType);
			return new JsVisibleType(delcaringType, typeName, members, superType);
		}

		internal static JsVisibleMember[] GetMembers(Type declaringType)
		{
			var props = declaringType.GetProperties(declaredFlags).AsSpan();
			var propCount = 0;
			var methods = declaringType.GetMethods(declaredFlags).AsSpan();
			var methodCount = 0;
			var memberNames = new JsMemberAttribute[methods.Length + props.Length];

			for (var i = 0; i < props.Length; i++)
			{
				var prop = props[i];
				var attr = prop.GetCustomAttribute<JsPropertyAttribute>();
				if (attr != null)
				{
					memberNames[propCount] = attr;
					props[propCount++] = prop;
				}
			}

			for (var i = 0; i < methods.Length; i++)
			{
				var method = methods[i];
				var attr = method.GetCustomAttribute<JsMethodAttribute>();
				if (attr != null)
				{
					memberNames[propCount + methodCount] = attr;
					methods[methodCount++] = method;
				}
			}

			props = props[..propCount];
			methods = methods[..methodCount];

			if (props.IsEmpty && methods.IsEmpty)
				return Array.Empty<JsVisibleMember>();

			var members = new JsVisibleMember[propCount + methodCount];
			var memberCount = 0;

			while (memberCount < propCount)
			{
				var attr = memberNames[memberCount];
				var prop = props[memberCount];
				var name = attr.Name ?? ToJsName(prop.Name);
				members[memberCount++] = JsVisibleMember.Create(declaringType, name, attr.Symbol, attr.Flags, prop);
			}

			for (var i = 0; i < methodCount; i++)
			{
				var attr = (JsMethodAttribute)memberNames[memberCount];
				var method = methods[i];
				var name = attr.Name ?? ToJsName(method.Name);
				members[memberCount++] = JsVisibleMember.Create(declaringType, name, attr.Symbol, attr.Flags, method, attr.GetLengthValue());
			}

			return members;
		}

		public static void AddMembers(this ObjectInstance @this, IReadOnlyCollection<JsVisibleMember> members, object? staticObject = null)
		{
			var engine = @this.Engine;
			foreach (var member in members)
			{
				var desc = member.Build(engine, staticObject);
				var key = (JsValue)member.Name;
				if (member.Symbol is not null)
					key = engine.Realm.Intrinsics.Symbol.Get(key);

				@this.FastSetProperty(key, desc);
			}
		}

		internal static string ToJsName(string str)
			=> ToJsName(str.AsMemory());

		internal static string ToJsName(ReadOnlyMemory<char> str)
			=> string.Create(str.Length, str, (span, str) =>
			{
				var src = str.Span;
				span[0] = char.ToLower(src[0]);
				src[1..].CopyTo(span[1..]);
			});

		internal static void AddMembers(ObjectInstance instance, Type type)
		{
			if (!typeof(JsValue).IsAssignableFrom(type))
				throw new ArgumentException($"Type '{type}' is not assignable from 'Jint.Native.JsValue'.");

			AddMembers(instance.Engine, instance, type, null);
		}

		internal static void AddStaticMembers(ObjectInstance instance, object staticObject)
			=> AddMembers(instance.Engine, instance, staticObject.GetType(), staticObject);

		private static void AddMembers(Engine engine, ObjectInstance instance, Type type, object? staticObject)
		{
			var flags = declaredFlags | BindingFlags.Instance;
			var props = type.GetProperties(flags);

			for (int i = 0; i < props.Length; i++)
			{
				var prop = props[i];
				var attr = prop.GetCustomAttribute<JsPropertyAttribute>();
				if (attr == null)
					continue;

				var name = new JsString(attr.Name ?? ToJsName(prop.Name));
				var getter = prop.CanRead ? JsInvoker.CreateForGetter(engine, type, prop, name, staticObject) : JsValue.Undefined;
				var setter = prop.CanWrite ? JsInvoker.CreateForSetter(engine, type, prop, name, staticObject) : JsValue.Undefined;
				var desc = JsHelper.CreateDescriptor(getter, setter, attr.Flags);

				instance.DefineOwnProperty(name, desc);
			}

			var methods = type.GetMethods(flags);

			for (int i = 0; i < methods.Length; i++)
			{
				var method = methods[i];
				var attr = method.GetCustomAttribute<JsMethodAttribute>();
				if (attr == null)
					continue;

				var name = new JsString(attr.Name ?? ToJsName(method.Name));
				var function = JsInvoker.CreateForMethod(instance.Engine, type, method, name, attr.GetLengthValue(), staticObject);
				var desc = JsHelper.CreateDescriptor(function, attr.Flags);

				instance.DefineOwnProperty(name, desc);
			}
		}
	}
}
