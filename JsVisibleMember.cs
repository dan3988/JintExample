using System.Diagnostics;
using System.Reflection;

using Jint;
using Jint.Native;
using Jint.Runtime.Descriptors;

namespace JintTest
{
	[DebuggerDisplay("{GetType().Name,nq} {Name.ToString()}")]
	internal abstract class JsVisibleMember
	{
		private sealed class JsVisibleMethod : JsVisibleMember
		{
			internal readonly MethodInfo Method;
			internal readonly JsValue? Length;

			public JsVisibleMethod(Type declaringType, string name, string? symbol, PropertyFlag flags, MethodInfo method, JsValue? length) : base(declaringType, name, symbol, flags)
			{
				Method = method;
				Length = length;
			}

			internal override PropertyDescriptor Build(Engine engine, object? staticObject)
			{
				var function = JsInvoker.CreateForMethod(engine, DeclaringType, Method, Name, Length, staticObject);
				return JsHelper.CreateDescriptor(function, Flags);
			}
		}

		private sealed class JsVisibleProperty : JsVisibleMember
		{
			internal readonly PropertyInfo Property;

			public JsVisibleProperty(Type declaringType, string name, string? symbol, PropertyFlag flags, PropertyInfo property) : base(declaringType, name, symbol, flags)
			{
				Property = property;
			}

			internal override PropertyDescriptor Build(Engine engine, object? staticObject)
			{
				var getter = Property.CanRead ? JsInvoker.CreateForGetter(engine, DeclaringType, Property, Name, staticObject) : JsValue.Undefined;
				var setter = Property.CanWrite ? JsInvoker.CreateForSetter(engine, DeclaringType, Property, Name, staticObject) : JsValue.Undefined;
				return JsHelper.CreateDescriptor(getter, setter, Flags);
			}
		}

		internal static JsVisibleMember Create(Type declaringType, string name, string? symbol, PropertyFlag flags, MethodInfo method, int? length)
			=> new JsVisibleMethod(declaringType, name, symbol, flags, method, length.HasValue ? (JsValue)length : null);

		internal static JsVisibleMember Create(Type declaringType, string name, string? symbol, PropertyFlag flags, PropertyInfo property)
			=> new JsVisibleProperty(declaringType, name, symbol, flags, property);

		internal readonly Type DeclaringType;
		internal readonly JsString Name;
		internal readonly JsString? Symbol;
		internal readonly PropertyFlag Flags;

		private JsVisibleMember(Type declaringType, string name, string? symbol, PropertyFlag flags)
		{
			DeclaringType = declaringType;
			Name = new(name);
			Symbol = symbol == null ? null : new(symbol);
			Flags = flags;
		}

		internal abstract PropertyDescriptor Build(Engine engine, object? staticObject);
	}
}
