using Jint.Native;

namespace JintTest
{
	internal static class CommonNames
	{
		public static readonly JsString Name = new("name");
		public static readonly JsString Value = new("value");
		public static readonly JsString Next = new("next");
		public static readonly JsString Done = new("done");
		public static readonly JsString Length = new("length");
		public static readonly JsString Message = new("message");
		public static readonly JsString Stack = new("stack");
		public static new readonly JsString ToString = new("toString");
		public static readonly JsString ToJson = new("toJSON");
		public static readonly JsString Prototype = new("prototype");
		public static readonly JsString Constructor = new("constructor");

		public static readonly JsString Undefined = new("undefined");
		public static readonly JsString Object = new("object");
		public static readonly JsString Boolean = new("boolean");
		public static readonly JsString Number = new("number");
		public static readonly JsString Bigint = new("bigint");
		public static readonly JsString String = new("string");
		public static readonly JsString Symbol = new("symbol");
		public static readonly JsString Function = new("function");
	}
}
