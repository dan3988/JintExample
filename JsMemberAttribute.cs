using System;

using Jint.Runtime.Descriptors;

namespace JintTest
{
	internal abstract class JsMemberAttribute : Attribute
	{
		public string? Name { get; set; }

		public string? Symbol { get; set; }

		public PropertyFlag Flags { get; }

		protected JsMemberAttribute(PropertyFlag flags)
		{
			Flags = flags;
		}
	}
}
