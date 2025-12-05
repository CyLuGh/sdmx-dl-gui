using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using SdmxDl.Browser.Models;

namespace SdmxDl.Browser.Infrastructure.Converters;

internal class SeriesRequestMenuItemConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is IEnumerable<SeriesRequest> requests)
        {
            return requests.Select(req =>
            {
                var mi = new MenuItem { Header = req.Title };
                return mi;
            });
        }

        return BindingOperations.DoNothing;
    }

    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => BindingOperations.DoNothing;
}
