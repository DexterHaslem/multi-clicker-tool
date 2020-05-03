using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace multi_clicker_tool
{
	public class RepeatOptionRadioConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			var v = (ClickRepeatType)value;
			
			return v.ToString().Equals(parameter);
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (value is null)
			{
				return ClickRepeatType.Unknown;
			}
			return Enum.Parse(typeof(ClickRepeatType), parameter as string);
		}
	}
}
