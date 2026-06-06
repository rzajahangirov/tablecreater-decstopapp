using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TableCreater.WPF.Converters;

/// <summary>
/// Converts a boolean value to Visibility.
/// true → Visible, false → Collapsed.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}

/// <summary>
/// Converts a null check to boolean.
/// non-null → true, null → false.
/// Used for enabling/disabling buttons based on selection.
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a null/empty string to Visibility.
/// non-null/non-empty → Visible, null/empty → Collapsed.
/// Used for showing/hiding error messages.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
            return string.IsNullOrEmpty(str) ? Visibility.Collapsed : Visibility.Visible;

        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts enum values to uppercase display strings.
/// Usd → "USD", Rub → "RUB", Truck → "TRUCK", Ship → "SHIP".
/// </summary>
public class EnumToUpperConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString()?.ToUpperInvariant() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && targetType.IsEnum)
        {
            foreach (var enumValue in Enum.GetValues(targetType))
            {
                if (string.Equals(enumValue.ToString(), str, StringComparison.OrdinalIgnoreCase))
                    return enumValue;
            }
        }
        return value!;
    }
}
