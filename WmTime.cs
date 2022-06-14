using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;

namespace JintTest
{
	internal sealed class WmTimeConstructor : AbstractObjectConstructor
	{
		public new static readonly JsString Name = new("WmTime");

		public WmTimeConstructor(FormScriptingEngine engine, Realm realm, FunctionPrototype prototype) : base(engine, realm, typeof(WmTimeInstance), Name, prototype)
		{
		}

		public WmTimeInstance Construct(TimeSpan? value)
			=> new(this, value);

		protected override AbstractObjectInstance Construct(JsValue[] arguments)
		{
			if (arguments.Length == 0)
				return new WmTimeInstance(this, DateTime.Now.TimeOfDay);

			if (arguments.Length == 1)
			{
				var days = TypeConverter.ToNumber(arguments[0]);
				var ticks = (long)(Math.Clamp(days, 0, 1) * TimeSpan.TicksPerDay);
				return new WmTimeInstance(this, TimeSpan.FromTicks(ticks));
			}
			else
			{
				var hours = TypeConverter.ToNumber(arguments[0]);
				var minutes = TypeConverter.ToNumber(arguments[1]);
				if (double.IsNaN(hours) || double.IsNaN(minutes))
				{
					return new WmTimeInstance(this, null);
				}
				else
				{
					var ts = TimeSpan.FromTicks((long)((hours * TimeSpan.TicksPerHour) + (minutes * TimeSpan.TicksPerMinute)));
					return new WmTimeInstance(this, ts);
				}
			}
		}
	}

	internal class WmTimeInstance : AbstractObjectInstance, IPrimitiveLike
	{
		internal static readonly JsString Invalid = new("Invalid WmTime");

		internal readonly TimeSpan _value;
		internal readonly bool _invalid;

		[JsProperty]
		public JsValue Hours { get; }

		[JsProperty]
		public JsValue Minutes { get; }

		JsString IPrimitiveLike.TypeName => WmTimeConstructor.Name;

		public new WmTimeConstructor Constructor => (WmTimeConstructor)base.Constructor;

		internal WmTimeInstance(WmTimeConstructor constructor, TimeSpan value) : base(constructor)
		{
			_value = value;
			_invalid = false;
			Hours = value.Hours;
			Minutes = value.Minutes;
		}

		internal WmTimeInstance(WmTimeConstructor constructor, TimeSpan? value) : base(constructor)
		{
			if (value.HasValue)
			{
				_value = value.GetValueOrDefault();
				Hours = _value.Hours;
				Minutes = _value.Minutes;
			}
			else
			{
				_invalid = true;
				Hours = JsHelper.NaN;
				Minutes = JsHelper.NaN;
			}
		}

		[JsMethod(Symbol = "toPrimitive")]
		public JsValue ToPrimitive(JsValue name)
		{
			string got;

			if (!name.IsString())
			{
				got = JsHelper.Typeof(name).ToString();
			}
			else
			{
				switch (got = name.ToString())
				{
					case "default":
					case "string":
						return ToJsString();
					case "number":
						return _invalid ? JsHelper.NaN : (_value.Ticks - Number.ZeroDate.Ticks) / (double)TimeSpan.TicksPerDay;
				}

				got = "string " + got;
			}

			throw _engine.TypeError("expected expected \"string\", \"number\", or \"default\", got " + got);
		}

		[JsMethod]
		public WmTimeInstance AddHours(JsNumber value)
		{
			var hours = TypeConverter.ToNumber(value);
			if (_invalid || double.IsNaN(hours))
				return Constructor.Construct(null);

			return Constructor.Construct(_value + TimeSpan.FromHours(hours));
		}

		[JsMethod]
		public WmTimeInstance AddMinutes(JsNumber value)
		{
			var minutes = TypeConverter.ToNumber(value);
			if (_invalid || double.IsNaN(minutes))
				return Constructor.Construct(null);

			return Constructor.Construct(_value + TimeSpan.FromMinutes(minutes));
		}

		[JsMethod]
		public JsValue ToJSON()
			=> ToJsString();

		[JsMethod]
		public JsValue ToLocaleString()
			=> _invalid ? Invalid : new DateTime(_value.Ticks).ToShortTimeString();

		[JsMethod(Name = "toString")]
		public JsValue ToJsString()
			=> _invalid ? Invalid : ((TimeSpan)_value).ToString("hh\\:mm");

		[JsMethod(Name = "equals")]
		public JsValue JsEquals(JsValue other)
		{
			if (this == other)
				return true;

			if (_invalid)
				return false;

			if (other is not WmTimeInstance d || d._invalid)
				return false;

			return _value == d._value;
		}
	}
}
