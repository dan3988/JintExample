using System;

using Jint.Runtime.Descriptors;

namespace JintTest
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
	internal sealed class JsMethodAttribute : JsMemberAttribute
	{
		private int? _length;
		public int Length
		{
			get => _length.GetValueOrDefault();
			set => _length = value;
		}

		public JsMethodAttribute() : base(JsHelper.DefaultFunctionFlags)
		{
		}

		public JsMethodAttribute(PropertyFlag flags) : base(flags)
		{
		}

		internal int? GetLengthValue()
			=> _length;
	}
}
