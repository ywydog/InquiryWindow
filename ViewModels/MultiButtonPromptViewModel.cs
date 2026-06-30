using System.Collections.ObjectModel;
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

    public MultiButtonPromptViewModel(
        MultiButtonPromptSettings settings,
        IActionService actionService,
        ILogger<MultiButtonPromptViewModel> logger)
    {
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

    [RelayCommand]
    private async Task PressButton(MultiButtonPromptButton? button)
    {
        if (button == null) return;

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
