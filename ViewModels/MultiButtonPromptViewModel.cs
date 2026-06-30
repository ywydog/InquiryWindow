using System.Collections.ObjectModel;
using System.Diagnostics;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InquiryWindow.Models;

namespace InquiryWindow.ViewModels;

/// <summary>
/// 多按钮询问弹窗的 ViewModel。
/// 按下任一按钮 → 关闭弹窗并按顺序执行该按钮的 Action 链（过程不再向用户询问）。
/// </summary>
public class MultiButtonPromptViewModel : ObservableObject
{
    public ObservableCollection<MultiButtonPromptButton> Buttons { get; }

    public string Title { get; }

    public string Prompt { get; }

    public string SubPrompt { get; }

    public MultiButtonPromptViewModel(MultiButtonPromptSettings settings)
    {
        Buttons = settings.Buttons;
        Title = settings.Title;
        Prompt = settings.Prompt;
        SubPrompt = settings.SubPrompt;
    }

    /// <summary>
    /// 弹窗请求关闭事件。Code-behind 订阅此事件来真正调用 Close()。
    /// </summary>
    public event Action? RequestClose;

    [RelayCommand]
    private async Task PressButton(MultiButtonPromptButton? button)
    {
        if (button == null) return;

        var actionService = IAppHost.TryGetService<IActionService>();
        if (actionService != null && button.Actions.ActionItems.Count > 0)
        {
            // 单个 Action 失败不应阻断弹窗关闭，其它 Action 也应继续。
            try
            {
                await actionService.InvokeActionSetAsync(button.Actions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InquiryWindow] 链执行失败：{ex}");
            }
        }

        RequestClose?.Invoke();
    }
}
