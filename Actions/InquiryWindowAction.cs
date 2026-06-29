using System.Diagnostics;
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

[ActionInfo("action.inquiryWindow", "询问窗", "\uE4C4")]
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
        if (Settings.ShowIcon && hasPath)
        {
            icon = IconExtractorService.TryExtract(Settings.TargetPath);
            logger.LogDebug("图标提取{Result}", icon != null ? "成功" : "失败或目标非 .exe");
        }

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

        var owner = AppBase.Current.GetRootWindow();
        var result = await window.ShowDialog(owner);
        logger.LogDebug("用户选择：{Result}", result == InquiryWindowResult.Execute ? "执行" : "取消");

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
