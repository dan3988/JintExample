using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace JintTest
{
	[DebuggerDisplay("JsInvoker[{GetType().Name,nq}] {name.ToObject()} ({method})")]
	internal abstract class JsInvoker : FunctionInstance
	{
		private delegate JsValue FunctionCall(Engine engine, JsValue methodName, object? staticObject, JsValue thisObject, JsValue[] arguments);

		private abstract class DelegateInvoker<T> : JsInvoker
			where T : class
		{
			protected DelegateInvoker(Engine engine, JsString name, MethodInfo method, object? staticObject, JsNumber length) : base(engine, name, method, staticObject, length)
			{
			}

			public sealed override JsValue Call(JsValue thisObject, JsValue[] arguments)
			{
				if (staticObject != null)
					return CallImpl((T)staticObject, arguments);

				var obj = _engine.RequireThis<T>(name, thisObject);
				return CallImpl(obj, arguments); 
			}

			protected abstract JsValue CallImpl(T thisObject, JsValue[] arguments);
		}

		#region DelegateInvoker implementations

		private sealed class VarargsInvoker<T> : DelegateInvoker<T>
			where T : class
		{
			private readonly Func<T, JsValue[], JsValue> func;

			internal VarargsInvoker(Engine engine, JsString name, MethodInfo method, object? staticObject, Func<T, JsValue[], JsValue> func) : base(engine, name, method, staticObject, JsHelper.Zero)
			{
				this.func = func;
			}

			protected override JsValue CallImpl(T thisObject, JsValue[] arguments)
				=> func.Invoke(thisObject, arguments);
		}

		private sealed class VarargsVoidInvoker<T> : DelegateInvoker<T>
			where T : class
		{
			private readonly Action<T, JsValue[]> func;

			internal VarargsVoidInvoker(Engine engine, JsString name, MethodInfo method, object? staticObject, Action<T, JsValue[]> func) : base(engine, name, method, staticObject, JsHelper.Zero)
			{
				this.func = func;
			}

			protected override JsValue CallImpl(T thisObject, JsValue[] arguments)
			{
				func.Invoke(thisObject, arguments);
				return Undefined;
			}
		}

		private sealed class NoParameterVoidInvoker<T> : DelegateInvoker<T>
			where T : class
		{
			private readonly Action<T> func;

			internal NoParameterVoidInvoker(Engine engine, JsString name, MethodInfo method, object? staticObject, Action<T> func) : base(engine, name, method, staticObject, JsHelper.Zero)
			{
				this.func = func;
			}

			protected override JsValue CallImpl(T thisObject, JsValue[] arguments)
			{
				func.Invoke(thisObject);
				return Undefined;
			}
		}

		private sealed class NoParameterInvoker<T> : DelegateInvoker<T>
			where T : class
		{
			private readonly Func<T, JsValue> func;

			internal NoParameterInvoker(Engine engine, JsString name, MethodInfo method, object? staticObject, Func<T, JsValue> func) : base(engine, name, method, staticObject, JsHelper.Zero)
			{
				this.func = func;
			}

			protected override JsValue CallImpl(T thisObject, JsValue[] arguments)
				=> func.Invoke(thisObject);
		}

		private sealed class SingleParameterVoidInvoker<T> : DelegateInvoker<T>
			where T : class
		{
			private readonly Action<T, JsValue> func;

			internal SingleParameterVoidInvoker(Engine engine, JsString name, MethodInfo method, object? staticObject, Action<T, JsValue> func) : base(engine, name, method, staticObject, JsHelper.One)
			{
				this.func = func;
			}

			protected override JsValue CallImpl(T thisObject, JsValue[] arguments)
			{
				var param = arguments.At(0);
				func.Invoke(thisObject, param);
				return Undefined;
			}
		}

		#endregion

		private sealed class ExpressionInvoker : JsInvoker
		{
			private readonly Expression<FunctionCall> _expression;
			private readonly FunctionCall _function;

			internal ExpressionInvoker(Engine engine, JsString name, MethodInfo method, object? staticObject, JsValue length, Expression<FunctionCall> expression) : base(engine, name, method, staticObject, length)
			{
				_expression = expression;
				_function = expression.Compile();
			}

			public override JsValue Call(JsValue thisObject, JsValue[] arguments)
				=> _function.Invoke(_engine, name, staticObject, thisObject, arguments);
		}

		private const BindingFlags ctorFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
		private const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

		private static readonly Dictionary<Type, MethodInfo> delegateTypes = new();
		private static readonly Type jsvType = typeof(JsValue);

		private static readonly MethodInfo miGetParameter = typeof(JsInvoker).GetMethod(nameof(GetParameter), staticFlags);
		private static readonly MethodInfo miGetRestArray = typeof(JsInvoker).GetMethods(staticFlags).First(v => v.Name == nameof(GetRestArray));
		private static readonly MethodInfo miValidateThisObject = typeof(JsInvoker).GetMethods(staticFlags).First(v => v.Name == nameof(ValidateThisObject));
		
		public static JsInvoker CreateForGetter(Engine engine, Type declaringType, PropertyInfo property, JsString name, object? staticObject)
		{
			Debug.Assert(property.CanRead, "Property is write-only.");

			var type = property.PropertyType;
			if (!jsvType.IsAssignableFrom(type))
				throw new ArgumentException("Property type must extend Jint.Native.JsValue.", nameof(property));

			var funcType = typeof(Func<,>).MakeGenericType(declaringType, type);
			var func = Delegate.CreateDelegate(funcType, property.GetMethod);
			var invokerType = typeof(NoParameterInvoker<>).MakeGenericType(declaringType);
			return (JsInvoker)Activator.CreateInstance(invokerType, ctorFlags, System.Type.DefaultBinder, new object?[] { engine, name, property.GetMethod, staticObject, func }, null);
		}

		public static JsInvoker CreateForSetter(Engine engine, Type declaringType, PropertyInfo property, JsString name, object? staticObject)
		{
			Debug.Assert(property.CanWrite, "Property is read-only.");

			var type = property.PropertyType;
			if (type != jsvType)
				throw new ArgumentException("Property type must be Jint.Native.JsValue.", nameof(property));

			var funcType = typeof(Action<,>).MakeGenericType(declaringType, type);
			var func = Delegate.CreateDelegate(funcType, property.SetMethod);
			var invokerType = typeof(SingleParameterVoidInvoker<>).MakeGenericType(declaringType);
			return (JsInvoker)Activator.CreateInstance(invokerType, ctorFlags, System.Type.DefaultBinder, new object?[] { engine, name, property.SetMethod, staticObject, func }, null);
		}

		public static JsInvoker CreateForMethod(Engine engine, Type declaringType, MethodInfo method, JsString name, JsValue? length, object? staticObject)
		{
			var isVoid = method.ReturnType == typeof(void);
			if (!isVoid && !jsvType.IsAssignableFrom(method.ReturnType))
				throw new ArgumentException("Method return type must be void or extend Jint.Native.JsValue.", nameof(method));

			var parameters = method.GetParameters();
			if (parameters.Length == 0)
			{
				if (isVoid)
				{
					var funcType = typeof(Action<>).MakeGenericType(declaringType);
					var func = Delegate.CreateDelegate(funcType, method);
					var invokerType = typeof(NoParameterVoidInvoker<>).MakeGenericType(declaringType);
					return (JsInvoker)Activator.CreateInstance(invokerType, ctorFlags, System.Type.DefaultBinder, new object?[] { engine, name, method, staticObject, func }, null);
				}
				else
				{
					var funcType = typeof(Func<,>).MakeGenericType(declaringType, method.ReturnType);
					var func = Delegate.CreateDelegate(funcType, method);
					var invokerType = typeof(NoParameterInvoker<>).MakeGenericType(declaringType);
					return (JsInvoker)Activator.CreateInstance(invokerType, ctorFlags, System.Type.DefaultBinder, new object?[] { engine, name, method, staticObject, func }, null);
				}
			}
			else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(JsValue[]) && parameters[0].IsDefined(typeof(ParamArrayAttribute)))
			{
				if (isVoid)
				{
					var funcType = typeof(Action<,>).MakeGenericType(declaringType, typeof(JsValue[]));
					var func = Delegate.CreateDelegate(funcType, method);
					var invokerType = typeof(VarargsVoidInvoker<>).MakeGenericType(declaringType);
					return (JsInvoker)Activator.CreateInstance(invokerType, ctorFlags, System.Type.DefaultBinder, new object?[] { engine, name, method, staticObject, func }, null);
				}
				else
				{
					var funcType = typeof(Func<,,>).MakeGenericType(declaringType, typeof(JsValue[]), method.ReturnType);
					var func = Delegate.CreateDelegate(funcType, method);
					var invokerType = typeof(VarargsInvoker<>).MakeGenericType(declaringType);
					return (JsInvoker)Activator.CreateInstance(invokerType, ctorFlags, System.Type.DefaultBinder, new object?[] { engine, name, method, staticObject, func }, null);
				}
			}

			var expression = GenerateExpression(declaringType, method, parameters, isVoid, out var realLength);
			return new ExpressionInvoker(engine, name, method, staticObject, length ?? realLength, expression);
		}

		private static Expression<FunctionCall> GenerateExpression(Type declaringType, MethodInfo method, ParameterInfo[] parameters, bool isVoid, out int length)
		{
			var paramEngine = Expression.Parameter(typeof(Engine), "engine");
			var paramName = Expression.Parameter(typeof(JsValue), "methodName");
			var paramStatic = Expression.Parameter(typeof(object), "staticObject");
			var paramThis = Expression.Parameter(typeof(JsValue), "thisObject");
			var paramArgs = Expression.Parameter(typeof(JsValue[]), "arguments");

			var eLocals = new ParameterExpression[parameters.Length + 1];
			var eParams = new Expression[parameters.Length];
			var setters = new Expression[parameters.Length];

			var block = new List<Expression>(parameters.Length + 3);
			var thisLocal = Expression.Variable(declaringType, "@this");
			var checkMethod = miValidateThisObject.MakeGenericMethod(declaringType);
			var checkCall = Expression.Call(checkMethod, paramEngine, paramName, paramStatic, paramThis);

			block.Add(Expression.Assign(thisLocal, checkCall));
			eLocals[0] = thisLocal;
			length = 0;

			for (int i = 0; i < parameters.Length; )
			{
				var param = parameters[i];
				if (param.IsDefined(typeof(ParamArrayAttribute)))
				{
					var elementType = param.ParameterType.GetElementType();
					if (!jsvType.IsAssignableFrom(elementType))
						throw new ArgumentException($"The element type of parameter '{param.Name}' must be assignable from Jint.Native.JsValue[].", nameof(method));

					var startIndex = Expression.Constant(i, typeof(int));
					var generic = miGetRestArray.MakeGenericMethod(elementType);
					var call = Expression.Call(generic, paramEngine, paramArgs, startIndex);
					var var = Expression.Variable(param.ParameterType, "arg_rest");
					setters[i] = Expression.Assign(var, call);
					eParams[i] = var;
					eLocals[++i] = var;
					break;
				}
				else
				{
					if (!jsvType.IsAssignableFrom(param.ParameterType))
						throw new ArgumentException($"The type of parameter '{param.Name}' must be assignable from Jint.Native.JsValue.", nameof(method));

					var index = Expression.Constant(i, typeof(int));
					var convertType = Expression.Constant(param.ParameterType);
					var call = Expression.Call(miGetParameter, paramEngine, paramArgs, index, convertType);
					var conv = Expression.Convert(call, param.ParameterType);
					var var = Expression.Variable(param.ParameterType, "arg_" + i);
					setters[i] = Expression.Assign(var, conv);
					eParams[i] = var;
					eLocals[++i] = var;

					if (!param.HasDefaultValue)
						length++;
				}
			}

			var methodCall = Expression.Call(thisLocal, method, eParams);

			block.AddRange(setters);
			block.Add(methodCall);

			if (isVoid)
				block.Add(Expression.Constant(Undefined));

			var body = Expression.Block(eLocals, block);
			return Expression.Lambda<FunctionCall>(body, paramEngine, paramName, paramStatic, paramThis, paramArgs);
		}

		private static T? ValidateThisObject<T>(Engine engine, JsValue methodName, object staticObject, JsValue thisObject)
			where T : class
		{
			if (staticObject != null)
				return (T)staticObject;

			if (thisObject is T t)
				return t;

			throw engine.TypeError($"{methodName} called on incompatible type.");
		}

		private static T[] GetRestArray<T>(Engine engine, JsValue[] args, int startIndex)
			where T : JsValue
		{
			var length = args.Length - startIndex;
			if (length <= 0)
				return Array.Empty<T>();

			var result = new T[length];
			for (var i = 0; i < length; i++)
				result[i] = (T)GetParameter(engine, args, startIndex++, typeof(T));

			return result;
		}

		private static JsValue GetParameter(Engine engine, JsValue[] args, int index, Type paramType)
		{
			var arg = args.At(index);
			if (!paramType.IsAssignableFrom(arg.GetType()))
			{
				if (paramType == typeof(JsString))
					return TypeConverter.ToString(arg);
				if (paramType == typeof(JsNumber))
					return TypeConverter.ToNumber(arg);
				if (paramType == typeof(JsBoolean))
					return TypeConverter.ToBoolean(arg);

				var typeName = JsHelper.Typeof(paramType, true);
				throw engine.TypeError($"Argument {index + 1} is not {(typeName[0].IsVowel() ? "an" : "a")} {typeName}");
			}

			return arg;
		}

		private static T CreateDelegate<T>(MethodInfo method) where T : Delegate
			=> (T)Delegate.CreateDelegate(typeof(T), method);

		private readonly string name;
		private readonly MethodInfo method;
		private readonly object? staticObject;

		private ObjectInstance _prototype;

		private JsInvoker(Engine engine, JsString name, MethodInfo method, object? staticObject, JsValue length) : base(engine, engine.Realm, name)
		{
			this.name = name.ToString();
			this.method = method;
			this.staticObject = staticObject;
			_prototype = engine.Realm.Intrinsics.Function.PrototypeObject;
			_length = JsHelper.CreateDescriptor(length, PropertyFlag.Configurable);
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
