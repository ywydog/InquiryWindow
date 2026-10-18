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
    /// 是否启用"交互倒计时"。启用后，弹窗打开时该按钮会被锁定 <see cref="CountdownSeconds"/> 秒：
    /// 锁定期内该按钮不可点击（UI 呈暗淡的禁用态）并显示剩余秒数「(Ns)」，归零后恢复可点击。
    /// 锁定期结束后，点击该按钮会立即执行其 Action 链。
    /// 未启用交互倒计时的按钮不受影响（弹窗打开即可点击，且不会变暗淡）。
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
    /// 运行时的交互倒计时剩余秒数（弹窗打开时初始化，每秒递减）。
    /// &gt;0 表示该按钮仍处于锁定态；0 表示可点击。不参与序列化。
    /// </summary>
    [ObservableProperty]
    [Newtonsoft.Json.JsonIgnore]
    private int _countdownRemaining;

    /// <summary>该按钮是否已完成交互倒计时、可以点击。</summary>
    [Newtonsoft.Json.JsonIgnore]
    public bool IsCountdownReady => CountdownRemaining <= 0;

    /// <summary>按钮显示文字：锁定期间在名称后追加「(Ns)」实时剩余秒数。</summary>
    [Newtonsoft.Json.JsonIgnore]
    public string DisplayName => CountdownRemaining > 0 ? $"{Name} ({CountdownRemaining}s)" : Name;

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
    /// 运行按钮的背景画刷，按 <see cref="AccentMode"/> 分派：
    /// <list type="bullet">
    ///   <item><see cref="ButtonAccentMode.Custom"/>：返回 <see cref="CustomColor"/>（用户自定义色）。</item>
    ///   <item><see cref="ButtonAccentMode.Default"/> / <see cref="ButtonAccentMode.Highlighted"/>：
    ///   返回 <c>null</c>，让按钮回退到 ClassIsland 主题默认背景（跟随主题变化；
    ///   "高亮"模式下背景由内置 accent 样式提供，即系统主题蓝）。</item>
    /// </list>
    /// （Avalonia 12 运行时不再把 Color 自动转换为 IBrush，直接绑 Color 会失败。）
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public IBrush? ButtonBackground =>
        AccentMode == ButtonAccentMode.Custom ? new SolidColorBrush(CustomColor) : null;

    // AccentMode / CustomColor 变化时通知派生 UI 属性一并刷新
    partial void OnAccentModeChanged(ButtonAccentMode value)
    {
        OnPropertyChanged(nameof(IsAccent));
        OnPropertyChanged(nameof(IsCustomAccent));
        OnPropertyChanged(nameof(ButtonBackground));
    }

    partial void OnCountdownRemainingChanged(int value)
    {
        // 锁定状态与显示文字（含剩余秒数）随之刷新。
        OnPropertyChanged(nameof(IsCountdownReady));
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnCustomColorChanged(Color value)
    {
        // 通知 ButtonBackground 一并刷新。
        OnPropertyChanged(nameof(ButtonBackground));
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
