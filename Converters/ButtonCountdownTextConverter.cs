using System.Globalization;
using Avalonia.Data.Converters;

namespace InquiryWindow.Converters;

/// <summary>
/// 按钮文字转换器：实现"按下按钮后显示倒计时（Ns）"的 UI 效果，
/// UI 表现参考 SuperAutoIsland 的 YesNo 对话框
/// （<c>RuleHandlerService.ShowDialogAsync</c> 中
/// <c>defaultButton.Text = $"{defaultText} ({remainingTime:0}s)"</c>）。
///
/// 输入：
/// <list type="number">
///   <item>MultiButtonPromptButton（DataTemplate 内的当前按钮）</item>
///   <item>MultiButtonPromptButton?（ViewModel.PressCountdownButton，正在倒计时的按钮引用）</item>
///   <item>double（ViewModel.PressCountdownRemaining，剩余秒数）</item>
/// </list>
/// 输出：按钮要显示的文字。
/// </summary>
public class ButtonCountdownTextConverter : IMultiValueConverter
{
    public static readonly ButtonCountdownTextConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 3) return null;
        if (values[0] is not Models.MultiButtonPromptButton button) return null;
        var countdownButton = values[1] as Models.MultiButtonPromptButton;
        var remaining = values[2] is double r ? r : 0.0;

        // 是当前正在倒计时的按钮：在名字后追加 (Ns)
        if (countdownButton is not null && ReferenceEquals(countdownButton, button) && remaining > 0)
        {
            return $"{button.Name} ({remaining:0}s)";
        }
        return button.Name;
    }
}
