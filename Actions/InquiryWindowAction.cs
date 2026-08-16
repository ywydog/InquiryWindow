using Avalonia.Controls;
using Avalonia.Media.Imaging;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Controls;
using InquiryWindow.Models;
using InquiryWindow.Services;
using InquiryWindow.Views;
using Microsoft.Extensions.Logging;

namespace InquiryWindow.Actions;

// addDefaultToMenu: false —— 关闭系统自动加默认菜单，改由 Plugin.BuildActionMenuTree
// 统一注册到「InquiryWindow 行动」集下，与 MultiButtonPromptAction 一起集中管理。
[ActionInfo("action.inquiryWindow", "询问窗", "\uE4C4", addDefaultToMenu: false)]
public class InquiryWindowAction(
    ILessonsService lessonsService,
    IExactTimeService exactTimeService,
    ILogger<InquiryWindowAction> logger)
    : ActionBase<InquiryWindowActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();
        logger.LogDebug("InquiryWindowAction 触发，路径：{Path}", Settings.TargetPath);

        // 1. 校验 TargetPath
        var hasPath = !string.IsNullOrWhiteSpace(Settings.TargetPath);

        // 2. 变量替换
        var titleResolved = VariableReplacer.Replace(Settings.DialogTitle, lessonsService, exactTimeService);
        var bodyResolved  = VariableReplacer.Replace(Settings.DialogBody,  lessonsService, exactTimeService);

        InquiryWindowResult result;
        if (OperatingSystem.IsAndroid())
        {
            // Android 的 WindowingPlatformStub 不支持创建 Window，改用 ContentDialog 承载「询问窗」内容。
            // 不去提取图标、不设亚克力、不实际启动目标路径（均不适用于 Android）。
            result = await AndroidDialogService.ShowInquiryWindowAsync(
                Settings, logger, titleResolved, bodyResolved, hasPath);
        }
        else
        {
            // 3. 桌面端：独立窗口弹窗
            var window = new InquiryWindowWindow
            {
                WindowTitle      = Settings.WindowTitle,
                DialogTitleSmall = titleResolved,
                DialogTitle      = titleResolved,
                DialogBody       = bodyResolved,
                PathText         = Settings.TargetPath,
                IsPathVisible    = Settings.ShowPath && hasPath,
                Icon             = null,
                IsIconVisible    = false,
                CanExecute       = hasPath
            };

            // 4. 若启用自动执行，则启动倒计时（仅在有目标路径时倒计时才有意义）
            if (Settings.IsAutoExecuteEnabled && hasPath)
            {
                window.StartAutoExecuteCountdown((int)Math.Ceiling(Settings.AutoExecuteSeconds));
            }

            var owner = AppBase.Current.GetRootWindow() as Window;
            result = await window.ShowDialog(owner);
        }

        logger.LogDebug("用户选择：{Result}", result == InquiryWindowResult.Execute ? "执行" : "取消");

        // 5. Android 分支不实际执行目标路径（不能直接 Process.Start），仅记录日志。
        if (result == InquiryWindowResult.Execute && hasPath)
        {
            logger.LogInformation("已选择执行（Android 分支不实际启动目标）：{Path}", Settings.TargetPath);
        }
    }
}
