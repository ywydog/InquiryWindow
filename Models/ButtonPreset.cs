using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

    [ObservableProperty]
    private ActionSet _actions = new();

    public ButtonPreset()
    {
        // 初始订阅：让 ActionItems 的增删改能冒泡成自身的 "Actions" 变更，
        // 触发 PresetsStore 的自动保存。
        _actions.ActionItems.CollectionChanged += OnActionItemsChanged;
    }

    /// <summary>
    /// 当 Actions 被整体替换（重新赋值）时，重新订阅新 ActionSet 的 ActionItems 变化。
    /// </summary>
    partial void OnActionsChanged(ActionSet? oldValue, ActionSet newValue)
    {
        if (oldValue is not null)
        {
            oldValue.ActionItems.CollectionChanged -= OnActionItemsChanged;
        }
        if (newValue is not null)
        {
            newValue.ActionItems.CollectionChanged += OnActionItemsChanged;
        }
        // 替换 ActionSet 时把变更冒泡出去，让 PresetsStore 立即落盘一次。
        OnPropertyChanged(nameof(Actions));
    }

    private void OnActionItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Actions));
    }
}

/// <summary>
/// 预设库 JSON 根对象。
/// </summary>
public class PresetsData
{
    public ObservableCollection<ButtonPreset> Presets { get; set; } = new();
}
