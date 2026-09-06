using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ClassIsland.Core;
using InquiryWindow.Actions;
using InquiryWindow.Models;
using InquiryWindow.ViewModels;
using Microsoft.Extensions.Logging;

namespace InquiryWindow.Views;

/// <summary>
/// 「交互融合」覆盖窗口（桌面版专用）。
///
/// <para>
/// 参考 SystemTools 语音对话覆盖层的思路：无边框、透明背景、置顶、不显示任务栏图标，
/// 并贴合 ClassIsland 主界面的位置与尺寸，把询问内容（询问窗 / 多按钮询问）融合到
/// 主界面覆盖层显示，而不是打开独立弹窗窗口。由「交互融合」开关（默认关闭）控制启用。
/// </para>
/// </summary>
public partial class InquiryWindowFusionOverlay : Window
{
    public InquiryWindowFusionOverlay()
    {
        InitializeComponent();
        // 无边框在 XAML 中通过 SystemDecorations="None" 设置。
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
    }

    /// <summary>
    /// 取得 ClassIsland 主界面宿主窗口（用于覆盖定位）。
    /// </summary>
    private static Window? GetHostWindow()
    {
        return AppBase.Current.GetRootWindow() as Window;
    }

    /// <summary>
    /// 把覆盖窗口贴合到宿主窗口的位置与尺寸，使询问内容"融合"在主界面上。
    /// 宿主不可用时回退到屏幕居中。
    /// </summary>
    private void AttachToHost(Window? host)
    {
        if (host is null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        Owner = host;
        Position = host.Position;
        Width = host.Bounds.Width;
        Height = host.Bounds.Height;
    }

    /// <summary>
    /// 显示「询问窗」弹窗（是 / 否）并等待用户选择。
    /// 逻辑与 AndroidDialogService.ShowInquiryWindowAsync 一致，宿主由覆盖窗口替代。
    /// </summary>
    public static async Task<InquiryWindowResult> ShowInquiryWindowAsync(
        InquiryWindowActionSettings settings,
        ILogger logger,
        string titleResolved,
        string bodyResolved,
        bool hasPath)
    {
        var overlay = new InquiryWindowFusionOverlay();
        overlay.AttachToHost(GetHostWindow());

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
        overlay.ContentHost.Content = content;

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
                overlay.Close();
            }
        }

        // 任何途径关闭（含 Alt+F4 等）都兜底为「取消」。
        overlay.Closing += (_, _) =>
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
            overlay.Show();
            overlay.Activate();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "交互融合模式下显示「询问窗」弹窗失败，回退为取消。");
            Complete(InquiryWindowResult.Cancel);
        }

        var result = await tcs.Task;
        content.ResultChosen -= Complete;
        return result;
    }

    /// <summary>
    /// 显示「多按钮询问」弹窗（承载按钮列表 / 倒计时 / 背景）并等待关闭。
    /// 逻辑与 AndroidDialogService.ShowMultiButtonPromptAsync 一致，宿主由覆盖窗口替代。
    /// </summary>
    public static async Task ShowMultiButtonPromptAsync(MultiButtonPromptViewModel vm, ILogger logger)
    {
        var overlay = new InquiryWindowFusionOverlay();
        overlay.AttachToHost(GetHostWindow());

        var content = new MultiButtonPromptDialogContent { DataContext = vm };
        overlay.ContentHost.Content = content;

        // ViewModel 通过 RequestClose 请求关闭（用户点了某个按钮）。
        void OnRequestClose() => overlay.Close();
        vm.RequestClose += OnRequestClose;

        var tcs = new TaskCompletionSource();
        overlay.Closed += (_, _) => tcs.TrySetResult();

        // 自动执行倒计时（与桌面版一致，由 ViewModel 驱动）。
        vm.StartAutoExecuteCountdown();

        try
        {
            overlay.Show();
            overlay.Activate();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "交互融合模式下显示「多按钮询问」弹窗失败。");
            overlay.Close();
        }

        await tcs.Task;
        vm.RequestClose -= OnRequestClose;
        vm.Cleanup();
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

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}