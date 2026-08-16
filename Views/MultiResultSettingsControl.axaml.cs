using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Controls;
using ClassIsland.Shared.Helpers;
using FluentAvalonia.UI.Controls;
using InquiryWindow.Models;
using InquiryWindow.Services;

namespace InquiryWindow.Views;

public partial class MultiResultSettingsControl : ActionSettingsControlBase<MultiResultSettings>
{
    /// <summary>
    /// XAML 中 <c>{x:Static local:MultiResultSettingsControl.OrderModes}</c> 引用，
    /// 作为「顺序模式」下拉框的 ItemsSource。
    /// </summary>
    public static readonly IReadOnlyList<MultiResultOrderMode> OrderModes = new[]
    {
        MultiResultOrderMode.Sequential,
        MultiResultOrderMode.Random,
        MultiResultOrderMode.RandomNoRepeat,
    };

    /// <summary>
    /// 把 <see cref="MultiResultOrderMode"/> 枚举值映射成中文显示文本。
    /// 注册为 XAML 静态资源（<c>{x:Static local:MultiResultSettingsControl.OrderModeConverter}</c>）。
    /// </summary>
    public static readonly OrderModeToTextConverter OrderModeConverter = new();

    /// <summary>
    /// 已订阅的 PropertyChanged 处理器：存为字段以便 OnDetachedFromVisualTree 时
    /// 能精确 -= 一次（如果用 lambda += 多次会重复挂事件）。
    /// </summary>
    private PropertyChangedEventHandler? _settingsPropertyChangedHandler;

    /// <summary>
    /// 已订阅的 Groups CollectionChanged 处理器：同上，存引用便于取消订阅。
    /// </summary>
    private NotifyCollectionChangedEventHandler? _groupsCollectionChangedHandler;

    /// <summary>
    /// 公开暴露设置对象，供 Avalonia 编译型绑定访问（基类的 Settings 是 protected，编译型
    /// 绑定无法访问，会导致 XAML 中 {Binding Settings.XXX} 全部失效）。
    /// </summary>
    public new MultiResultSettings Settings => base.Settings;

    public MultiResultSettingsControl()
    {
        // ⚠️ 不要在这里访问 Settings——基类注释明确写了「请勿在构造函数中访问」。
        // ActionSettingsControlBase.GetInstance 先构造控件、再写 SettingsInternal，
        // 所以构造函数阶段 Settings 一定是 null，会抛 ArgumentNullException。
        // 订阅 / 取消订阅统一放到 OnAttachedToVisualTree / OnDetachedFromVisualTree。
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeSettings();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeSettings();
        base.OnDetachedFromVisualTree(e);
    }

    private void SubscribeSettings()
    {
        // 防御性写法：通常 OnAttachedToVisualTree 时 Settings 已被 GetInstance 赋值。
        // 如果 SettingsInternal 还是 null（控件在显示前就被 detach 等极端情况），
        // 直接跳过，等下次 attach 再试。
        // 注意：SettingsInternal 是 internal，外部程序集只能通过 Settings 访问。
        // 但 Settings 抛 ArgumentNullException 的语义是「过早访问」，
        // OnAttachedToVisualTree 时它一定就绪了，所以这里直接用 Settings。
        if (_settingsPropertyChangedHandler != null) return;

        MultiResultSettings settings;
        try
        {
            settings = Settings;
        }
        catch (ArgumentNullException)
        {
            return; // 还没就绪
        }

        _settingsPropertyChangedHandler = OnSettingsPropertyChanged;
        _groupsCollectionChangedHandler = (_, _) => ResetShuffleQueue();

        settings.PropertyChanged += _settingsPropertyChangedHandler;
        settings.Groups.CollectionChanged += _groupsCollectionChangedHandler;
    }

    private void UnsubscribeSettings()
    {
        if (_settingsPropertyChangedHandler == null || _groupsCollectionChangedHandler == null) return;

        // OnDetachedFromVisualTree 时 Settings 必然就绪（GetInstance 流程）
        // —— 真要抛就让它抛，总比悄悄漏事件好。
        var settings = Settings;
        settings.PropertyChanged -= _settingsPropertyChangedHandler;
        settings.Groups.CollectionChanged -= _groupsCollectionChangedHandler;
        _settingsPropertyChangedHandler = null;
        _groupsCollectionChangedHandler = null;
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MultiResultSettings.OrderMode))
        {
            // 切走 / 切回 都要把游标归零
            ResetShuffleQueue();
        }
    }

    /// <summary>
    /// 让「随机不重复」的下次触发从一轮新的洗牌开始。
    /// </summary>
    private void ResetShuffleQueue()
    {
        Settings.RemainingOrder.Clear();
        Settings.Cursor = 0;
    }

    private void OnAddGroupClick(object? sender, RoutedEventArgs e)
    {
        Settings.Groups.Add(new MultiResultGroup
        {
            Name = "结果 " + (Settings.Groups.Count + 1)
        });
    }

    private void OnRemoveGroupClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: MultiResultGroup target } control) return;
        Settings.Groups.Remove(target);

        // 修正 LastIndex：删除一个组后索引可能越界。
        if (Settings.Groups.Count == 0)
        {
            Settings.LastIndex = -1;
        }
        else if (Settings.LastIndex >= Settings.Groups.Count)
        {
            Settings.LastIndex = Settings.Groups.Count - 1;
        }
    }

    // ---- 结果组排序拖拽（参考 MultiButtonPromptSettingsControl 的实现） ----

    private const string GroupDragDataKey = "InquiryWindow.MultiResultGroup";
    private const double GroupDragThreshold = 4.0;

    private Point? _groupDragStartPoint;
    private Border? _groupDragSourceHandle;
    private PointerPressedEventArgs? _groupDragPressedArgs;

    private void OnGroupDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border handle) return;
        if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;

        _groupDragSourceHandle = handle;
        _groupDragStartPoint = e.GetPosition(handle);
        _groupDragPressedArgs = e;
        // 触摸/笔才需要 e.Handled = true，鼠标不需要
        e.Handled = e.Pointer.Type is PointerType.Touch or PointerType.Pen;
    }

    private void OnGroupDragHandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        _groupDragSourceHandle = null;
        _groupDragStartPoint = null;
        _groupDragPressedArgs = null;
    }

    private async void OnGroupDragHandleMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Border handle) return;
        if (_groupDragSourceHandle != handle || _groupDragStartPoint is null) return;
        if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;

        var now = e.GetPosition(handle);
        if (Math.Abs(now.X - _groupDragStartPoint.Value.X) + Math.Abs(now.Y - _groupDragStartPoint.Value.Y) < GroupDragThreshold)
        {
            return;
        }

        if (handle.Tag is not MultiResultGroup source) return;

        // 拖动源就是被拖对象本身；data 直接包对象引用
        var format = DataFormat.CreateInProcessFormat<MultiResultGroup>(GroupDragDataKey);
        var item = new DataTransferItem();
        item.Set(format, source);
        var dataTransfer = new DataTransfer();
        dataTransfer.Add(item);

        // Avalonia 12 的 DoDragDropAsync 需要原始的 PointerPressedEventArgs
        var pressedArgs = _groupDragPressedArgs;
        _groupDragSourceHandle = null;
        _groupDragStartPoint = null;
        _groupDragPressedArgs = null;
        if (pressedArgs != null)
        {
            await DragDrop.DoDragDropAsync(pressedArgs, dataTransfer, DragDropEffects.Move);
        }
        e.Handled = e.Pointer.Type is PointerType.Touch or PointerType.Pen;
    }

    private void OnGroupListDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TryGetGroupDrag(e, out _) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnGroupListDrop(object? sender, DragEventArgs e)
    {
        if (!TryGetGroupDrag(e, out var source)) return;
        // 拖到列表空白区：移到末尾
        MoveGroup(source, Settings.Groups.Count);
    }

    private void OnGroupItemDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = TryGetGroupDrag(e, out _) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnGroupItemDrop(object? sender, DragEventArgs e)
    {
        if (!TryGetGroupDrag(e, out var source)) return;
        if (sender is not Control targetControl) return;
        if (targetControl.DataContext is not MultiResultGroup target) return;

        // 按指针 Y 落在目标卡片的上半 / 下半决定插入到目标前还是后
        var pointerY = e.GetPosition(targetControl).Y;
        var insertIndex = pointerY > targetControl.Bounds.Height / 2
            ? Settings.Groups.IndexOf(target) + 1
            : Settings.Groups.IndexOf(target);

        MoveGroup(source, insertIndex);
    }

    private static bool TryGetGroupDrag(DragEventArgs e, out MultiResultGroup source)
    {
        source = null!;
        var format = DataFormat.CreateInProcessFormat<MultiResultGroup>(GroupDragDataKey);
        if (!e.DataTransfer.Contains(format)) return false;
        if (e.DataTransfer.TryGetValue(format) is not { } s) return false;
        source = s;
        return true;
    }

    /// <summary>
    /// 把 <paramref name="source"/> 移动到 <paramref name="insertIndex"/>。
    /// 索引经过规范化：拖到比原位置更后的位置时，因为 Move 会先把原位置抹掉，
    /// 需要把 insertIndex - 1 修正回期望位置。
    /// </summary>
    private void MoveGroup(MultiResultGroup source, int insertIndex)
    {
        var list = Settings.Groups;
        var oldIndex = list.IndexOf(source);
        if (oldIndex < 0) return;

        var normalized = insertIndex > oldIndex ? insertIndex - 1 : insertIndex;
        if (normalized == oldIndex) return;

        list.Move(oldIndex, Math.Clamp(normalized, 0, list.Count - 1));
    }

    private async void OnInsertPresetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: MultiResultGroup target } control) return;

        var store = PresetsStore.Instance;
        store.Load();

        if (store.Presets.Count == 0)
        {
            await ShowEmptyDialogAsync();
            return;
        }

        var selected = await ShowPresetPickerDialogAsync(store.Presets);
        if (selected is null) return;

        // 深拷贝整条 Action 链（避免和预设共享引用）
        var clone = ConfigureFileHelper.CopyObject(selected.Actions);
        if (clone is null) return;
        foreach (var item in clone.ActionItems)
        {
            target.Actions.ActionItems.Add(item);
        }
    }

    private static async Task ShowEmptyDialogAsync()
    {
        var dialog = new FAContentDialog
        {
            Title = "没有可用的预设",
            Content = "请到插件设置（InquiryWindow 设置 → 按钮预设库）里先添加按钮预设。",
            PrimaryButtonText = "确定",
            DefaultButton = FAContentDialogButton.Primary
        };
        await dialog.ShowAsync();
    }

    /// <summary>
    /// 用 FAContentDialog + ListBox 显示预设选择器。
    /// 实现与 MultiButtonPromptSettingsControl 中同名方法一致：FAContentDialog 走完整
    /// 可视树，避免 FluentAvalonia MenuFlyout 在代码创建 + 鼠标 hover 时的 NRE 问题。
    /// </summary>
    private static async Task<ButtonPreset?> ShowPresetPickerDialogAsync(
        IReadOnlyList<ButtonPreset> presets)
    {
        var listBox = new ListBox
        {
            ItemsSource = presets,
            MaxHeight = 360,
            MinWidth = 320,
            ItemTemplate = new FuncDataTemplate<ButtonPreset>((p, _) =>
            {
                if (p is null) return new TextBlock();
                var row = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 10
                };
                row.Children.Add(new FluentIcon
                {
                    Glyph = p.Icon,
                    FontSize = 16,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                });
                row.Children.Add(new TextBlock
                {
                    Text = p.Name,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                });
                return row;
            })
        };

        var dialog = new FAContentDialog
        {
            Title = "选择要插入的预设",
            Content = listBox,
            PrimaryButtonText = "插入",
            SecondaryButtonText = "取消",
            IsPrimaryButtonEnabled = false,
            DefaultButton = FAContentDialogButton.Primary
        };

        listBox.SelectionChanged += (_, _) =>
        {
            dialog.IsPrimaryButtonEnabled = listBox.SelectedItem is ButtonPreset;
        };
        listBox.DoubleTapped += (_, _) =>
        {
            if (listBox.SelectedItem is ButtonPreset)
            {
                dialog.Hide(FAContentDialogResult.Primary);
            }
        };

        var result = await dialog.ShowAsync();
        if (result != FAContentDialogResult.Primary) return null;
        return listBox.SelectedItem as ButtonPreset;
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}

/// <summary>
/// <see cref="MultiResultOrderMode"/> 枚举到中文文本的转换器。
/// 在 XAML 中通过 <c>{x:Static local:MultiResultSettingsControl.OrderModeConverter}</c> 引用。
/// </summary>
public class OrderModeToTextConverter : Avalonia.Data.Converters.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is MultiResultOrderMode mode)
        {
            return mode switch
            {
                MultiResultOrderMode.Sequential     => "顺序",
                MultiResultOrderMode.Random         => "随机",
                MultiResultOrderMode.RandomNoRepeat => "随机不重复",
                _ => mode.ToString(),
            };
        }
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        // ComboBox 的 SelectedItem 是直接绑定枚举值，无需回转
        return Avalonia.Data.BindingOperations.DoNothing;
    }
}
