using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using InquiryWindow.Models;
using InquiryWindow.ViewModels;
using InquiryWindow.Views;

namespace InquiryWindow.Actions;

/// <summary>
/// 多按钮询问：弹出一个 N 按钮窗口，按下任一按钮后按顺序执行该按钮的 Action 链。
/// </summary>
[ActionInfo("InquiryWindow.MultiButtonPrompt", "多按钮询问", "\uE82D")]
public class MultiButtonPromptAction(IActionService actionService)
    : ActionBase<MultiButtonPromptSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        if (Settings.Buttons.Count == 0)
        {
            return;
        }

        var window = new MultiButtonPromptWindow
        {
            DataContext = new MultiButtonPromptViewModel(Settings, actionService)
        };

        await window.ShowDialogCompat();
    }
}
