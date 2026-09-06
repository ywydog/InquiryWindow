using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace InquiryWindow.Converters;

/// <summary>
/// 判断当前背景的 <see cref="Stretch"/> 是否为非 Tile 模式（即不是 <see cref="Stretch.None"/>）。
/// 与 <see cref="StretchIsTileConverter"/> 互斥使用。
///
/// 输入：<see cref="Stretch"/>。
/// 输出：bool（true=非 Tile 模式，false=Tile 模式）。
/// </summary>
public class StretchIsNotTileConverter : IValueConverter
{
    public static readonly StretchIsNotTileConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is Stretch s && s != Stretch.None;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
