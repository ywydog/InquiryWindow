using CommunityToolkit.Mvvm.ComponentModel;
using ClassIsland.Shared.Models.Automation;

namespace InquiryWindow.Models;

/// <summary>
/// 单个按钮：显示名 + 图标 + 触发时执行的 Action 链。
/// </summary>
public partial class MultiButtonPromptButton : ObservableObject
{
    [ObservableProperty]
    private string _name = "按钮";

    /// <summary>Fluent 系统字体图标 glyph（Unicode 字符）。</summary>
    [ObservableProperty]
    private string _icon = "\uE10F";

    /// <summary>按下按钮后依次执行的 Action 链。</summary>
    [ObservableProperty]
    private ActionSet _actions = new();
}
