using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace InquiryWindow.Models;

/// <summary>
/// 多按钮询问 Action 的设置。
/// </summary>
public partial class MultiButtonPromptSettings : ObservableObject
{
    /// <summary>OS 窗口标题栏文字。</summary>
    [ObservableProperty]
    private string _title = "ClassIsland - 询问";

    /// <summary>窗口内主提示文本。</summary>
    [ObservableProperty]
    private string _prompt = "请选择";

    /// <summary>副提示文本（可空）。</summary>
    [ObservableProperty]
    private string _subPrompt = string.Empty;

    /// <summary>
    /// 窗口图标字符（Fluent Icons 字体），显示在弹窗主标题左侧。
    /// 与 InquiryWindowActionSettings 的 ShowIcon 无关，MultiButtonPrompt 总显示。
    /// </summary>
    [ObservableProperty]
    private string _icon = "\uE82D";

    /// <summary>
    /// 窗口是否可直接关闭（显示 X 关闭按钮）。默认 true。
    /// </summary>
    [ObservableProperty]
    private bool _isClosable = true;

    /// <summary>
    /// 窗口是否置顶（始终位于最上层）。默认 true。
    /// </summary>
    [ObservableProperty]
    private bool _isTopmost = true;

    /// <summary>
    /// 窗口文本是否按 Markdown 渲染（主提示、副提示、按钮副提示等）。
    /// 默认 true。
    /// </summary>
    [ObservableProperty]
    private bool _isMarkdownRendered = true;

    private ObservableCollection<MultiButtonPromptButton> _buttons = new();

    /// <summary>
    /// 按钮集合（按显示顺序）。
    /// setter 仍保留以兼容 ConfigureFileHelper 的 JSON 反序列化（整体赋值）。
    /// 反序列化之外请通过 Add/Remove 变更集合，**不要**整体替换，否则持旧引用的代码
    /// （包括弹窗 ViewModel）会看不到新数据。
    /// </summary>
    public ObservableCollection<MultiButtonPromptButton> Buttons
    {
        get => _buttons;
        set
        {
            if (ReferenceEquals(_buttons, value)) return;

            // 反序列化会走 setter 整体替换集合，必须把 CollectionChanged 订阅迁移到新集合，
            // 否则 AutoExecuteTargets 无法随按钮增删自动刷新。
            if (_buttons is not null)
            {
                _buttons.CollectionChanged -= OnButtonsCollectionChanged;
            }
            _buttons = value ?? new ObservableCollection<MultiButtonPromptButton>();
            _buttons.CollectionChanged += OnButtonsCollectionChanged;

            // 整体替换后重建"自动执行"目标下拉，并把索引夹到合法范围。
            OnButtonsCollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 是否启用自动执行（到时间后自动触发指定按钮的 Action 链或"无事发生"）。默认 false。
    /// </summary>
    [ObservableProperty]
    private bool _isAutoExecuteEnabled;

    /// <summary>
    /// 自动执行等待秒数。需与 <see cref="IsAutoExecuteEnabled"/> 配合使用。
    /// </summary>
    [ObservableProperty]
    private double _autoExecuteSeconds = 5;

    /// <summary>
    /// 自动执行下拉框中当前选中项在 <see cref="AutoExecuteTargets"/> 里的索引。
    /// 取值范围 [0, Buttons.Count]：
    /// <list type="bullet">
    ///   <item>[0, Buttons.Count - 1]：对应 <see cref="Buttons"/> 中的真实按钮</item>
    ///   <item>Buttons.Count：末尾的"无事发生"占位</item>
    /// </list>
    /// </summary>
    [ObservableProperty]
    private int _autoExecuteTargetIndex;

    /// <summary>
    /// 窗口背景图片绝对路径；空字符串 / null 表示不使用图片背景（使用纯色背景）。
    /// 支持 PNG / JPG / JPEG / WEBP / BMP。文件不存在或加载失败时降级到纯色背景。
    /// </summary>
    [ObservableProperty]
    private string _backgroundImagePath = string.Empty;

    /// <summary>
    /// 背景图片缩放模式（0=Cover 覆盖，1=Contain 包含，2=Stretch 拉伸，3=Tile 平铺）。
    /// 存为 int 索引方便 XAML 的 ComboBox SelectedIndex 直接绑定。
    /// </summary>
    [ObservableProperty]
    private int _backgroundImageMode;

    /// <summary>
    /// 自动执行下拉框的可选项集合（运行时由 <see cref="Buttons"/> + 末尾"无事发生"占位生成）。
    /// 运行时派生数据，不参与 JSON 持久化；反序列化后由 <see cref="Buttons"/> 的 setter 重建，
    /// 若序列化/反序列化会追加导致下拉重复错位，故标记 [JsonIgnore]。
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    public ObservableCollection<AutoExecuteTarget> AutoExecuteTargets { get; } = new();

    /// <summary>
    /// 是否启用「交互融合」显示模式：开启后把多按钮询问内容融合到 ClassIsland 主界面
    /// 覆盖层显示（而非打开独立窗口）。默认 false。该开关在「详细设置」窗口中配置。
    /// </summary>
    [ObservableProperty]
    private bool _isInteractiveFusion;

    public MultiButtonPromptSettings()
    {
        Buttons.CollectionChanged += OnButtonsCollectionChanged;
        PropertyChanged += OnSelfPropertyChanged;
        Buttons.Add(new MultiButtonPromptButton { Name = "执行", Icon = "\uE73E" });
        Buttons.Add(new MultiButtonPromptButton { Name = "取消", Icon = "\uE711" });
        RebuildAutoExecuteTargets();
        AutoExecuteTargetIndex = 0;
    }

    /// <summary>
    /// 启用自动执行时，自动选一个真实按钮（如果存在），保证下拉框不会是"无事发生"。
    /// 之后用户可以再手动改成"无事发生"。
    /// </summary>
    private void OnSelfPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IsAutoExecuteEnabled) && IsAutoExecuteEnabled)
        {
            // 如果当前选中的是"无事发生"（索引 == Buttons.Count），或者索引越界，
            // 就回退到第一个真实按钮（索引 0）。Buttons 为空时仍然会选到"无事发生"占位。
            if (AutoExecuteTargetIndex < 0 || AutoExecuteTargetIndex >= Buttons.Count)
            {
                AutoExecuteTargetIndex = 0;
            }
        }
    }

    /// <summary>
    /// 当 <see cref="Buttons"/> 集合变化时，刷新 <see cref="AutoExecuteTargets"/>，
    /// 并把 <see cref="AutoExecuteTargetIndex"/> 夹到 [0, Buttons.Count] 合法范围。
    /// </summary>
    private void OnButtonsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildAutoExecuteTargets();

        if (AutoExecuteTargetIndex < 0 || AutoExecuteTargetIndex > Buttons.Count)
        {
            // 集合清空时唯一可选的就是"无事发生"（索引 0）；非空时默认选第一个真实按钮。
            AutoExecuteTargetIndex = 0;
        }
    }

    private void RebuildAutoExecuteTargets()
    {
        AutoExecuteTargets.Clear();
        for (int i = 0; i < Buttons.Count; i++)
        {
            AutoExecuteTargets.Add(new AutoExecuteTarget
            {
                Name = Buttons[i].Name,
                IsNoAction = false,
                ButtonIndex = i,
            });
        }
        AutoExecuteTargets.Add(new AutoExecuteTarget
        {
            Name = "无事发生",
            IsNoAction = true,
            ButtonIndex = -1,
        });
    }
}
