using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Data;
using Avalonia.Data.Converters;
using LanguageExt;
using SdmxDl.Browser.Models;
using SdmxDl.Client.Models;

namespace SdmxDl.Browser.Infrastructure.Converters;

internal class RequestMultiConverter : IMultiValueConverter
{
    public object? Convert(
        IList<object?> values,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        if (values?.Count != 3)
            throw new NotSupportedException();

        if (
            values[0] is not Option<SdmxWebSource> oSource
            || values[1] is not Option<DataFlow> oFlow
            || values[2] is not string key
        )
        {
            return BindingOperations.DoNothing;
        }

        return from source in oSource
            from flow in oFlow
            select new SeriesRequest
            {
                Source = source,
                Flow = flow,
                Key = key
            };
    }
}
