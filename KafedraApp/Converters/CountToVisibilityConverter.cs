﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace KafedraApp.Converters
{
	public class CountToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (double.TryParse(value.ToString(), out double count) && count != 0)
				return Visibility.Visible;

			return Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}