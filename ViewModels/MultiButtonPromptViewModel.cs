using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InquiryWindow.Models;
using Microsoft.Extensions.Logging;

namespace InquiryWindow.ViewModels;

/// <summary>
/// 多按钮询问弹窗的 ViewModel。
/// 按下任一按钮 → 关闭弹窗并按顺序执行该按钮的 Action 链（过程不再向用户询问）。
/// </summary>
public partial class MultiButtonPromptViewModel : ObservableObject
{
    public ObservableCollection<MultiButtonPromptButton> Buttons { get; }

    public string Title { get; }

    public string Prompt { get; }

    public string SubPrompt { get; }

    private readonly IActionService _actionService;
    private readonly ILogger<MultiButtonPromptViewModel> _logger;
    private readonly MultiButtonPromptSettings _settings;

    private DispatcherTimer? _autoExecuteTimer;
    private int _autoExecuteRemaining;

    /// <summary>按下按钮后的交互倒计时计时器。</summary>
    private DispatcherTimer? _pressCountdownTimer;

    /// <summary>当前正在进行交互倒计时的按钮引用；null 表示无。</summary>
    [ObservableProperty]
    private MultiButtonPromptButton? _pressCountdownButton;

    /// <summary>交互倒计时剩余秒数（用于 UI 实时显示）。</summary>
    [ObservableProperty]
    private double _pressCountdownRemaining;

    // ===== 窗口背景 =====
    private Bitmap? _backgroundImage;

    /// <summary>窗口背景图片。null 时弹窗降级为纯色背景。</summary>
    public Bitmap? BackgroundImage
    {
        get => _backgroundImage;
        set
        {
            if (ReferenceEquals(_backgroundImage, value)) return;
            _backgroundImage = value;
            OnPropertyChanged(nameof(BackgroundImage));
            OnPropertyChanged(nameof(HasBackgroundImage));
        }
    }

    /// <summary>是否有可用的背景图片（用于 Image 控件的 IsVisible 绑定与蒙层显隐）。</summary>
    public bool HasBackgroundImage => _backgroundImage is not null;

    private Stretch _backgroundImageStretch = Stretch.UniformToFill;

    /// <summary>背景图片的缩放模式（来自 Settings.BackgroundImageMode）。</summary>
    public Stretch BackgroundImageStretch
    {
        get => _backgroundImageStretch;
        set
        {
            if (_backgroundImageStretch == value) return;
            _backgroundImageStretch = value;
            OnPropertyChanged(nameof(BackgroundImageStretch));
        }
    }

    public MultiButtonPromptViewModel(
        MultiButtonPromptSettings settings,
        IActionService actionService,
        ILogger<MultiButtonPromptViewModel> logger)
    {
        _settings = settings;
        Buttons = settings.Buttons;
        Title = settings.Title;
        Prompt = settings.Prompt;
        SubPrompt = settings.SubPrompt;
        _actionService = actionService;
        _logger = logger;

        // 初始化背景并监听变化
        _backgroundImage = LoadBackgroundBitmap(settings.BackgroundImagePath);
        _backgroundImageStretch = MapStretch(settings.BackgroundImageMode);
        settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MultiButtonPromptSettings.BackgroundImagePath))
        {
            // 路径变化时在 UI 线程加载图片，避免阻塞调用方
            Dispatcher.UIThread.Post(() =>
            {
                BackgroundImage = LoadBackgroundBitmap(_settings.BackgroundImagePath);
            });
        }
        else if (e.PropertyName == nameof(MultiButtonPromptSettings.BackgroundImageMode))
        {
            Dispatcher.UIThread.Post(() =>
            {
                BackgroundImageStretch = MapStretch(_settings.BackgroundImageMode);
            });
        }
    }

    /// <summary>从绝对路径加载背景图片。失败 / 路径为空时返回 null（弹窗降级为纯色背景）。</summary>
    private static Bitmap? LoadBackgroundBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (!System.IO.File.Exists(path)) return null;
        try
        {
            return new Bitmap(path);
        }
        catch (Exception)
        {
            // 图片格式不支持或文件损坏：返回 null 让 XAML 不显示 Image
            return null;
        }
    }

    /// <summary>把枚举值映射到 Avalonia <see cref="Stretch"/>。</summary>
    private static Stretch MapStretch(int mode)
    {
        return mode switch
        {
            (int)BackgroundImageMode.Cover => Stretch.UniformToFill,
            (int)BackgroundImageMode.Contain => Stretch.Uniform,
            (int)BackgroundImageMode.Stretch => Stretch.Fill,
            // Tile 通过 TileMode 实现；这里 Stretch 仅控制单图，Tile 在 XAML 里通过 Brush 实现
            (int)BackgroundImageMode.Tile => Stretch.None,
            _ => Stretch.UniformToFill
        };
    }

    /// <summary>
    /// 弹窗请求关闭事件。Code-behind 订阅此事件来真正调用 Close()。
    /// </summary>
    public event Action? RequestClose;

    [ObservableProperty]
    private bool _isAutoExecuteActive;

    [ObservableProperty]
    private string _countdownText = string.Empty;

    [ObservableProperty]
    private int _autoExecuteHighlightIndex = -1;

    [ObservableProperty]
    private double _autoExecuteMaxSeconds;

    [ObservableProperty]
    private double _autoExecuteRemainingSeconds;

    /// <summary>
    /// 启动自动执行倒计时。倒计时归零时按指定目标触发。
    /// 必须在 ShowDialogCompat 之前调用。用户手动按按钮时自动停止。
    /// </summary>
    public void StartAutoExecuteCountdown()
    {
        if (!_settings.IsAutoExecuteEnabled) return;
        var seconds = (int)Math.Ceiling(_settings.AutoExecuteSeconds);
        if (seconds <= 0)
        {
            _logger.LogWarning("自动执行未启动：等待秒数 <= 0，seconds={Seconds}", _settings.AutoExecuteSeconds);
            return;
        }

        // idx 是 AutoExecuteTargets 的下标：0..Buttons.Count-1 = 真实按钮，Buttons.Count = "无事发生"。
        // 由于 Settings 的 OnButtonsCollectionChanged 持续夹到 [0, Buttons.Count]，到这里基本是合法的，
        // 但仍做一次防御性校验并打日志，方便日后排查配置损坏的情况。
        var idx = _settings.AutoExecuteTargetIndex;
        if (idx < 0 || idx > Buttons.Count)
        {
            _logger.LogWarning(
                "自动执行未启动：目标索引越界 idx={Idx}, Buttons.Count={Count}",
                idx, Buttons.Count);
            return;
        }

        CancelAutoExecuteCountdown();
        _autoExecuteRemaining = seconds;
        AutoExecuteHighlightIndex = idx < Buttons.Count ? idx : -1;
        AutoExecuteMaxSeconds = seconds;
        AutoExecuteRemainingSeconds = seconds;
        IsAutoExecuteActive = true;
        UpdateCountdownText();
        _autoExecuteTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _autoExecuteTimer.Tick += OnAutoExecuteTick;
        _autoExecuteTimer.Start();
    }

    /// <summary>
    /// 停止自动执行倒计时。
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
        IsAutoExecuteActive = false;
        AutoExecuteHighlightIndex = -1;
        AutoExecuteRemainingSeconds = 0;
        CountdownText = string.Empty;
    }

    /// <summary>
    /// 停止按下按钮后的交互倒计时（不触发 Action）。
    /// </summary>
    private void CancelPressCountdown()
    {
        if (_pressCountdownTimer != null)
        {
            _pressCountdownTimer.Stop();
            _pressCountdownTimer.Tick -= OnPressCountdownTick;
            _pressCountdownTimer = null;
        }
        PressCountdownButton = null;
        PressCountdownRemaining = 0;
    }

    private void OnAutoExecuteTick(object? sender, EventArgs e)
    {
        _autoExecuteRemaining--;
        if (_autoExecuteRemaining > 0)
        {
            AutoExecuteRemainingSeconds = _autoExecuteRemaining;
            UpdateCountdownText();
            return;
        }
        // 倒计时归零：触发目标
        var idx = _settings.AutoExecuteTargetIndex;
        CancelAutoExecuteCountdown();
        if (idx >= 0 && idx < Buttons.Count)
        {
            // 真实按钮：直接执行其 Action 链（不走交互倒计时逻辑，倒计时已经走完一遍了）
            _ = ExecuteButtonAsync(Buttons[idx]);
        }
        else
        {
            // idx == Buttons.Count 表示 "无事发生"：不执行任何 Action，仅关闭弹窗
            _logger.LogInformation("自动执行触发「无事发生」：不执行任何 Action 链。");
            RequestClose?.Invoke();
        }
    }

    private void UpdateCountdownText()
    {
        var idx = _settings.AutoExecuteTargetIndex;
        // 索引 == Buttons.Count 即指向末尾的"无事发生"占位；越界也按"无事发生"展示更安全。
        var name = (idx >= 0 && idx < Buttons.Count) ? Buttons[idx].Name : "无事发生";
        var minutes = _autoExecuteRemaining / 60;
        var secs = _autoExecuteRemaining % 60;
        var time = $"{minutes}分{secs}秒";
        CountdownText = $"将{time}后执行「{name}」......";
    }

    [RelayCommand]
    private async Task PressButton(MultiButtonPromptButton? button)
    {
        if (button == null) return;

        // 已在倒计时的按钮：再次点击直接忽略，避免重复触发
        if (ReferenceEquals(PressCountdownButton, button))
        {
            return;
        }

        // 任意时刻只允许一个交互倒计时在进行：如果之前有别的按钮正在倒计时，先取消
        if (PressCountdownButton is not null)
        {
            CancelPressCountdown();
        }

        // 用户主动按按钮时，取消自动执行倒计时，避免倒计时归零与用户点击并发触发。
        CancelAutoExecuteCountdown();

        // 启用了交互倒计时：进入"禁用所有按钮 N 秒"模式（仿 YesNo 对话框）。
        // 注意：交互倒计时归零时只"恢复交互"，不会自动执行该按钮的 Action。
        // 如需到时间自动执行，请使用左侧的"自动执行"功能（按指定目标触发）。
        if (button.IsCountdownEnabled)
        {
            var seconds = (int)Math.Ceiling(button.CountdownSeconds);
            if (seconds > 0)
            {
                StartPressCountdown(button, seconds);
                return;
            }
        }

        await ExecuteButtonAsync(button);
    }

    /// <summary>
    /// 启动按下按钮后的交互倒计时。倒计时期间所有按钮被禁用、当前按钮文字后显示剩余秒数
    /// （仿 YesNo 对话框：<c>defaultButton.Text = $"{defaultText} ({remainingTime:0}s)"</c>）。
    /// 倒计时归零时**只恢复交互**，不会自动执行该按钮的 Action。
    /// </summary>
    private void StartPressCountdown(MultiButtonPromptButton button, int seconds)
    {
        PressCountdownButton = button;
        PressCountdownRemaining = seconds;
        _pressCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _pressCountdownTimer.Tick += OnPressCountdownTick;
        _pressCountdownTimer.Start();
    }

    private void OnPressCountdownTick(object? sender, EventArgs e)
    {
        var btn = PressCountdownButton;
        if (btn is null || !Buttons.Contains(btn))
        {
            CancelPressCountdown();
            return;
        }
        PressCountdownRemaining--;
        if (PressCountdownRemaining > 0)
        {
            return;
        }
        // 倒计时归零：只恢复交互，不执行任何按钮的 Action。
        // 若需要"到时间自动执行"，请使用左侧"自动执行"区块。
        _logger.LogInformation("交互倒计时结束：恢复交互，按钮={Name}", btn.Name);
        CancelPressCountdown();
    }

    /// <summary>
    /// 真正执行按钮的 Action 链并请求关闭弹窗。
    /// </summary>
    private async Task ExecuteButtonAsync(MultiButtonPromptButton button)
    {
        if (button.Actions.ActionItems.Count > 0)
        {
            // 单个 Action 失败不应阻断弹窗关闭，其它 Action 也应继续。
            try
            {
                await _actionService.InvokeActionSetAsync(button.Actions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "链执行失败：按钮={Name}", button.Name);
            }
        }

        RequestClose?.Invoke();
    }

    /// <summary>
    /// 清理订阅。弹窗关闭时由 code-behind 调用，避免 Settings 持有本 VM 引用。
    /// </summary>
    public void Cleanup()
    {
        CancelAutoExecuteCountdown();
        CancelPressCountdown();
        if (_settings is not null)
        {
            _settings.PropertyChanged -= OnSettingsPropertyChanged;
        }
        _backgroundImage?.Dispose();
        _backgroundImage = null;
    }
}
