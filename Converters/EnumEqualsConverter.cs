using System.Globalization;
using Avalonia.Data.Converters;

namespace InquiryWindow.Converters;

/// <summary>
/// 枚举值相等性比较转换器。XAML 中通过 <c>ConverterParameter</c> 指定要比较的枚举名
/// （如 <c>ConverterParameter=Custom</c>），与 <c>value</c> 转字符串后做不区分大小写比较。
///
/// 输入：枚举值（任意 enum）。
/// 输出：bool（value.ToString() == ConverterParameter）。
///
/// 用法：
/// <code>
/// IsVisible="{Binding Mode, Converter={x:Static local:EnumEqualsConverter.Instance}, ConverterParameter=Custom}"
/// </code>
/// </summary>
public class EnumEqualsConverter : IValueConverter
{
    public static readonly EnumEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 仅用于 IsVisible 等单向绑定，不实现反向
        throw new NotSupportedException();
    }
}
