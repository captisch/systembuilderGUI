using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace systembuilderGUI.Converters;

public sealed class StringToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b) return b;
        if (value is string s && bool.TryParse(s, out var parsed)) return parsed;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? b.ToString() : "False";
}


// Won't be in use, but leave for reference
public sealed class ScientificToIntegerStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() ?? string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value?.ToString()?.Trim();
        if (string.IsNullOrEmpty(s))
            return new BindingNotification(new InvalidOperationException("Bitte eine Zahl eingeben."), BindingErrorType.Error);

        // Erst InvariantCulture (Punkt), dann CurrentCulture (Komma) versuchen
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ||
            double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out d))
        {
            // Auf ganze Zahl runden (AwayFromZero) und als string zurückgeben
            var rounded = Math.Round(d, 0, MidpointRounding.AwayFromZero);
            // Vorsicht bei sehr großen Werten
            if (rounded < long.MinValue || rounded > long.MaxValue)
                return new BindingNotification(new InvalidOperationException("Wert außerhalb des gültigen Bereichs."), BindingErrorType.Error);

            var asLong = System.Convert.ToInt64(rounded);
            
            return asLong.ToString(CultureInfo.InvariantCulture);
        }

        return new BindingNotification(new InvalidOperationException("Ungültiges Zahlenformat."), BindingErrorType.Error);
    }
}
