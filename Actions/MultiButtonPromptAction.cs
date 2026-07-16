using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using InquiryWindow.Models;
using InquiryWindow.ViewModels;
using InquiryWindow.Views;
using Microsoft.Extensions.Logging;

namespace InquiryWindow.Actions;

/// <summary>
/// 多按钮询问：弹出一个 N 按钮窗口，按下任一按钮后按顺序执行该按钮的 Action 链。
/// </summary>
[ActionInfo("InquiryWindow.MultiButtonPrompt", "多按钮询问", "\uE82D")]
public class MultiButtonPromptAction(
    IActionService actionService,
    ILogger<MultiButtonPromptViewModel> viewModelLogger,
    ILogger<MultiButtonPromptAction> logger)
    : ActionBase<MultiButtonPromptSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        if (Settings.Buttons.Count == 0 && !Settings.IsAutoExecuteEnabled)
        {
            // 没有任何按钮 + 没启用自动执行：弹窗没有可点的按钮，倒计时也不会启动。
            // 这种情况多半是 Action 配置错了，行为上选择"什么都不做"并写日志告警。
            logger.LogWarning(
                "多按钮询问触发但 Buttons 为空且未启用自动执行，工作流表现为无操作。请检查 Action 配置或至少启用自动执行。");
            return;
        }

        if (Settings.Buttons.Count == 0 && Settings.IsAutoExecuteEnabled)
        {
            // 按钮被清空但开启了自动执行：仍然弹窗，让自动执行按"无事发生"自行关闭。
            // 主要是给运维一个"动作发生过"的视觉反馈，方便看到 Action 实际被触发了。
            logger.LogWarning(
                "多按钮询问触发：Buttons 已清空但启用了自动执行。弹窗将出现并按「无事发生」处理（到时间后自动关闭，不执行任何 Action）。");
        }

        var window = new MultiButtonPromptWindow
        {
            WindowTitle = Settings.Title,
            DataContext = new MultiButtonPromptViewModel(Settings, actionService, viewModelLogger)
        };

        // 启用自动执行时启动倒计时，倒计时归零按指定目标触发（按钮 Action 链或"无事发生"）。
        if (window.DataContext is MultiButtonPromptViewModel vm)
        {
            vm.StartAutoExecuteCountdown();
        }

        await window.ShowDialogCompat();
    }
}
