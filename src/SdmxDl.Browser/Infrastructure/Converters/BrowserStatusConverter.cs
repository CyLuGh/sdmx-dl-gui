using System;
using System.Globalization;
using Avalonia.Data.Converters;
using SdmxDl.Browser.Models;
using SdmxDl.Client.Models;

namespace SdmxDl.Browser.Infrastructure.Converters;

public class BrowserStatusConverter : IValueConverter
{
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) =>
        value switch
        {
            BrowserStatus bStatus => bStatus switch
            {
                BrowserStatus.Offline => "OFFLINE",
                BrowserStatus.Connected => "CONNECTED",
                BrowserStatus.Hosting => "HOSTING",
                _ => string.Empty,
            },
            _ => string.Empty,
        };

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
