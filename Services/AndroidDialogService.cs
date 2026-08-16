using Avalonia.Threading;
using ClassIsland.Core.Extensions.UI;
using FluentAvalonia.UI.Controls;
using InquiryWindow.Actions;
using InquiryWindow.Models;
using InquiryWindow.ViewModels;
using InquiryWindow.Views;
using Microsoft.Extensions.Logging;

namespace InquiryWindow.Services;

/// <summary>
/// Android 分支专用的运行时弹窗服务。
///
/// <para>
/// Android 上 <c>WindowingPlatformStub</c> 不支持创建 <see cref="Avalonia.Controls.Window"/>，
/// 因此运行时弹窗无法用独立窗口实现。这里改用 <see cref="FAContentDialog"/>（覆盖层对话框）
/// 承载从 Window 抽取出来的内容 UserControl，并通过 <c>ShowAsyncAuto()</c> 让 ClassIsland
/// 自动解析 Android 的 TopLevel（<c>IViewHostProvider</c>）作为宿主。
/// </para>
/// </summary>
public static class AndroidDialogService
{
    /// <summary>
    /// 显示「询问窗」弹窗（是 / 否）并等待用户选择。
    /// </summary>
    /// <param name="settings">行动设置。</param>
    /// <param name="logger">日志器，用于输出倒计时/异常信息。</param>
    /// <param name="titleResolved">替换变量后的标题。</param>
    /// <param name="bodyResolved">替换变量后的正文。</param>
    /// <param name="hasPath">是否存在目标路径。</param>
    /// <returns>用户选择结果。</returns>
    public static async Task<InquiryWindowResult> ShowInquiryWindowAsync(
        InquiryWindowActionSettings settings,
        ILogger logger,
        string titleResolved,
        string bodyResolved,
        bool hasPath)
    {
        var vm = new InquiryWindowWindow.ViewModel
        {
            DialogTitleSmall = titleResolved,
            DialogTitle = titleResolved,
            DialogBody = bodyResolved,
            PathText = settings.TargetPath,
            IsPathVisible = settings.ShowPath && hasPath,
            Icon = null,
            IsIconVisible = false,
            CanExecute = hasPath,
        };

        var content = new InquiryWindowDialogContent { DataContext = vm };
        var dialog = new FAContentDialog { Content = content };

        var tcs = new TaskCompletionSource<InquiryWindowResult>();
        DispatcherTimer? timer = null;
        EventHandler? onTick = null;

        void Complete(InquiryWindowResult result)
        {
            if (!tcs.Task.IsCompleted)
            {
                if (timer != null)
                {
                    timer.Stop();
                    if (onTick != null)
                    {
                        timer.Tick -= onTick;
                    }
                    timer = null;
                }
                vm.IsAutoExecuteActive = false;
                tcs.TrySetResult(result);
                dialog.Hide();
            }
        }

        // 任何途径关闭（含 ContentDialog 的关闭按钮）都兜底为「取消」。
        // 注：FluentAvalonia 运行时版本没有 dialog.Closing 事件，改为在 ShowAsyncAuto
        // 返回后检测 tcs 是否已被按钮结果填充，未填充则视为「取消」。
        content.ResultChosen += Complete;

        // 自动执行倒计时：归零时等效「执行」。
        if (settings.IsAutoExecuteEnabled && hasPath)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(settings.AutoExecuteSeconds));
            var remaining = seconds;
            vm.IsAutoExecuteActive = true;
            vm.AutoExecuteMaxSeconds = seconds;
            vm.AutoExecuteRemainingSeconds = remaining;
            vm.CountdownText = FormatCountdown(remaining);
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            onTick = (_, _) =>
            {
                remaining--;
                if (remaining > 0)
                {
                    vm.AutoExecuteRemainingSeconds = remaining;
                    vm.CountdownText = FormatCountdown(remaining);
                }
                else
                {
                    Complete(InquiryWindowResult.Execute);
                }
            };
            timer.Tick += onTick;
            timer.Start();
        }

        try
        {
            await dialog.ShowAsyncAuto();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Android 上显示「询问窗」弹窗失败。");
            Complete(InquiryWindowResult.Cancel);
        }

        // 兜底：弹窗以任何未选择按钮的方式关闭（如系统返回 / 关闭按钮）时按「取消」处理。
        if (!tcs.Task.IsCompleted)
        {
            tcs.TrySetResult(InquiryWindowResult.Cancel);
        }

        var result = await tcs.Task;
        content.ResultChosen -= Complete;
        return result;
    }

    /// <summary>
    /// 把剩余秒数格式化为 "将X分Y秒后执行......"。
    /// </summary>
    private static string FormatCountdown(int seconds)
    {
        var minutes = seconds / 60;
        var secs = seconds % 60;
        return $"将{minutes}分{secs}秒后执行......";
    }

    /// <summary>
    /// 显示「多按钮询问」弹窗（承载按钮列表 / 倒计时 / 背景）并等待关闭。
    /// </summary>
    /// <param name="vm">多按钮询问的 ViewModel（由调用方构造）。</param>
    /// <param name="logger">日志器，用于输出异常信息。</param>
    public static async Task ShowMultiButtonPromptAsync(MultiButtonPromptViewModel vm, ILogger logger)
    {
        var content = new MultiButtonPromptDialogContent { DataContext = vm };
        var dialog = new FAContentDialog { Content = content };

        // ViewModel 通过 RequestClose 请求关闭（用户点了某个按钮）。
        void OnRequestClose() => dialog.Hide();
        vm.RequestClose += OnRequestClose;

        // 自动执行倒计时（与桌面版一致，由 ViewModel 驱动）。
        vm.StartAutoExecuteCountdown();

        try
        {
            await dialog.ShowAsyncAuto();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Android 上显示「多按钮询问」弹窗失败。");
        }
        finally
        {
            vm.RequestClose -= OnRequestClose;
            vm.Cleanup();
        }
    }
}