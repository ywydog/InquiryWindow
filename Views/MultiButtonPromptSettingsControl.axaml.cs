using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Shared.Helpers;
using FluentAvalonia.UI.Controls;
using InquiryWindow.Models;
using InquiryWindow.Services;

namespace InquiryWindow.Views;

public partial class MultiButtonPromptSettingsControl : ActionSettingsControlBase<MultiButtonPromptSettings>
{
    public MultiButtonPromptSettingsControl()
    {
        InitializeComponent();
    }

    private void OnAddButtonClick(object? sender, RoutedEventArgs e)
    {
        Settings.Buttons.Add(new MultiButtonPromptButton
        {
            Name = "按钮 " + (Settings.Buttons.Count + 1),
            Icon = "\uE10F"
        });
    }

    private async void OnPickIconClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: MultiButtonPromptButton target } control) return;

        var topLevel = TopLevel.GetTopLevel(control);
        if (topLevel == null) return;

        var picked = await IconPickerDialog.PickAsync(topLevel, title: "选择按钮图标", highlightGlyph: target.Icon);
        if (!string.IsNullOrEmpty(picked))
        {
            target.Icon = picked;
        }
    }

    private async void OnInsertPresetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control) return;
        if (control.Tag is not MultiButtonPromptButton target) return;

        var store = PresetsStore.Instance;
        store.Load();

        if (store.Presets.Count == 0)
        {
            await ShowEmptyDialogAsync();
            return;
        }

        // 把每个 item 的引用先存起来，等 flyout 关闭时统一解绑 Click，
        // 避免频繁开 flyout 时事件订阅累计。
        var items = store.Presets
            .Select(p => new MenuFlyoutItem
            {
                Text = p.Name,
                CommandParameter = p,
                Tag = target
            })
            .ToList();

        var flyout = new MenuFlyout();
        foreach (var item in items)
        {
            item.Click += OnPresetItemClick;
            flyout.Items.Add(item);
        }

        flyout.Closing += (_, _) =>
        {
            foreach (var item in items)
            {
                item.Click -= OnPresetItemClick;
            }
        };

        flyout.ShowAt(control);
    }

    private void OnPresetItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { CommandParameter: ButtonPreset preset, Tag: MultiButtonPromptButton target })
            return;

        // 深拷贝整条 Action 链（避免和预设共享引用，导致改一处影响全部）
        var clone = ConfigureFileHelper.CopyObject(preset.Actions);
        if (clone is null)
        {
            // 极少见：配置损坏 / CopyObject 不支持该类型
            return;
        }
        foreach (var item in clone.ActionItems)
        {
            target.Actions.ActionItems.Add(item);
        }
    }

    // ---- 按钮排序拖拽（参考 SystemTools 的实现） ----

    private const string ButtonDragDataKey = "InquiryWindow.MultiButtonPromptButton.Id";
    private const double ButtonDragThreshold = 4.0;

    private Point? _buttonDragStartPoint;
    private Border? _buttonDragSourceHandle;

    private void OnButtonDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border handle) return;
        if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;

        _buttonDragSourceHandle = handle;
        _buttonDragStartPoint = e.GetPosition(handle);
        // 触摸/笔才需要 e.Handled = true，鼠标不需要
        e.Handled = e.Pointer.Type is PointerType.Touch or PointerType.Pen;
    }

    private void OnButtonDragHandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        _buttonDragSourceHandle = null;
        _buttonDragStartPoint = null;
    }

    private async void OnButtonDragHandleMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Border handle) return;
        if (_buttonDragSourceHandle != handle || _buttonDragStartPoint is null) return;
        if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;

        var now = e.GetPosition(handle);
        if (Math.Abs(now.X - _buttonDragStartPoint.Value.X) + Math.Abs(now.Y - _buttonDragStartPoint.Value.Y) < ButtonDragThreshold)
        {
            return;
        }

        if (handle.Tag is not MultiButtonPromptButton source) return;

        // 拖动源就是被拖对象本身；不需要再回查 sender，data 包 buttonId
        var data = new DataObject();
        data.Set(ButtonDragDataKey, source);

        _buttonDragSourceHandle = null;
        _buttonDragStartPoint = null;
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
        e.Handled = e.Pointer.Type is PointerType.Touch or PointerType.Pen;
    }

    private void OnButtonListDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TryGetButtonDrag(e, out _) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnButtonListDrop(object? sender, DragEventArgs e)
    {
        if (!TryGetButtonDrag(e, out var source)) return;
        // 拖到列表空白区：移到末尾
        MoveButton(source, Settings.Buttons.Count);
    }

    private void OnButtonItemDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TryGetButtonDrag(e, out _) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnButtonItemDrop(object? sender, DragEventArgs e)
    {
        if (!TryGetButtonDrag(e, out var source)) return;
        if (sender is not Control targetControl) return;
        if (targetControl.DataContext is not MultiButtonPromptButton target) return;

        var pointerY = e.GetPosition(targetControl).Y;
        var insertIndex = pointerY > targetControl.Bounds.Height / 2
            ? Settings.Buttons.IndexOf(target) + 1
            : Settings.Buttons.IndexOf(target);

        MoveButton(source, insertIndex);
    }

    private static bool TryGetButtonDrag(DragEventArgs e, out MultiButtonPromptButton source)
    {
        source = null!;
        if (!e.Data.Contains(ButtonDragDataKey)) return false;
        if (e.Data.Get(ButtonDragDataKey) is not MultiButtonPromptButton s) return false;
        source = s;
        return true;
    }

    private void MoveButton(MultiButtonPromptButton source, int insertIndex)
    {
        var list = Settings.Buttons;
        var oldIndex = list.IndexOf(source);
        if (oldIndex < 0) return;

        // 同位置或相邻位置直接忽略
        var normalized = insertIndex > oldIndex ? insertIndex - 1 : insertIndex;
        if (normalized == oldIndex) return;

        list.Move(oldIndex, Math.Clamp(normalized, 0, list.Count - 1));
    }

    private static async Task ShowEmptyDialogAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "没有可用的预设",
            Content = "请到插件设置（InquiryWindow 设置 → 按钮预设库）里先添加按钮预设。",
            PrimaryButtonText = "确定",
            DefaultButton = ContentDialogButton.Primary
        };
        await dialog.ShowAsync();
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
