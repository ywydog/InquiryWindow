using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Controls;
using ClassIsland.Core.Controls.Automation;
using ClassIsland.Shared.Helpers;
using ClassIsland.Shared.Models.Automation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using InquiryWindow.Models;
using InquiryWindow.Services;
using InquiryWindow.Views;

namespace InquiryWindow.ViewModels;

/// <summary>
/// 按钮预设库设置页的 ViewModel：
/// 借鉴 SuperAutoIsland 「可复用行动组」的设计理念 —— 左侧列表（项目集），
/// 右侧详情（项目元数据 + Action 链）。
/// </summary>
public partial class ButtonPresetSettingsViewModel : ObservableObject
{
    /// <summary>预设库全量数据（已加载的），由 <see cref="PresetsStore"/> 持有。</summary>
    public ObservableCollection<ButtonPreset> Presets => PresetsStore.Instance.Presets;

    [ObservableProperty]
    private ButtonPreset? _selectedPreset;

    /// <summary>在右侧详情区是否处于压缩模式（窄屏）。</summary>
    [ObservableProperty]
    private bool _isDetailPanelOpen;

    /// <summary>
    /// 单调递增的预设序号，避免删除中间预设后命名撞车。
    /// </summary>
    private int _nextPresetNumber = 1;

    public ButtonPresetSettingsViewModel()
    {
        // 保证预设库从磁盘加载（如果 Plugin.Initialize 之前没跑过）。
        PresetsStore.Instance.Load();

        // 选中即打开右侧详情面板（类似 SuperAutoIsland 的 SelectionChanged → IsPanelOpened）。
        // 用命名方法而非 lambda，便于 Dispose 时精确取消订阅，避免静态单例持有本 VM 导致泄漏。
        PresetsStore.Instance.Presets.CollectionChanged += OnPresetsCollectionChanged;
        RefreshNextNumber();
    }

    private void OnPresetsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshNextNumber();

    /// <summary>
    /// 取消对静态 <see cref="PresetsStore"/> 集合的订阅。
    /// 由设置页卸载时调用，避免 VM 被静态单例持有的集合事件永久引用。
    /// </summary>
    public void Dispose()
    {
        PresetsStore.Instance.Presets.CollectionChanged -= OnPresetsCollectionChanged;
    }

    /// <summary>
    /// 「新建预设」：按当前序号起名，插入到 PresetsStore，并自动选中新行。
    /// 借鉴 SuperAutoIsland AutomationSettingsPage.CreateProject 的契约。
    /// </summary>
    [RelayCommand]
    public void AddPreset()
    {
        var name = "新预设 " + _nextPresetNumber;
        _nextPresetNumber++;
        var preset = PresetsStore.Instance.AddPreset(name, "\uE10F");
        SelectedPreset = preset;
    }

    /// <summary>
    /// 「删除预设」：弹窗确认后从 PresetsStore 移除。
    /// </summary>
    [RelayCommand]
    public async Task RemovePresetAsync(TopLevel? topLevel)
    {
        var preset = SelectedPreset;
        if (preset == null || topLevel == null) return;

        var dialog = new FAContentDialog
        {
            Title = "删除预设？",
            Content = $"确定删除预设「{preset.Name}」？已插入到按钮里的 Action 链不受影响。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Close
        };
        var result = await dialog.ShowAsync(topLevel);
        if (result == FAContentDialogResult.Primary)
        {
            PresetsStore.Instance.RemovePreset(preset);
            SelectedPreset = null;
        }
    }

    /// <summary>
    /// 「编辑名称/图标」：右侧就地编辑。Action 链通过独立的 <see cref="BeginEditActionsAsync"/>
    /// 弹窗维护，避免 ActionControl 长期嵌入设置页导致首屏卡顿。
    /// </summary>
    [RelayCommand]
    public void CommitMetaEdit()
    {
        if (SelectedPreset is null) return;
        if (string.IsNullOrWhiteSpace(SelectedPreset.Name))
        {
            SelectedPreset.Name = "未命名预设";
        }
        if (string.IsNullOrWhiteSpace(SelectedPreset.Icon))
        {
            SelectedPreset.Icon = "\uE10F";
        }
        // Name/Icon 字段均会通过 INPC 触发 PresetsStore 的 debounce save，
        // 显式 SaveNow 兜底，避免用户编辑完立刻关 app 丢数据。
        PresetsStore.Instance.SaveNow();
    }

    /// <summary>
    /// 「编辑 Action 链…」：弹出 FAContentDialog，承载 ActionControl 让用户调整链内容。
    /// 关键：克隆一份 ActionSet 给 ActionControl 编辑，「取消」时改动随 workingActions
    /// 一起被丢弃，preset.Actions 不被污染。
    /// </summary>
    [RelayCommand]
    public async Task BeginEditActionsAsync(TopLevel? topLevel)
    {
        var preset = SelectedPreset;
        if (preset is null || topLevel is null) return;

        var workingActions = ConfigureFileHelper.CopyObject(preset.Actions);
        var actionControl = new ActionControl
        {
            ActionSet = workingActions
        };

        var dialog = new FAContentDialog
        {
            Title = $"编辑「{preset.Name}」的 Action 链",
            Content = actionControl,
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = FAContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync(topLevel);
        if (result != FAContentDialogResult.Primary) return;

        preset.Actions = workingActions;
        PresetsStore.Instance.SaveNow();
    }

    /// <summary>
    /// 「选择图标」按钮的弹窗：复刻 InquiryWindowSettingsPage 的行为。
    /// </summary>
    [RelayCommand]
    public async Task PickIconAsync(TopLevel? topLevel)
    {
        var preset = SelectedPreset;
        if (preset is null || topLevel is null) return;

        var picked = await IconPickerDialog.PickAsync(topLevel, title: "选择预设图标", highlightGlyph: preset.Icon);
        if (!string.IsNullOrEmpty(picked))
        {
            preset.Icon = picked;
        }
    }

    private void RefreshNextNumber()
    {
        // 扫描现有预设名中「新预设 N」里的最大 N，作为下次新建的起点。
        var max = 0;
        foreach (var p in Presets)
        {
            if (p.Name.StartsWith("新预设 ") &&
                int.TryParse(p.Name.AsSpan("新预设 ".Length), out var n))
            {
                if (n > max) max = n;
            }
        }
        _nextPresetNumber = max + 1;
    }

    /// <summary>选中项变化时打开右侧详情面板。</summary>
    partial void OnSelectedPresetChanged(ButtonPreset? value)
    {
        IsDetailPanelOpen = value is not null;
    }
}
