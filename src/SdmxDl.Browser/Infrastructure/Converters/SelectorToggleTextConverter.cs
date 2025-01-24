using System;
using System.Globalization;
using Avalonia.Data.Converters;
using LanguageExt;
using SdmxDl.Client.Models;

namespace SdmxDl.Browser.Infrastructure.Converters;

public class SelectorToggleTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            Option<SdmxWebSource> source => source.Some(s => s.Id).None(() => "SELECT SOURCE"),
            Option<DataFlow> dataFlow => dataFlow
                .Some(d => d.Description)
                .None(() => "SELECT DATAFLOW"),
            _ => "?",
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
