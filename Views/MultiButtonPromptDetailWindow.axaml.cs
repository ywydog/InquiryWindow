using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Core.Controls;
using InquiryWindow.Models;
using InquiryWindow.Views;

namespace InquiryWindow.Views;

/// <summary>
/// 多按钮询问行动 · 详细设置（独立窗口）。
/// 参照 <c>multi-button-prompt-detailed-window.html</c> 设计稿：
/// <list type="bullet">
///   <item>左侧：窗口设置（弹窗基础 / 倒计时 / 窗口关闭 / 弹窗行为）</item>
///   <item>中间：按钮列表</item>
///   <item>右侧：按钮详情（Action 链）</item>
/// </list>
/// 触发方式参考 <c>YesNoDialogRuleSettingsControl.ShowSettingsButton_OnClick</c>：
/// 由设置面板里的"打开详细设置"按钮通过 <see cref="OpenAsync"/> 弹出本窗口。
/// </summary>
public partial class MultiButtonPromptDetailWindow : MyWindow, INotifyPropertyChanged
{
    /// <summary>
    /// 当前正在编辑的设置对象（与 <see cref="MultiButtonPromptSettingsControl"/> 共享同一实例，
    /// 因此窗口内的修改会自动反映回原设置的"打开"按钮所在的抽屉中）。
    /// </summary>
    public MultiButtonPromptSettings Settings { get; }

    private MultiButtonPromptButton? _activeButton;

    /// <summary>
    /// 当前选中的按钮（供右侧详情面板绑定）。
    /// 由于本类继承自 <see cref="MyWindow"/>（非 <c>ObservableObject</c>），
    /// 不能用 <c>[ObservableProperty]</c>，这里手动实现 <see cref="INotifyPropertyChanged"/>。
    /// </summary>
    public MultiButtonPromptButton? ActiveButton
    {
        get => _activeButton;
        set
        {
            if (ReferenceEquals(_activeButton, value)) return;
            _activeButton = value;
            OnPropertyChanged(nameof(ActiveButton));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ===== 拖拽状态（参照 SystemTools / SettingsControl 的实现） =====
    private const string ButtonDragDataKey = "InquiryWindow.MultiButtonPromptDetailWindow.Button";
    private const double ButtonDragThreshold = 4.0;

    private Point? _buttonDragStartPoint;
    private Border? _buttonDragSourceHandle;
    private PointerPressedEventArgs? _buttonDragPressedArgs;

    public MultiButtonPromptDetailWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 用指定的设置构造窗口。窗口的 <see cref="DataContext"/> 设为自身，
    /// 方便 XAML 直接 <c>{Binding Settings.Title}</c>。
    /// </summary>
    public MultiButtonPromptDetailWindow(MultiButtonPromptSettings settings) : this()
    {
        Settings = settings;
        DataContext = this;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        // 窗口加载时刷新一次列表
        Dispatcher.UIThread.Post(RefreshList, DispatcherPriority.Background);
    }

    private void RefreshList()
    {
        if (ButtonList is null) return;
        // 列表源用 Settings.Buttons（同一个 ObservableCollection）
        ButtonList.ItemsSource = Settings.Buttons;
        if (ButtonCountLabel is not null)
        {
            ButtonCountLabel.Text = $"({Settings.Buttons.Count})";
        }
        if (Settings.Buttons.Count > 0)
        {
            ButtonList.SelectedIndex = 0;
        }
    }

    private void OnButtonListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ButtonList?.SelectedItem is MultiButtonPromptButton btn)
        {
            ActiveButton = btn;
            if (PlaceholderView is not null) PlaceholderView.IsVisible = false;
            if (DetailView is not null) DetailView.IsVisible = true;
        }
        else
        {
            ActiveButton = null;
            if (PlaceholderView is not null) PlaceholderView.IsVisible = true;
            if (DetailView is not null) DetailView.IsVisible = false;
        }
    }

    private void OnAddButtonClick(object? sender, RoutedEventArgs e)
    {
        var id = Settings.Buttons.Count + 1;
        var btn = new MultiButtonPromptButton
        {
            Name = "新按钮 " + id,
            Icon = "\uE10F"
        };
        Settings.Buttons.Add(btn);
        if (ButtonCountLabel is not null)
        {
            ButtonCountLabel.Text = $"({Settings.Buttons.Count})";
        }
        ButtonList.SelectedItem = btn;
    }

    private void OnDeleteButtonClick(object? sender, RoutedEventArgs e)
    {
        if (ActiveButton is null) return;
        if (Settings.Buttons.Count <= 1) return;
        Settings.Buttons.Remove(ActiveButton);
        if (ButtonCountLabel is not null)
        {
            ButtonCountLabel.Text = $"({Settings.Buttons.Count})";
        }
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        // 简易搜索：清空 ItemsSource，按文本过滤后重新填充。
        // 用 ItemsSource 整体替换的方式，会打断对选中状态的追踪，
        // 但简单抽屉场景够用，且与设计稿的"中间按钮列表"一致。
        if (ButtonList is null || Settings is null) return;
        var q = (SearchBox?.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(q))
        {
            ButtonList.ItemsSource = Settings.Buttons;
            return;
        }
        ButtonList.ItemsSource = Settings.Buttons
            .Where(b => b.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private async void OnPickWindowIconClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control) return;
        var topLevel = TopLevel.GetTopLevel(control);
        if (topLevel == null) return;

        var picked = await IconPickerDialog.PickAsync(
            topLevel,
            title: "选择窗口图标",
            highlightGlyph: Settings.Icon);
        if (!string.IsNullOrEmpty(picked))
        {
            Settings.Icon = picked;
        }
    }

    // ===== 按钮列表拖拽（参照 SystemTools / SettingsControl 的实现） =====

    private void OnButtonDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border handle) return;
        if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;

        _buttonDragSourceHandle = handle;
        _buttonDragStartPoint = e.GetPosition(handle);
        _buttonDragPressedArgs = e;
        // 触摸/笔才需要 e.Handled = true，鼠标不需要
        e.Handled = e.Pointer.Type is PointerType.Touch or PointerType.Pen;
    }

    private void OnButtonDragHandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        _buttonDragSourceHandle = null;
        _buttonDragStartPoint = null;
        _buttonDragPressedArgs = null;
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

        // 用 Avalonia 12 的 InProcessFormat 跨 ItemsControl 传引用类型
        var format = DataFormat.CreateInProcessFormat<MultiButtonPromptButton>(ButtonDragDataKey);
        var item = new DataTransferItem();
        item.Set(format, source);
        var dataTransfer = new DataTransfer();
        dataTransfer.Add(item);

        var pressedArgs = _buttonDragPressedArgs;
        _buttonDragSourceHandle = null;
        _buttonDragStartPoint = null;
        _buttonDragPressedArgs = null;
        if (pressedArgs != null)
        {
            await DragDrop.DoDragDropAsync(pressedArgs, dataTransfer, DragDropEffects.Move);
        }
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
        e.Handled = true;
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

        var list = Settings.Buttons;
        var targetIndex = list.IndexOf(target);
        if (targetIndex < 0) return;

        // 水平方向二等分：右半边插到目标之后
        var pos = e.GetPosition(targetControl);
        if (pos.Y > targetControl.Bounds.Height / 2)
        {
            targetIndex += 1;
        }
        MoveButton(source, targetIndex);
        e.Handled = true;
    }

    private static bool TryGetButtonDrag(DragEventArgs e, out MultiButtonPromptButton source)
    {
        source = null!;
        var format = DataFormat.CreateInProcessFormat<MultiButtonPromptButton>(ButtonDragDataKey);
        if (!e.DataTransfer.Contains(format)) return false;
        if (e.DataTransfer.TryGetValue(format) is not { } s) return false;
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

    private async void OnPickIconClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || ActiveButton is null) return;
        var topLevel = TopLevel.GetTopLevel(control);
        if (topLevel == null) return;

        var picked = await IconPickerDialog.PickAsync(
            topLevel,
            title: "选择按钮图标",
            highlightGlyph: ActiveButton.Icon);
        if (!string.IsNullOrEmpty(picked))
        {
            ActiveButton.Icon = picked;
        }
    }

    private void OnPreviewClick(object? sender, RoutedEventArgs e)
    {
        // 复用运行时的预览逻辑：直接复用 MultiButtonPromptWindow + ViewModel 即可
        var vm = new ViewModels.MultiButtonPromptViewModel(
            Settings,
            AppBase.Current.GetServiceRequired<ClassIsland.Core.Abstractions.Services.IActionService>(),
            AppBase.Current.GetServiceRequired<Microsoft.Extensions.Logging.ILogger<ViewModels.MultiButtonPromptViewModel>>());

        var win = new MultiButtonPromptWindow { DataContext = vm };
        // 预览时按原 MultiButtonPromptWindow 的方式启动倒计时
        vm.StartAutoExecuteCountdown();
        _ = win.ShowDialogCompat();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        // 设置页保存机制由 ActionSettingsControlBase / SettingsPageBase 统一处理，
        // 本窗口内的修改是 in-place 改的（共享同一 Settings 实例），所以这里仅关闭。
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        // 简化处理：直接关窗。如果后续需要"放弃修改"语义，可在此处加一份快照恢复。
        Close();
    }

    /// <summary>
    /// 弹出本窗口。owner 不存在时回退到 Show()。
    /// </summary>
    public async Task ShowDialogCompat()
    {
        var owner = AppBase.Current.GetRootWindow();
        if (owner is Window windowOwner)
        {
            await ShowDialog<object?>(windowOwner);
        }
        else
        {
            var tcs = new TaskCompletionSource<object?>();
            void OnClosed(object? s, EventArgs e)
            {
                Closed -= OnClosed;
                tcs.TrySetResult(null);
            }
            Closed += OnClosed;
            Show();
            await tcs.Task;
        }
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
}
