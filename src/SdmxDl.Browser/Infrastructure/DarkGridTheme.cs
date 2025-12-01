using HierarchyGrid.Definitions;

namespace SdmxDl.Browser.Infrastructure;

internal class DarkGridTheme : ITheme
{
    public static DarkGridTheme Instance { get; } = new();

    public ThemeColor BackgroundColor { get; } = new(255, 51, 51, 51);

    public ThemeColor ForegroundColor { get; } = new("ebebeb");

    public ThemeColor BorderColor { get; } = new(255, 120, 120, 120);

    public ThemeColor SelectionBorderColor { get; } = new(255, 25, 158, 241);

    public float SelectionBorderThickness { get; } = 2f;

    public ThemeColor HeaderBackgroundColor { get; } = new(255, 32, 36, 44);

    public ThemeColor HeaderForegroundColor { get; } = new("ebebeb");

    public ThemeColor HoverBackgroundColor { get; } = new(255, 235, 243, 249);

    public ThemeColor HoverForegroundColor { get; } = new(255, 34, 34, 34);

    public ThemeColor HoverHeaderBackgroundColor { get; } = new(255, 223, 239, 249);

    public ThemeColor HoverHeaderForegroundColor { get; } = new(255, 25, 158, 241);

    public ThemeColor HighlightBackgroundColor { get; } = ThemeColors.LightBlue;

    public ThemeColor HighlightForegroundColor { get; } = ThemeColors.Black;

    public ThemeColor HighlightHeaderBackgroundColor { get; } = ThemeColors.LightBlue;

    public ThemeColor HighlightHeaderForegroundColor { get; } = ThemeColors.Black;

    public ThemeColor ReadOnlyBackgroundColor { get; } = ThemeColors.LightGray;

    public ThemeColor ReadOnlyForegroundColor { get; } = ThemeColors.DarkSlateGray;

    public ThemeColor ComputedBackgroundColor { get; } = new("d8e6e0");

    public ThemeColor ComputedForegroundColor { get; } = new("004a8f");

    public ThemeColor RemarkBackgroundColor { get; } = new ThemeColor("0092b6");

    public ThemeColor RemarkForegroundColor { get; } = ThemeColors.White;

    public ThemeColor WarningBackgroundColor { get; } = new("#ffcb05");

    public ThemeColor WarningForegroundColor { get; } = ThemeColors.Black;

    public ThemeColor ErrorBackgroundColor { get; } = new("b80c4b");

    public ThemeColor ErrorForegroundColor { get; } = ThemeColors.White;

    public ThemeColor EmptyBackgroundColor { get; } = ThemeColors.LightGray;
}
