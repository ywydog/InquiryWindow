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

    /// <summary>
    /// 首次创建时给一个"执行/取消"模板，让用户能直接看到可工作的最小配置。
    /// 加载旧数据时反序列化器会用 JSON 里的值覆盖，不会保留这两条默认按钮。
    /// </summary>
    public MultiButtonPromptSettings()
    {
        Buttons.Add(new MultiButtonPromptButton { Name = "执行", Icon = "\uE73E" });
        Buttons.Add(new MultiButtonPromptButton { Name = "取消", Icon = "\uE711" });
    }
}
