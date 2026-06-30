using System.Collections.Specialized;
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

    [ObservableProperty]
    private ActionSet _actions = new();

    public MultiButtonPromptButton()
    {
        // 同样把 ActionItems 的增删改冒泡成自身的 "Actions" 变更，
        // 让 ActionSettingsControlBase 能正确检测到设置脏。
        _actions.ActionItems.CollectionChanged += OnActionItemsChanged;
    }

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
        OnPropertyChanged(nameof(Actions));
    }

    private void OnActionItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Actions));
    }
}
