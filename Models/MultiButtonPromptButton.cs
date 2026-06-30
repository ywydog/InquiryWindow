using ClassIsland.Shared.Models.Automation;

namespace InquiryWindow.Models;

/// <summary>
/// 单个按钮：显示名 + 图标 + 触发时执行的 Action 链。
/// </summary>
public class MultiButtonPromptButton
{
    /// <summary>按钮上显示的文本。</summary>
    public string Name { get; set; } = "按钮";

    /// <summary>Fluent 系统字体图标 glyph（Unicode 字符）。</summary>
    public string Icon { get; set; } = "\uE10F";

    /// <summary>按下按钮后依次执行的 Action 链。</summary>
    public ActionSet Actions { get; set; } = new();
}
