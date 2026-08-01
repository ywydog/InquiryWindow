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

        // 预览模式：仅显示「看完了」按钮，隐藏「取消 / 执行」。
        [ObservableProperty] private bool _isPreviewMode;
    }
}
