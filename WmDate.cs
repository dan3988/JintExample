using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;

namespace JintTest
{
	internal sealed class WmDateConstructor : AbstractObjectConstructor
	{
		public new static readonly JsString Name = new("WmDate");

		public WmDateConstructor(FormScriptingEngine engine, Realm realm, FunctionPrototype prototype) : base(engine, realm, typeof(WmDateInstance), Name, prototype)
		{
		}

		public WmDateInstance Construct(DateTime? value)
			=> new(this, value);

		protected override AbstractObjectInstance Construct(JsValue[] arguments)
		{
			if (arguments.Length == 0)
				return new WmDateInstance(this, DateTime.Today);

			if (arguments.Length == 1)
			{
				var days = TypeConverter.ToNumber(arguments[0]);
				var ticks = (long)(days * TimeSpan.TicksPerDay);
				return new WmDateInstance(this, new DateTime(ticks));
			}
			else
			{
				var years = TypeConverter.ToNumber(arguments[0]);
				var months = TypeConverter.ToNumber(arguments[1]) - 1;
				var days = arguments.Length == 2 ? 0 : TypeConverter.ToNumber(arguments[2]) - 1;
				if (double.IsNaN(years) || double.IsNaN(months) || double.IsNaN(days))
				{
					return new WmDateInstance(this, null);
				}
				else
				{
					var dt = new DateTime((int)years, 1, 1);
					dt = dt.AddMonths((int)months);
					dt = dt.AddDays((int)days);

					return new WmDateInstance(this, dt);
				}
			}
		}
	}

	internal class WmDateInstance : AbstractObjectInstance, IPrimitiveLike
	{
		internal static readonly JsString Invalid = new("Invalid WmDate");

		internal readonly DateTime _value;
		internal readonly bool _invalid;

		[JsProperty]
		public JsValue Year { get; }

		[JsProperty]
		public JsValue Month { get; }

		[JsProperty]
		public JsValue Day { get; }

		JsString IPrimitiveLike.TypeName => WmDateConstructor.Name;

		public new WmDateConstructor Constructor => (WmDateConstructor)base.Constructor;

		internal WmDateInstance(WmDateConstructor constructor, DateTime value) : base(constructor)
		{
			_value = value;
			_invalid = false;
			Year = value.Year;
			Month = value.Month;
			Day = value.Day;
		}

		internal WmDateInstance(WmDateConstructor constructor, DateTime? value) : base(constructor)
		{

			if (value.HasValue)
			{
				_value = value.GetValueOrDefault();
				Year = _value.Year;
				Month = _value.Month;
				Day = _value.Day;
			}
			else
			{
				_invalid = true;
				Year = JsHelper.NaN;
				Month = JsHelper.NaN;
				Day = JsHelper.NaN;
			}
		}

		[JsMethod]
		public WmDateInstance AddYears(JsNumber value)
		{
			var years = TypeConverter.ToNumber(value);
			if (_invalid || double.IsNaN(years))
				return Constructor.Construct(null);

			return Constructor.Construct(_value.AddYears((int)years));
		}

		[JsMethod]
		public WmDateInstance AddMonths(JsNumber value)
		{
			var months = TypeConverter.ToNumber(value);
			if (_invalid || double.IsNaN(months))
				return Constructor.Construct(null);

			return Constructor.Construct(_value.AddMonths((int)months));
		}

		[JsMethod]
		public WmDateInstance AddDays(JsNumber value)
		{
			var days = TypeConverter.ToNumber(value);
			if (_invalid || double.IsNaN(days))
				return Constructor.Construct(null);

			return Constructor.Construct(_value.AddDays(Math.Truncate(days)));
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
		public JsValue ToJSON()
			=> ToJsString();

		[JsMethod]
		public JsValue ToLocaleString()
			=> _invalid ? Invalid : ((DateTime)_value).ToShortDateString();

		[JsMethod(Name = "toString")]
		public JsValue ToJsString()
			=> _invalid ? Invalid : ((DateTime)_value).ToString("yyyy-MM-dd");

		[JsMethod(Name = "equals")]
		public JsValue JsEquals(JsValue other)
		{
			if (this == other)
				return true;

			if (_invalid)
				return false;

			if (other is not WmDateInstance d || d._invalid)
				return false;

			return _value == d._value;
		}
	}
}
