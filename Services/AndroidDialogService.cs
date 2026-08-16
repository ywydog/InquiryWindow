using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services.UI;
using ClassIsland.Core.Enums.UI;
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
/// 因此运行时弹窗无法用独立窗口实现。同时 Android 的 AOT 会把插件的反射调用裁剪掉——
/// 例如 <c>FluentAvalonia.UI.Controls.FAContentDialog.Hide(...)</c>（ClassIsland 自身从不
/// 编程式关闭对话框，AOT 便不会保留该方法），运行时直接抛 MissingMethodException 崩溃。
/// </para>
/// <para>
/// 因此这里改用 Avalonia 核心控件 <see cref="Popup"/> 承载从 Window 抽取出来的内容
/// UserControl，通过 <c>IsOpen</c> 开关实现打开/关闭——<see cref="Popup"/> 是核心控件、
/// ClassIsland 大量使用，不会被 AOT 裁剪。
/// </para>
/// </summary>
public static class AndroidDialogService
{
    /// <summary>
    /// 让 Popup 贴合当前 Activity 的视图宿主，使居中定位有明确的参照控件。
    /// 取不到宿主时保持默认（Popup 会退到顶层 overlay）。
    /// </summary>
    private static void TrySetHost(Popup popup)
    {
        try
        {
            var viewHost = IViewHostProvider.Instance.GetViewHost(ViewActivationPreference.Default);
            if (viewHost is Control hostControl)
            {
                popup.PlacementTarget = hostControl;
            }
        }
        catch
        {
            // 取宿主失败时让 Popup 自行使用顶层 overlay，不影响显示。
        }
    }
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
        var popup = new Popup
        {
            IsLightDismissEnabled = false,
            Placement = PlacementMode.Center,
            Child = content
        };
        TrySetHost(popup);

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
                popup.IsOpen = false;
            }
        }

        // 任何途经关闭（含系统返回 / Back 键）都兜底为「取消」。
        popup.Closed += (_, _) =>
        {
            if (!tcs.Task.IsCompleted)
            {
                tcs.TrySetResult(InquiryWindowResult.Cancel);
            }
        };

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
            popup.IsOpen = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Android 上显示「询问窗」弹窗失败。");
            Complete(InquiryWindowResult.Cancel);
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
        var popup = new Popup
        {
            IsLightDismissEnabled = false,
            Placement = PlacementMode.Center,
            Child = content
        };
        TrySetHost(popup);

        // ViewModel 通过 RequestClose 请求关闭（用户点了某个按钮 / 自动执行"无事发生"）。
        void OnRequestClose() => popup.IsOpen = false;
        vm.RequestClose += OnRequestClose;

        var tcs = new TaskCompletionSource();
        popup.Closed += (_, _) => tcs.TrySetResult();

        // 自动执行倒计时（与桌面版一致，由 ViewModel 驱动）。
        vm.StartAutoExecuteCountdown();

        try
        {
            popup.IsOpen = true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Android 上显示「多按钮询问」弹窗失败。");
            popup.IsOpen = false;
        }

        await tcs.Task;
        vm.RequestClose -= OnRequestClose;
        vm.Cleanup();
    }
}