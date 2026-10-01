using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TableCreater.WPF.Enums;

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
/// Usd → "USD", Rub → "RUB", Truck → "TRUCK", Wagon → "WAGON".
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

/// <summary>
/// Converts decimal amount to SolidColorBrush:
/// Positive (> 0) → Dark Green, Negative (&lt; 0) → Crimson, Zero → Gray.
/// </summary>
public class ProfitLossToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal dec)
        {
            if (dec > 0) return System.Windows.Media.Brushes.DarkGreen;
            if (dec < 0) return System.Windows.Media.Brushes.Crimson;
            return System.Windows.Media.Brushes.Gray;
        }
        return System.Windows.Media.Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ShipmentStatus to a modern background brush for badges.
/// Pending → Light Amber, Loaded → Light Blue, InTransit → Light Purple, Delivered → Light Green.
/// </summary>
public class ShipmentStatusToBackgroundConverter : IValueConverter
{
    private static readonly System.Windows.Media.Brush PendingBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 248, 225));
    private static readonly System.Windows.Media.Brush LoadedBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(227, 242, 253));
    private static readonly System.Windows.Media.Brush InTransitBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(237, 231, 246));
    private static readonly System.Windows.Media.Brush DeliveredBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 245, 233));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ShipmentStatus status)
        {
            return status switch
            {
                ShipmentStatus.Pending => PendingBg,
                ShipmentStatus.Loaded => LoadedBg,
                ShipmentStatus.InTransit => InTransitBg,
                ShipmentStatus.Delivered => DeliveredBg,
                _ => PendingBg
            };
        }
        return PendingBg;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ShipmentStatus to a text foreground brush for badges.
/// </summary>
public class ShipmentStatusToForegroundConverter : IValueConverter
{
    private static readonly System.Windows.Media.Brush PendingFg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 127, 23));
    private static readonly System.Windows.Media.Brush LoadedFg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(25, 118, 210));
    private static readonly System.Windows.Media.Brush InTransitFg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(94, 53, 177));
    private static readonly System.Windows.Media.Brush DeliveredFg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 125, 50));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ShipmentStatus status)
        {
            return status switch
            {
                ShipmentStatus.Pending => PendingFg,
                ShipmentStatus.Loaded => LoadedFg,
                ShipmentStatus.InTransit => InTransitFg,
                ShipmentStatus.Delivered => DeliveredFg,
                _ => PendingFg
            };
        }
        return PendingFg;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ShipmentStatus to friendly Azerbaijani display text.
/// </summary>
public class ShipmentStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ShipmentStatus status)
        {
            return status switch
            {
                ShipmentStatus.Pending => "Gözləmədə",
                ShipmentStatus.Loaded => "Yükləndi",
                ShipmentStatus.InTransit => "Yoldadır",
                ShipmentStatus.Delivered => "Çatdı",
                _ => status.ToString()
            };
        }
        return "Gözləmədə";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ShipmentStatus to Segoe MDL2 Assets glyph code.
/// </summary>
public class ShipmentStatusToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ShipmentStatus status)
        {
            return status switch
            {
                ShipmentStatus.Pending => "\uE823",   // History/Timer
                ShipmentStatus.Loaded => "\uE7B8",    // Package / Archive
                ShipmentStatus.InTransit => "\uE7EC", // Car / Vehicle / Transit
                ShipmentStatus.Delivered => "\uE73E", // Checkmark / Delivered
                _ => "\uE823"
            };
        }
        return "\uE823";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Compares value against converter parameter string; returns Visible if equal, Collapsed if not.
/// </summary>
public class EnumEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return Visibility.Collapsed;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Compares value against converter parameter string for RadioButton two-way binding.
/// </summary>
public class EnumEqualsToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is string str && targetType.IsEnum)
        {
            return Enum.Parse(targetType, str, true);
        }
        return System.Windows.Data.Binding.DoNothing;
    }
}
