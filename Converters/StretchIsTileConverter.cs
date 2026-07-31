using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace InquiryWindow.Converters;

/// <summary>
/// 判断当前背景的 <see cref="Stretch"/> 是否为 Tile 模式（即 <see cref="Stretch.None"/>）。
/// 运行时弹窗中据此切换 "普通 Image" 和 "TileBrush Image" 两个 Image 元素。
///
/// 输入：<see cref="Stretch"/>。
/// 输出：bool（true=Tile 模式，false=其他模式）。
/// </summary>
public class StretchIsTileConverter : IValueConverter
{
    public static readonly StretchIsTileConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Stretch s && s == Stretch.None;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
