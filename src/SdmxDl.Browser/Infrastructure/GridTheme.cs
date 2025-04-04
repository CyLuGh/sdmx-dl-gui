using System.Runtime.InteropServices;
using HierarchyGrid.Definitions;

namespace SdmxDl.Browser.Infrastructure;

public class GridTheme : ITheme
{
    public static GridTheme Instance { get; } = new();

    public ThemeColor BackgroundColor =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ThemeColors.White : new(0, 0, 0, 0);
    public ThemeColor ForegroundColor => ThemeColors.Black;
    public ThemeColor BorderColor => ThemeColors.SlateGray;
    public ThemeColor SelectionBorderColor => ThemeColors.SlateGray;
    public float SelectionBorderThickness => 2f;
    public ThemeColor HeaderBackgroundColor => ThemeColors.LightGray;
    public ThemeColor HeaderForegroundColor => ThemeColors.Black;
    public ThemeColor HoverBackgroundColor => new("c8d8f7");
    public ThemeColor HoverForegroundColor => ThemeColors.Black;
    public ThemeColor HoverHeaderBackgroundColor => new("0a59f7");
    public ThemeColor HoverHeaderForegroundColor => ThemeColors.White;
    public ThemeColor HighlightBackgroundColor => ThemeColors.LightGray;
    public ThemeColor HighlightForegroundColor => ThemeColors.Black;
    public ThemeColor HighlightHeaderBackgroundColor => ThemeColors.LightGray;
    public ThemeColor HighlightHeaderForegroundColor => ThemeColors.Black;
    public ThemeColor ReadOnlyBackgroundColor => ThemeColors.LightGray;
    public ThemeColor ReadOnlyForegroundColor => ThemeColors.Black;
    public ThemeColor ComputedBackgroundColor => ThemeColors.LightGray;
    public ThemeColor ComputedForegroundColor => ThemeColors.Black;
    public ThemeColor RemarkBackgroundColor => ThemeColors.LightGray;
    public ThemeColor RemarkForegroundColor => ThemeColors.Black;
    public ThemeColor WarningBackgroundColor => ThemeColors.LightGray;
    public ThemeColor WarningForegroundColor => ThemeColors.Black;
    public ThemeColor ErrorBackgroundColor => ThemeColors.LightGray;
    public ThemeColor ErrorForegroundColor => ThemeColors.Black;
    public ThemeColor EmptyBackgroundColor => ThemeColors.LightGray;
}
