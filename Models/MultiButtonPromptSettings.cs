using System.Collections.ObjectModel;

namespace InquiryWindow.Models;

/// <summary>
/// 多按钮询问 Action 的设置。
/// </summary>
public class MultiButtonPromptSettings
{
    /// <summary>OS 窗口标题栏文字。</summary>
    public string Title { get; set; } = "ClassIsland - 询问";

    /// <summary>窗口内主提示文本。</summary>
    public string Prompt { get; set; } = "请选择";

    /// <summary>窗口内副提示文本（可空）。</summary>
    public string SubPrompt { get; set; } = string.Empty;

    /// <summary>按钮集合（按显示顺序）。</summary>
    public ObservableCollection<MultiButtonPromptButton> Buttons { get; set; } = new();

    public MultiButtonPromptSettings()
    {
        Buttons.Add(new MultiButtonPromptButton { Name = "执行", Icon = "\uE73E" });
        Buttons.Add(new MultiButtonPromptButton { Name = "取消", Icon = "\uE711" });
    }
}
