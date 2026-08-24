using System.Collections.Specialized;
using Avalonia.Media;
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

    /// <summary>
    /// 是否启用"交互倒计时"。启用后，按下该按钮会禁用所有按钮 N 秒，
    /// 倒计时结束才恢复交互，但**不会**自动执行该按钮的 Action。
    /// UI 表现参考 SuperAutoIsland 的 YesNo 对话框：在默认按钮文字后追加「(Ns)」实时显示剩余秒数。
    /// 与"自动执行"功能（左侧"自动执行"区块）独立：自动执行到时间会按指定目标触发 Action。
    /// </summary>
    [ObservableProperty]
    private bool _isCountdownEnabled;

    /// <summary>
    /// 交互倒计时禁用秒数。需与 <see cref="IsCountdownEnabled"/> 配合使用。
    /// </summary>
    [ObservableProperty]
    private double _countdownSeconds = 3;

    /// <summary>
    /// 按钮的强调样式模式。默认 <see cref="ButtonAccentMode.Default"/>（不强调）。
    /// 详见 <see cref="ButtonAccentMode"/>。
    /// </summary>
    [ObservableProperty]
    private ButtonAccentMode _accentMode = ButtonAccentMode.Default;

    /// <summary>
    /// 自定义强调色。仅在 <see cref="AccentMode"/> = <see cref="ButtonAccentMode.Custom"/> 时使用。
    /// 默认值取系统蓝色，与"高亮"模式视觉保持一致。
    /// </summary>
    [ObservableProperty]
    private Color _customColor = Color.FromRgb(0x2D, 0x7F, 0xF9);

    // ===== 派生属性（不持久化，运行时用） =====

    /// <summary>是否使用系统主题强调色（"高亮"模式）。用于 XAML 的 <c>Classes.accent</c> 绑定。</summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool IsAccent => AccentMode == ButtonAccentMode.Highlighted;

    /// <summary>是否使用自定义颜色（"自定义"模式）。用于 XAML 的 <c>Classes.customAccent</c> 绑定。</summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool IsCustomAccent => AccentMode == ButtonAccentMode.Custom;

    /// <summary>
    /// <see cref="CustomColor"/> 对应的画刷，用于 XAML 的 <c>Background</c> 绑定。
    /// （Avalonia 12 运行时不再把 Color 自动转换为 IBrush，直接绑 Color 会失败。）
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public IBrush CustomColorBrush => new SolidColorBrush(CustomColor);

    // AccentMode / CustomColor 变化时通知 IsAccent / IsCustomAccent
    partial void OnAccentModeChanged(ButtonAccentMode value)
    {
        OnPropertyChanged(nameof(IsAccent));
        OnPropertyChanged(nameof(IsCustomAccent));
    }

    partial void OnCustomColorChanged(Color value)
    {
        // 通知 CustomColorBrush 一并刷新。
        OnPropertyChanged(nameof(CustomColorBrush));
    }

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
