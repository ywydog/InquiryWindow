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

public partial class MultiButtonPromptSettingsControl : ActionSettingsControlBase<MultiButtonPromptSettings>
{
    /// <summary>
    /// 公开暴露设置对象，供 Avalonia 编译型绑定访问（基类的 Settings 是 protected，编译型
    /// 绑定无法访问，会导致 XAML 中 {Binding Settings.XXX} 全部失效）。
    /// </summary>
    public new MultiButtonPromptSettings Settings => base.Settings;

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

    /// <summary>
    /// 参照 <c>YesNoDialogRuleSettingsControl.ShowSettingsButton_OnClick</c> 的触发模式，
    /// 这里把抽屉内容换成"独立窗口"（多按钮询问行动 · 详细设置）。
    /// 窗口会与本设置共享同一个 <see cref="MultiButtonPromptSettings"/> 实例，
    /// 所以窗口内的改动会立刻反映回抽屉。
    /// </summary>
    private async void OnOpenDetailWindowClick(object? sender, RoutedEventArgs e)
    {
        // Avalonia Android 的 WindowingPlatformStub 不支持创建子窗口（FAAppWindow
        // 走的是 Window..ctor → PlatformManager.CreateWindow → Android stub 直接抛
        // NotSupportedException），所以在 Android 平台上禁用"打开详细设置"按钮
        // 的弹窗行为，改用 ContentDialog 提示用户该功能在 Android 暂不可用。
        if (OperatingSystem.IsAndroid())
        {
            var dialog = new FAContentDialog
            {
                Title = "暂不可用",
                Content = "「多按钮询问行动 · 详细设置」需要打开独立窗口进行编辑，目前在 Android 平台暂不支持。请直接在本设置面板中编辑按钮（点击下方按钮的 Expander 展开后可重命名、改图标、插入预设）。",
                PrimaryButtonText = "好的",
                DefaultButton = FAContentDialogButton.Primary
            };
            await dialog.ShowAsync();
            return;
        }

        var win = new MultiButtonPromptDetailWindow(Settings);
        await win.ShowDialogCompat();
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

        // 用 ContentDialog + ListBox 代替 MenuFlyout：FluentAvalonia 的
        // MenuFlyoutItemBase.OnPointerEntered 在代码创建 + 鼠标 hover 时
        // 会因模板上下文未就绪而抛 NullReferenceException（ClassIsland.App 日志可见）。
        var selected = await ShowPresetPickerDialogAsync(store.Presets);
        if (selected is null) return;

        // 深拷贝整条 Action 链（避免和预设共享引用，导致改一处影响全部）
        var clone = ConfigureFileHelper.CopyObject(selected.Actions);
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

    private async void OnPreviewPromptClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        await MarkdownPreviewDialog.ShowAsync(
            topLevel,
            title: "主提示预览",
            markdown: Settings.Prompt ?? "");
    }

    private async void OnPreviewSubPromptClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        await MarkdownPreviewDialog.ShowAsync(
            topLevel,
            title: "副提示预览",
            markdown: Settings.SubPrompt ?? "");
    }

    // ---- 按钮排序（手动指针拖拽） ----
    // 注：Android 的 AOT 会裁剪 DataFormat.CreateInProcessFormat<T>/DragDrop，运行时会崩溃，
    // 因此这里不依赖系统拖拽，改用指针捕获 + 实时 Move 的自实现排序。

    private const double ButtonDragThreshold = 4.0;

    private bool _buttonDragging;
    private Point? _buttonDragStart;
    private MultiButtonPromptButton? _buttonDragSource;

    private void OnButtonDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border handle) return;
        if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;
        if (handle.Tag is not MultiButtonPromptButton source) return;

        _buttonDragSource = source;
        _buttonDragStart = e.GetPosition(handle);
        _buttonDragging = false;
        // 捕获指针，保证拖动期间即使移出手柄也能持续收到 PointerMoved。
        e.Pointer.Capture(handle);
        e.Handled = e.Pointer.Type is PointerType.Touch or PointerType.Pen;
    }

    private void OnButtonDragHandleMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Border handle) return;
        if (_buttonDragSource is null) return;

        var now = e.GetPosition(handle);
        if (!_buttonDragging)
        {
            if (_buttonDragStart is null) return;
            if (Math.Abs(now.X - _buttonDragStart.Value.X) + Math.Abs(now.Y - _buttonDragStart.Value.Y) < ButtonDragThreshold)
            {
                return;
            }
            _buttonDragging = true;
        }

        var items = handle.FindAncestorOfType<ItemsControl>();
        if (items is null) return;
        var pt = e.GetPosition(items);
        if (TryGetHoveredButton(items, pt, out var target))
        {
            TryMoveButtonTo(_buttonDragSource, target);
        }
        e.Handled = e.Pointer.Type is PointerType.Touch or PointerType.Pen;
    }

    private void OnButtonDragHandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Border handle)
        {
            e.Pointer.Capture(null);
        }
        _buttonDragSource = null;
        _buttonDragStart = null;
        _buttonDragging = false;
    }

    /// <summary>从悬停点向上查找命中的按钮数据（类比 InputHitTest 后沿可视树取 DataContext）。</summary>
    private static bool TryGetHoveredButton(ItemsControl host, Point pt, out MultiButtonPromptButton target)
    {
        target = null!;
        var hit = host.InputHitTest(pt);
        var node = hit as Visual;
        while (node is not null)
        {
            if (node is Control c && c.DataContext is MultiButtonPromptButton b)
            {
                target = b;
                return true;
            }
            node = node.GetVisualParent();
        }
        return false;
    }

    /// <summary>把源按钮移动到目标按钮所在位置（交换式实时排序）。</summary>
    private bool TryMoveButtonTo(MultiButtonPromptButton source, MultiButtonPromptButton target)
    {
        var list = Settings.Buttons;
        var oldIndex = list.IndexOf(source);
        var targetIndex = list.IndexOf(target);
        if (oldIndex < 0 || targetIndex < 0 || oldIndex == targetIndex) return false;
        list.Move(oldIndex, targetIndex);
        return true;
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
    /// 之所以不直接复用 MenuFlyout：FluentAvalonia.MenuFlyoutItemBase.OnPointerEntered
    /// 在代码创建 + 鼠标 hover 时会因模板上下文未就绪而抛 NullReferenceException
    /// （ClassIsland.App 日志可见），FAContentDialog 走的是完整可视树，无此问题。
    /// </summary>
    private static async Task<ButtonPreset?> ShowPresetPickerDialogAsync(
        System.Collections.Generic.IReadOnlyList<ButtonPreset> presets)
    {
        var listBox = new ListBox
        {
            ItemsSource = presets,
            MaxHeight = 360,
            MinWidth = 320,
            // 用 DataTemplate 让每一行展示图标 + 名称
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

        // 只有选中一行后才能点"插入"
        listBox.SelectionChanged += (_, _) =>
        {
            dialog.IsPrimaryButtonEnabled = listBox.SelectedItem is ButtonPreset;
        };
        // 注意：Android 的 AOT 会裁剪 FAContentDialog.Hide，因此这里不再提供
        // "双击直接插入并关闭"的快捷操作，统一走下方主按钮"插入"来关闭。

        var result = await dialog.ShowAsync();
        if (result != FAContentDialogResult.Primary) return null;
        return listBox.SelectedItem as ButtonPreset;
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
