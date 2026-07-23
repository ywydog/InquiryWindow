using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
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

        // 亚克力背景：窗口加载时根据"亚克力背景"开关决定是否启用
        // 注意必须在 Loaded 之后再设置 Window.Background/TransparencyLevelHint，
        // 否则在某些平台上会被后续逻辑覆盖。
        Loaded += (_, _) => ApplyAcrylicBackground();
    }

    /// <summary>
    /// 根据 <see cref="Services.PluginSettingsStore"/> 的当前值，给本窗口应用或取消亚克力背景。
    /// </summary>
    private void ApplyAcrylicBackground()
    {
        var settings = Services.PluginSettingsStore.Instance.Settings;
        var isAcrylic = settings.IsAcrylicEnabled;

        // 1) 切换窗口级别的亚克力基底（透明 + AcrylicBlur 提示）
        AcrylicHelper.Apply(this, isAcrylic);

        // 2) 切换根 Border 的背景：亚克力用半透明刷，否则回退到不透明卡片
        if (RootBorder is not null)
        {
            RootBorder.Background = AcrylicHelper.GetCardBrush(isAcrylic);
        }
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

    public InquiryWindowResult Result { get; private set; } = InquiryWindowResult.Cancel;

    /// <summary>
    /// 显示确认弹窗并等待用户选择。
    /// 重写父类方法是为了统一通过 <see cref="Result"/> 返回结果，
    /// 并在关闭前用 <see cref="_allowClose"/> 拦截非按钮关闭。
    /// </summary>
    public new async Task<InquiryWindowResult> ShowDialog(Window? owner = null)
    {
        _allowClose = false;

        if (owner != null)
        {
            return await base.ShowDialog<InquiryWindowResult>(owner);
        }

        Show();
        var tcs = new TaskCompletionSource<InquiryWindowResult>();
        Closed += (_, _) => tcs.TrySetResult(Result);
        return await tcs.Task;
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
    }
}
