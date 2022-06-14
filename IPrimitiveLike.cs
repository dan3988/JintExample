using Jint.Native;

namespace JintTest
{
	/// <summary>
	/// Values of types that implement this type will be logged as <c>$"[{TypeName}} {ToJSON()}"</c> when serialized by <see cref="ObjectWriter"/> and <see cref="ObjectWriterSettings.IgnoreToJson"/> is <c>true</c>.
	/// </summary>
	internal interface IPrimitiveLike
	{
		JsString TypeName { get; }

		JsValue ToJSON();
	}
}
