using Jint.Runtime.Descriptors;

namespace JintTest
{
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	internal sealed class JsPropertyAttribute : JsMemberAttribute
	{
		public JsPropertyAttribute() : base(JsHelper.DefaultPropertyFlags)
		{
		}

		public JsPropertyAttribute(PropertyFlag flags) : base(flags)
		{
		}
	}
}
