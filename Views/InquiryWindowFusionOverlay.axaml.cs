using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
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
    private Window? _host;

    public InquiryWindowFusionOverlay()
    {
        InitializeComponent();
        // 无边框在 XAML 中通过 WindowDecorations="None" 设置。
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
    }

    /// <summary>
    /// 取得 ClassIsland 主界面宿主窗口（用于覆盖定位）。
    /// 不能用 <see cref="AppBase.GetRootWindow"/>：它会优先返回当前激活的窗口
    /// （例如 ClassIsland 设置窗口），导致覆盖层贴合到错误的窗口上。
    /// </summary>
    private static Window? GetHostWindow()
    {
        return AppBase.Current.MainWindow is { IsVisible: true } mainWindow &&
               mainWindow.WindowState != WindowState.Minimized
            ? mainWindow
            : null;
    }

    /// <summary>
    /// 把覆盖窗口贴合到宿主窗口的位置与尺寸，使询问内容"融合"在主界面上。
    /// 与 SystemTools 覆盖层一致：优先取主界面实际可见区域（PART_GridWrapper 的并集），
    /// 并考虑 DPI 缩放；宿主不可用时回退到屏幕居中。
    /// </summary>
    private void AttachToHost(Window? host)
    {
        if (host is null)
        {
            // 没有可用的主界面：退化为居中的独立弹窗尺寸（高度随内容自适应）。
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = 500;
            Height = double.NaN;
            SizeToContent = SizeToContent.Height;
            MaxHeight = 800;
            return;
        }

        _host = host;
        Owner = host;
        UpdateHostBounds();

        // 主界面被拖动 / 缩放 / 跨屏 DPI 变化时，覆盖层跟随移动，避免弹窗脱节。
        host.PositionChanged += OnHostPositionChanged;
        host.SizeChanged += OnHostSizeChanged;
        host.ScalingChanged += OnHostScalingChanged;
    }

    private void DetachHost()
    {
        if (_host is not { } host)
        {
            return;
        }

        host.PositionChanged -= OnHostPositionChanged;
        host.SizeChanged -= OnHostSizeChanged;
        host.ScalingChanged -= OnHostScalingChanged;
        _host = null;
    }

    protected override void OnClosed(EventArgs e)
    {
        DetachHost();
        base.OnClosed(e);
    }

    private void OnHostPositionChanged(object? sender, PixelPointEventArgs e) => UpdateHostBounds();

    private void OnHostSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateHostBounds();

    private void OnHostScalingChanged(object? sender, EventArgs e) => UpdateHostBounds();

    private void UpdateHostBounds()
    {
        if (_host is not { } host)
        {
            return;
        }

        try
        {
            var scaling = host.RenderScaling > 0 ? host.RenderScaling : 1.0;
            if (GetIslandScreenRect(host) is { } island)
            {
                Position = island.Position;
                Width = Math.Max(1, island.Width / scaling);
                Height = Math.Max(1, island.Height / scaling);
                return;
            }

            Position = host.Position;
            Width = Math.Max(1, host.Bounds.Width);
            Height = Math.Max(1, host.Bounds.Height);
        }
        catch (Exception)
        {
            // 宿主正在关闭等情况下忽略定位失败，等待窗口自然关闭。
        }
    }

    /// <summary>
    /// 计算主界面可见区域（PART_GridWrapper 各实例的屏幕矩形的并集，单位为物理像素）。
    /// ClassIsland 主窗口本身可能是带透明边距的容器，直接用 Window.Bounds 会让弹窗
    /// 居中到透明区域，而不是用户看到的主界面卡片上。
    /// </summary>
    private static (PixelPoint Position, double Width, double Height)? GetIslandScreenRect(Window host)
    {
        try
        {
            var rects = host.GetVisualDescendants()
                .OfType<Grid>()
                .Where(grid => grid.Name == "PART_GridWrapper" &&
                               grid.IsEffectivelyVisible &&
                               grid.Bounds.Width > 0 && grid.Bounds.Height > 0)
                .Select(GetScreenRect)
                .Where(rect => rect.Width > 0 && rect.Height > 0)
                .ToList();

            if (rects.Count == 0)
            {
                return null;
            }

            var left = rects.Min(rect => rect.X);
            var top = rects.Min(rect => rect.Y);
            var right = rects.Max(rect => rect.Right);
            var bottom = rects.Max(rect => rect.Bottom);
            return (new PixelPoint(left, top), right - left, bottom - top);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static PixelRect GetScreenRect(Control control)
    {
        var topLeft = control.PointToScreen(new Point(0, 0));
        var bottomRight = control.PointToScreen(new Point(control.Bounds.Width, control.Bounds.Height));
        var left = Math.Min(topLeft.X, bottomRight.X);
        var top = Math.Min(topLeft.Y, bottomRight.Y);
        var right = Math.Max(topLeft.X, bottomRight.X);
        var bottom = Math.Max(topLeft.Y, bottomRight.Y);
        return new PixelRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
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
            CountdownBarStyleIndex = settings.CountdownBarStyleIndex,
        };

        var content = new InquiryWindowDialogContent { DataContext = vm };
        overlay.ContentHost.Content = content;

        var tcs = new TaskCompletionSource<InquiryWindowResult>();
        DispatcherTimer? timer = null;
        EventHandler? onTick = null;

        // 覆盖窗口可能尚未显示就失败，Close() 需要容忍这种情况。
        void CloseOverlay()
        {
            try
            {
                overlay.Close();
            }
            catch (InvalidOperationException)
            {
                // 窗口未显示 / 已关闭。
            }
        }

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
                CloseOverlay();
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
        logger.LogInformation("[交互融合] 显示「多按钮询问」覆盖窗口，按钮数={Count}。", vm.Buttons?.Count ?? 0);
        var overlay = new InquiryWindowFusionOverlay();
        overlay.AttachToHost(GetHostWindow());

        var content = new MultiButtonPromptDialogContent { DataContext = vm };
        logger.LogInformation("[交互融合] 准备注入多按钮询问内容，ContentHost={ContentHost}。",
            overlay.ContentHost is null ? "null" : "ok");
        overlay.ContentHost.Content = content;

        // ViewModel 通过 RequestClose 请求关闭（用户点了某个按钮）。
        void OnRequestClose() => overlay.Close();
        vm.RequestClose += OnRequestClose;

        var tcs = new TaskCompletionSource();
        overlay.Closed += (_, _) => tcs.TrySetResult();

        // 自动执行倒计时（与桌面版一致，由 ViewModel 驱动）。
        vm.StartAutoExecuteCountdown();
        // 弹窗打开即启动交互倒计时：为启用了「交互倒计时」的按钮锁定 N 秒。
        vm.StartInteractionCountdowns();

        try
        {
            overlay.Show();
            overlay.Activate();
            logger.LogInformation("[交互融合] 多按钮询问覆盖窗口已成功显示。");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "交互融合模式下显示「多按钮询问」弹窗失败。");
            // 显示失败时窗口不会触发 Closed，必须主动结束等待，否则行动会永久挂起。
            tcs.TrySetResult();
            try
            {
                overlay.Close();
            }
            catch (InvalidOperationException)
            {
                // 窗口未显示 / 已关闭。
            }
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
}