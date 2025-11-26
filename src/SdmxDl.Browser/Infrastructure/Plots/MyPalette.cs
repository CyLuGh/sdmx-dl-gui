using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScottPlot;

namespace SdmxDl.Browser.Infrastructure.Plots;

public class MyPalette : IPalette
{
    public Color[] Colors { get; } =
        [
            //new(
            //    192,
            //    209,
            //    217
            //) /* Gray */
            //,
            new(
                0,
                135,
                205
            ) /* Blue */
            ,
            new(
                255,
                203,
                5
            ) /* Yellow */
            ,
            new(
                136,
                192,
                61
            ) /* Green (bright) */
            ,
            new(
                243,
                112,
                33
            ) /* Orange */
            ,
            new(
                165,
                176,
                208
            ) /* Gray (darker) */
            ,
            new(
                226,
                16,
                115
            ) /* Pink */
            ,
            new(0, 175, 194) /* Blue-Teal */
        ];

    public string Name => "MyPalette";

    public string Description => "Palette";

    public Color GetColor(int index)
    {
        var i = index % Colors.Length;
        return Colors[i];
    }
}
