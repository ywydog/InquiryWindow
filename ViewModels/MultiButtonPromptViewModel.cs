using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared;
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

    /// <summary>交互倒计时计时器（弹窗打开后统一递减各按钮的剩余锁定秒数）。</summary>
    private DispatcherTimer? _interactionCountdownTimer;

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

    // ===== 自动执行倒计时进度条样式（来自 Settings.CountdownBarStyleIndex） =====
    /// <summary>进度条样式索引：0=线性条，1=环形，2=隐藏。</summary>
    public int CountdownBarStyleIndex => _settings.CountdownBarStyleIndex;

    /// <summary>是否使用线性进度条样式。</summary>
    public bool IsLineProgressStyle => CountdownBarStyleIndex == 0;

    /// <summary>是否使用环形（圆环）进度样式。</summary>
    public bool IsRingProgressStyle => CountdownBarStyleIndex == 1;

    /// <summary>是否隐藏进度指示（仅显示倒计时文字）。</summary>
    public bool IsNoneProgressStyle => CountdownBarStyleIndex == 2;

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
        else if (e.PropertyName == nameof(MultiButtonPromptSettings.CountdownBarStyleIndex))
        {
            OnPropertyChanged(nameof(CountdownBarStyleIndex));
            OnPropertyChanged(nameof(IsLineProgressStyle));
            OnPropertyChanged(nameof(IsRingProgressStyle));
            OnPropertyChanged(nameof(IsNoneProgressStyle));
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
    /// 启动交互倒计时：弹窗显示前调用。为已启用「交互倒计时」的按钮设置初始锁定秒数，
    /// 之后每秒统一递减。锁定期内按钮不可点击（UI 呈暗淡的禁用态）并显示「(Ns)」，
    /// 归零后自动恢复可点击。未启用交互倒计时的按钮不受影响。
    /// </summary>
    public void StartInteractionCountdowns()
    {
        CancelInteractionCountdowns();

        var anyLocked = false;
        foreach (var button in Buttons)
        {
            var seconds = button.IsCountdownEnabled ? (int)Math.Ceiling(button.CountdownSeconds) : 0;
            button.CountdownRemaining = Math.Max(0, seconds);
            if (button.CountdownRemaining > 0)
            {
                anyLocked = true;
                _logger.LogInformation("交互倒计时开始：按钮 \"{Name}\" 锁定 {Seconds} 秒。",
                    button.Name, button.CountdownRemaining);
            }
        }

        if (!anyLocked) return;
        _interactionCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _interactionCountdownTimer.Tick += OnInteractionCountdownTick;
        _interactionCountdownTimer.Start();
    }

    /// <summary>停止交互倒计时并解除所有按钮的锁定状态（不执行任何 Action）。</summary>
    private void CancelInteractionCountdowns()
    {
        if (_interactionCountdownTimer != null)
        {
            _interactionCountdownTimer.Stop();
            _interactionCountdownTimer.Tick -= OnInteractionCountdownTick;
            _interactionCountdownTimer = null;
        }
        foreach (var button in Buttons)
        {
            button.CountdownRemaining = 0;
        }
    }

    private void OnInteractionCountdownTick(object? sender, EventArgs e)
    {
        var anyLocked = false;
        foreach (var button in Buttons)
        {
            if (button.CountdownRemaining <= 0) continue;
            button.CountdownRemaining--;
            if (button.CountdownRemaining > 0)
            {
                anyLocked = true;
            }
            else
            {
                _logger.LogInformation("交互倒计时结束：按钮恢复可点击，按钮={Name}", button.Name);
            }
        }
        if (!anyLocked)
        {
            CancelInteractionCountdowns();
        }
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
            _logger.LogInformation("自动执行倒计时归零，触发按钮 \"{Name}\" 的 Action 链。", Buttons[idx].Name);
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

        // 交互倒计时未结束时按钮处于锁定态（UI 已禁用），这里再做一次防御性拦截。
        if (button.CountdownRemaining > 0) return;

        // 用户主动按按钮时，取消自动执行倒计时，避免倒计时归零与用户点击并发触发。
        CancelAutoExecuteCountdown();

        await ExecuteButtonAsync(button);
    }

    /// <summary>
    /// 真正执行按钮的 Action 链并请求关闭弹窗。
    /// 若该按钮启用了「操作验证」，会先调用 AuthorizeService 进行身份验证，验证通过才执行 Action 链。
    /// </summary>
    private async Task ExecuteButtonAsync(MultiButtonPromptButton button)
    {
        // 操作验证：启用且已配置凭据时，先进行身份验证。
        if (button.IsOperationVerification && !string.IsNullOrWhiteSpace(button.CredentialString))
        {
            var authorized = await AuthorizeButtonAsync(button);
            if (!authorized)
            {
                if (button.VerificationFailureBehavior == VerificationFailureBehavior.CloseDialog)
                {
                    // 验证失败且配置为"关闭弹窗"：不执行 Action，直接关闭。
                    _logger.LogInformation("操作验证失败，按配置关闭弹窗，按钮={Button}", button.Name);
                    RequestClose?.Invoke();
                }
                else
                {
                    // 验证失败且配置为"保持打开"：不执行 Action，留在弹窗让用户重试/改选。
                    _logger.LogInformation("操作验证失败，弹窗保持打开，按钮={Button}", button.Name);
                }
                return;
            }
        }

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
    /// 调用 ClassIsland 的 AuthorizeService 验证当前按钮的凭据。
    /// 验证需要弹出 AuthorizeWindow（桌面端 Window），因此仅在桌面平台可用；
    /// Android 分支本就不走本 ViewModel 的窗口路径。
    /// </summary>
    private async Task<bool> AuthorizeButtonAsync(MultiButtonPromptButton button)
    {
        // 操作验证是安全功能，采用 fail-closed 语义：服务不可用时视为验证失败，
        // 拒绝执行该按钮的 Action 链，而不是静默放行（避免"以为验证了却绕过"）。
        var authorizeService = IAppHost.TryGetService<IAuthorizeService>();
        if (authorizeService is null)
        {
            _logger.LogError("操作验证无法进行：AuthorizeService 不可用，按验证失败处理，按钮={Button}", button.Name);
            return false;
        }

        try
        {
            // owner 传主窗口；TopLevel 适配（AuthorizeService 内部会回退到根窗口）。
            var owner = AppBase.Current.GetRootWindow() as Window;
            return await authorizeService.AuthenticateAsync(button.CredentialString, owner);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "调用 AuthorizeService 进行操作验证时发生错误，按钮={Button}", button.Name);
            return false;
        }
    }

    /// <summary>
    /// 清理订阅。弹窗关闭时由 code-behind 调用，避免 Settings 持有本 VM 引用。
    /// </summary>
    public void Cleanup()
    {
        CancelAutoExecuteCountdown();
        CancelInteractionCountdowns();
        if (_settings is not null)
        {
            _settings.PropertyChanged -= OnSettingsPropertyChanged;
        }
        _backgroundImage?.Dispose();
        _backgroundImage = null;
    }
}
