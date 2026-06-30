using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ClassIsland.Shared.Models.Automation;

namespace InquiryWindow.Models;

/// <summary>
/// 按钮预设：可在插件 ViewPage 中管理，被多按钮询问的按钮引用插入。
/// </summary>
public partial class ButtonPreset : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _name = "未命名预设";

    /// <summary>Fluent 系统字体图标 glyph（Unicode 字符）。</summary>
    [ObservableProperty]
    private string _icon = "\uE10F";

    /// <summary>预设里包含的 Action 链。</summary>
    [ObservableProperty]
    private ActionSet _actions = new();
}

/// <summary>
/// 预设库 JSON 根对象。
/// </summary>
public class PresetsData
{
    public ObservableCollection<ButtonPreset> Presets { get; set; } = new();
}
