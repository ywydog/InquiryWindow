using System.Globalization;
using Avalonia.Data.Converters;

namespace InquiryWindow.Converters;

/// <summary>
/// 按钮启用转换器：当 <see cref="InquiryWindow.ViewModels.MultiButtonPromptViewModel.PressCountdownButton"/>
/// 为 null 时（即没有交互倒计时在进行），所有按钮都可用；
/// 当有正在倒计时的按钮时，禁用所有按钮，避免用户切换目标时混淆。
///
/// 输入：<see cref="InquiryWindow.Models.MultiButtonPromptButton"/>?（ViewModel.PressCountdownButton）。
/// 输出：bool（true=启用，false=禁用）。
/// </summary>
public class ButtonIsEnabledConverter : IValueConverter
{
    public static readonly ButtonIsEnabledConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // 没有正在倒计时的按钮 → 启用
        return value is null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
