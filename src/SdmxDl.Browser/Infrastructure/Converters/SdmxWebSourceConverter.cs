using System;
using System.Globalization;
using Avalonia.Data.Converters;
using SdmxDl.Client.Models;

namespace SdmxDl.Browser.Infrastructure.Converters;

public class SdmxWebSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            SdmxWebSource source => source.GetDescription(),
            _ => string.Empty,
        };
    }

    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        throw new NotImplementedException();
    }
}
