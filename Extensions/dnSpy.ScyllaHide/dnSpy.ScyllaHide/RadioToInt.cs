using System;
using System.Windows.Data;

namespace dnSpy.ScyllaHide {
	public class RadioToInt :IValueConverter
	{

		public object Convert(object value, Type targetType, object parameter,
				System.Globalization.CultureInfo culture)
			{
				return value.Equals(parameter);
			}

			public object ConvertBack(object value, Type targetType, object parameter,
				System.Globalization.CultureInfo culture)
			{
				return value.Equals(true) ? parameter : Binding.DoNothing;
			}
		}
}
