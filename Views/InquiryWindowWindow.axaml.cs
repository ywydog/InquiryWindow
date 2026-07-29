using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ClassIsland.Core.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using InquiryWindow.Actions;

namespace InquiryWindow.Views;

public partial class InquiryWindowWindow : MyWindow
{
    private readonly ViewModel _vm = new();
    private bool _allowClose;
    private DispatcherTimer? _autoExecuteTimer;
    private int _autoExecuteRemaining;

    public InquiryWindowWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // 拦截关闭：必须通过按钮才能关闭
        Closing += (_, e) =>
        {
            if (!_allowClose)
            {
                e.Cancel = true;
            }
        };

        // 拦截最小化：自动还原
        this.GetObservable(WindowStateProperty).Subscribe(s =>
        {
            if (s == WindowState.Minimized)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    WindowState = WindowState.Normal;
                    Activate();
                }, DispatcherPriority.MaxValue);
            }
        });
    }

    public string WindowTitle
    {
        get => Title ?? "";
        set => Title = value;
    }

    public string DialogTitleSmall
    {
        get => _vm.DialogTitleSmall;
        set => _vm.DialogTitleSmall = value;
    }

    public string DialogTitle
    {
        get => _vm.DialogTitle;
        set => _vm.DialogTitle = value;
    }

    public string DialogBody
    {
        get => _vm.DialogBody;
        set => _vm.DialogBody = value;
    }

    public string PathText
    {
        get => _vm.PathText;
        set => _vm.PathText = value;
    }

    public bool IsPathVisible
    {
        get => _vm.IsPathVisible;
        set => _vm.IsPathVisible = value;
    }

    public new Bitmap? Icon
    {
        get => _vm.Icon;
        set
        {
            _vm.Icon = value;
            _vm.IsIconVisible = value != null;
        }
    }

    public bool IsIconVisible
    {
        get => _vm.IsIconVisible;
        set => _vm.IsIconVisible = value;
    }

    public bool CanExecute
    {
        get => _vm.CanExecute;
        set => _vm.CanExecute = value;
    }

    /// <summary>
    /// 预览模式：仅供设置页调试外观用。
    /// 打开时隐藏「取消 / 执行」按钮，只显示「看完了」按钮（点击只关闭弹窗，
    /// 不会触发任何后续动作，也不会启动自动执行倒计时）。
    /// </summary>
    public bool IsPreviewMode
    {
        get => _vm.IsPreviewMode;
        set => _vm.IsPreviewMode = value;
    }

    /// <summary>
    /// 弹窗结果。默认 <see cref="InquiryWindowResult.Cancel"/>；
    /// 用户点"执行"后被改写为 <see cref="InquiryWindowResult.Execute"/>。
    /// </summary>
    public InquiryWindowResult Result { get; private set; } = InquiryWindowResult.Cancel;

    /// <summary>
    /// 启用 / 关闭询问窗的亚克力模糊背景。
    /// 启用时把窗口设为 <c>AcrylicBlur</c> 透明 + 内部 Border 改用半透明系统画刷。
    /// tint 透明度由 <see cref="AcrylicTintOpacity"/> 控制（0~1）。
    /// </summary>
    public bool UseAcrylicBackground
    {
        get => _vm.IsAcrylicActive;
        set
        {
            if (_vm.IsAcrylicActive == value) return;
            _vm.IsAcrylicActive = value;
            ApplyAcrylicState();
        }
    }

    /// <summary>
    /// 亚克力背景色调不透明度（0~1）。值越小背景越透明，桌面越清晰。
    /// 仅在 <see cref="UseAcrylicBackground"/> 为 true 时生效。
    /// </summary>
    public double AcrylicTintOpacity
    {
        get => _vm.AcrylicTintOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_vm.AcrylicTintOpacity - clamped) < 0.0001) return;
            _vm.AcrylicTintOpacity = clamped;
            ApplyAcrylicState();
        }
    }

    /// <summary>
    /// 显示确认弹窗并等待用户选择。
    /// 复刻 v1 的语义：通过 <see cref="Result"/> 统一返回结果，
    /// 并在关闭前用 <see cref="_allowClose"/> 拦截非按钮关闭。
    /// 参数改成 <see cref="TopLevel"/> 是因为 Misha 起 <c>AppBase.GetRootWindow()</c>
    /// 返回的是 <see cref="TopLevel"/>（不再保证是 <see cref="Window"/>）。
    /// </summary>
    public async Task<InquiryWindowResult> ShowDialog(TopLevel? owner = null)
    {
        _allowClose = false;

        if (owner is Window windowOwner)
        {
            return await base.ShowDialog<InquiryWindowResult>(windowOwner);
        }

        Show();
        var tcs = new TaskCompletionSource<InquiryWindowResult>();
        Closed += (_, _) => tcs.TrySetResult(Result);
        return await tcs.Task;
    }

    /// <summary>
    /// 把当前的 <see cref="UseAcrylicBackground"/> + <see cref="AcrylicTintOpacity"/>
    /// 实际应用到窗口和内容 Border 上。
    /// 默认状态是纯色风格（沿用原版），打开亚克力时改用 AcrylicBlur 透明 + 半透明系统画刷。
    /// </summary>
    private void ApplyAcrylicState()
    {
        if (_vm.IsAcrylicActive)
        {
            // 打开亚克力：窗口透到桌面 + AcrylicBlur 模糊，
            // Border 用系统 ChromeMediumLow 画刷并按用户设定的 tint 调整不透明度。
            TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur };
            Background = Brushes.Transparent;

            if (this.FindControl<Border>("ContentRoot") is { } border)
            {
                border.Background = new SolidColorBrush(GetAcrylicTintColor(_vm.AcrylicTintOpacity));
                border.Opacity = 1.0;
            }
        }
        else
        {
            // 关闭亚克力：还原成纯色 LayerFillColorAltBrush（与原版视觉一致）。
            TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
            Background = null;     // 让窗口回到默认主题背景

            if (this.FindControl<Border>("ContentRoot") is { } border)
            {
                border.Background = null;     // 还原成 XAML 里 DynamicResource 绑定的 LayerFillColorAltBrush
                border.Opacity = 1.0;
            }
        }
    }

    /// <summary>
    /// 按不透明度算出一组用于亚克力 tint 的颜色（深色/浅色主题各一套）。
    /// 直接用 alpha 比对会偏暗，这里用 FluentAvalonia 自带的 ChromeMediumLow 颜色为基础，
    /// 按用户值在 0（完全透明）~1（接近纯色）之间插值。
    /// </summary>
    private static Color GetAcrylicTintColor(double opacity)
    {
        // 参考 ConvenientText 的视觉：深色模式用 #2D2D2D 半透、浅色模式用 #F3F3F3 半透。
        // FluentAvalonia 的请求主题：深色用偏黑、浅色用偏白。
        var isDark = Application.Current?.RequestedThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        var baseColor = isDark
            ? Color.FromRgb(0x2D, 0x2D, 0x2D)
            : Color.FromRgb(0xF3, 0xF3, 0xF3);
        // opacity 0.5 → 半透；1 → 全不透明；0 → 完全透明（交给 AcrylicBlur 看桌面）。
        var alpha = (byte)Math.Clamp(opacity * 255, 0, 255);
        return Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CancelAutoExecuteCountdown();
        Result = InquiryWindowResult.Cancel;
        CloseProgrammatically();
    }

    private void OnExecuteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CancelAutoExecuteCountdown();
        Result = InquiryWindowResult.Execute;
        CloseProgrammatically();
    }

    /// <summary>
    /// 预览模式下的「看完了」按钮：仅关闭弹窗，不触发任何后续动作。
    /// </summary>
    private void OnAcknowledgeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CancelAutoExecuteCountdown();
        Result = InquiryWindowResult.Acknowledged;
        CloseProgrammatically();
    }

    private void CloseProgrammatically()
    {
        _allowClose = true;
        base.Close(Result);
    }

    /// <summary>
    /// 启动自动执行倒计时。倒计时归零时按"执行"关闭弹窗。
    /// 必须在 ShowDialog 之前调用。调用后用户点取消/执行会自动停止计时。
    /// </summary>
    /// <param name="seconds">倒计时秒数（&lt;=0 时不会启动）。</param>
    public void StartAutoExecuteCountdown(int seconds)
    {
        if (seconds <= 0) return;
        CancelAutoExecuteCountdown();
        _autoExecuteRemaining = seconds;
        _vm.AutoExecuteMaxSeconds = seconds;
        _vm.AutoExecuteRemainingSeconds = seconds;
        _vm.IsAutoExecuteActive = true;
        _vm.CountdownText = FormatCountdownText(seconds, buttonName: null);
        _autoExecuteTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autoExecuteTimer.Tick += OnAutoExecuteTick;
        _autoExecuteTimer.Start();
    }

    /// <summary>
    /// 停止自动执行倒计时（用户已主动选择或窗口被销毁时调用）。
    /// </summary>
    public void CancelAutoExecuteCountdown()
    {
        if (_autoExecuteTimer != null)
        {
            _autoExecuteTimer.Stop();
            _autoExecuteTimer.Tick -= OnAutoExecuteTick;
            _autoExecuteTimer = null;
        }
        _autoExecuteRemaining = 0;
        _vm.IsAutoExecuteActive = false;
        _vm.AutoExecuteRemainingSeconds = 0;
        _vm.CountdownText = string.Empty;
    }

    private void OnAutoExecuteTick(object? sender, EventArgs e)
    {
        _autoExecuteRemaining--;
        if (_autoExecuteRemaining > 0)
        {
            _vm.AutoExecuteRemainingSeconds = _autoExecuteRemaining;
            _vm.CountdownText = FormatCountdownText(_autoExecuteRemaining, buttonName: null);
            return;
        }
        // 倒计时归零：等同于按"执行"
        CancelAutoExecuteCountdown();
        Result = InquiryWindowResult.Execute;
        CloseProgrammatically();
    }

    /// <summary>
    /// 把剩余秒数格式化为 "将X分Y秒后执行{按钮}......"。
    /// <paramref name="buttonName"/> 为 null 时省略按钮名。
    /// </summary>
    private static string FormatCountdownText(int seconds, string? buttonName)
    {
        var minutes = seconds / 60;
        var secs = seconds % 60;
        var time = $"{minutes}分{secs}秒";
        return string.IsNullOrEmpty(buttonName)
            ? $"将{time}后执行......"
            : $"将{time}后执行「{buttonName}」......";
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }

    public partial class ViewModel : ObservableObject
    {
        [ObservableProperty] private string _dialogTitleSmall = "";
        [ObservableProperty] private string _dialogTitle = "";
        [ObservableProperty] private string _dialogBody = "";
        [ObservableProperty] private string _pathText = "";
        [ObservableProperty] private bool _isPathVisible;
        [ObservableProperty] private Bitmap? _icon;
        [ObservableProperty] private bool _isIconVisible;
        [ObservableProperty] private bool _canExecute = true;
        [ObservableProperty] private bool _isAutoExecuteActive;
        [ObservableProperty] private string _countdownText = string.Empty;
        [ObservableProperty] private double _autoExecuteMaxSeconds;
        [ObservableProperty] private double _autoExecuteRemainingSeconds;

        // 亚克力背景：仅用于本窗口内 UI 与初始化时同步；最终值由外部属性驱动。
        [ObservableProperty] private bool _isAcrylicActive;
        [ObservableProperty] private double _acrylicTintOpacity = 0.5;

        // 预览模式：仅显示「看完了」按钮，隐藏「取消 / 执行」。
        [ObservableProperty] private bool _isPreviewMode;
    }
}
