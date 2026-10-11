using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace InquiryWindow.Converters;

/// <summary>
/// 把可空画刷绑定到 <c>Background</c> 时使用的小工具：值为 <c>null</c> 时返回
/// <see cref="AvaloniaProperty.UnsetValue"/>，表示「不设置本地值」，从而回落到主题 / 样式
/// 提供的背景；值为非空画刷时原样返回。
///
/// 背景：Avalonia 中直接 <c>Background="{Binding X}"</c> 且 X 为 null 时，会把 null 当作
/// 「本地值」写入并覆盖主题 / accent 样式提供的背景（导致默认按钮变透明）。返回 UnsetValue
/// 则可避免该行为，让默认 / 高亮模式按钮完整跟随 ClassIsland 主题，仅自定义模式赋予画刷。
/// </summary>
public class NullToUnsetConverter : IValueConverter
{
    public static readonly NullToUnsetConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is null ? AvaloniaProperty.UnsetValue : value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}