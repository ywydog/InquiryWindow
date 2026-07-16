using System.Collections.ObjectModel;
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
            // 真实按钮：执行其 Action 链
            _ = PressButton(Buttons[idx]);
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

        // 用户主动按按钮时，取消倒计时，避免倒计时归零与用户点击并发触发。
        CancelAutoExecuteCountdown();

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
}
