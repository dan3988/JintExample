// See https://aka.ms/new-console-template for more information
using Jint.Native;
using Jint.Native.Array;

using JintTest;

var engine = new FormScriptingEngine();
var today = engine.Evaluate("let today = new WmDate();today;");
var now = engine.Evaluate("let now = new WmTime();now;");

var dateProto = engine.Evaluate("Object.getPrototypeOf(today)");
var timeProto = engine.Evaluate("Object.getPrototypeOf(now)");

var keys1 = (ArrayInstance)engine.Realm.Intrinsics.Object.GetOwnPropertyNames(JsValue.Undefined, new[] { dateProto });
var keys2 = (ArrayInstance)engine.Realm.Intrinsics.Object.GetOwnPropertyNames(JsValue.Undefined, new[] { timeProto });

Console.WriteLine("{0} {1}", today, now);
Console.WriteLine(engine.WmDate.PrototypeObject == dateProto);
Console.WriteLine(engine.WmTime.PrototypeObject == timeProto);
Console.WriteLine(string.Join(", ", keys1));
Console.WriteLine(string.Join(", ", keys2));