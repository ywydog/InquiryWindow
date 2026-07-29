using System.Collections.Specialized;
using ClassIsland.Shared.Models.Automation;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InquiryWindow.Models;

/// <summary>
/// 「多结果行动」中的单个结果组：仅含名称 + Action 链。
/// 与 <see cref="MultiButtonPromptButton"/> 的区别在于没有 Icon 字段——
/// 这里的"组"是后台执行的候选，不需要展示给用户。
/// </summary>
public partial class MultiResultGroup : ObservableObject
{
    [ObservableProperty]
    private string _name = "结果";

    [ObservableProperty]
    private ActionSet _actions = new();

    public MultiResultGroup()
    {
        // 把 ActionItems 的增删改冒泡成自身的 "Actions" 变更，
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
