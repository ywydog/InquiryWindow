using System.Diagnostics;
using System.Runtime.Versioning;
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
[SupportedOSPlatform("windows")]   // 调 IconExtractorService（仅 Windows）
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

        // 3. 提取图标（仅在 ShowIcon=true 且是 .exe 时提取）
        Bitmap? icon = null;
        if (Settings.ShowIcon && hasPath && !Settings.IsInteractiveFusion)
        {
            icon = IconExtractorService.TryExtract(Settings.TargetPath);
            logger.LogDebug("图标提取{Result}", icon != null ? "成功" : "失败或目标非 .exe");
        }

        InquiryWindowResult result;
        if (Settings.IsInteractiveFusion)
        {
            // 5.0 「交互融合」模式：把询问内容融合到 ClassIsland 主界面覆盖层显示（而非独立窗口）
            result = await InquiryWindowFusionOverlay.ShowInquiryWindowAsync(
                Settings, logger, titleResolved, bodyResolved, hasPath);
            logger.LogDebug("交互融合模式：用户选择：{Result}", result == InquiryWindowResult.Execute ? "执行" : "取消");
        }
        else
        {
            // 4. 构造并显示弹窗
            var window = new InquiryWindowWindow
            {
                WindowTitle      = Settings.WindowTitle,
                DialogTitleSmall = titleResolved,
                DialogTitle      = titleResolved,
                DialogBody       = bodyResolved,
                PathText         = Settings.TargetPath,
                IsPathVisible    = Settings.ShowPath && hasPath,
                Icon             = icon,
                IsIconVisible    = icon != null,
                CanExecute       = hasPath
            };

            // 4.4 亚克力背景：从插件级全局设置里读取（设置页可开关），无侵入式地挂到弹窗上
            var pluginSettings = PluginSettingsStore.Instance.Data;
            window.UseAcrylicBackground = pluginSettings.UseAcrylicBackground;
            window.AcrylicTintOpacity = pluginSettings.AcrylicTintOpacity;

            // 4.5 若启用自动执行，则启动倒计时（仅在有目标路径时倒计时才有意义）
            if (Settings.IsAutoExecuteEnabled && hasPath)
            {
                window.StartAutoExecuteCountdown((int)Math.Ceiling(Settings.AutoExecuteSeconds));
            }

            var owner = AppBase.Current.GetRootWindow() as Window;
            result = await window.ShowDialog(owner);
            logger.LogDebug("用户选择：{Result}", result == InquiryWindowResult.Execute ? "执行" : "取消");
        }

        // 5. 根据结果处理
        if (result == InquiryWindowResult.Execute && hasPath)
        {
            await LaunchTargetAsync(Settings.TargetPath);
        }
    }

    private async Task LaunchTargetAsync(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            logger.LogInformation("已打开目标：{Path}", path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "打开目标失败：{Path}", path);
            // 主弹窗已关闭；弹独立错误提示
            await CommonTaskDialogs.ShowDialog("打开失败", $"无法打开「{path}」：{ex.Message}");
        }
    }
}
