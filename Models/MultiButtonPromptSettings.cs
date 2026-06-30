using System.Collections.ObjectModel;
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

    /// <summary>窗口内副提示文本（可空）。</summary>
    [ObservableProperty]
    private string _subPrompt = string.Empty;

    /// <summary>
    /// 按钮集合（按显示顺序）。
    /// setter 仍保留以兼容 ConfigureFileHelper 的 JSON 反序列化（整体赋值）。
    /// 反序列化之外请通过 Add/Remove 变更集合，**不要**整体替换，否则持旧引用的代码
    /// （包括弹窗 ViewModel）会看不到新数据。
    /// </summary>
    public ObservableCollection<MultiButtonPromptButton> Buttons { get; set; } = new();

    /// <summary>
    /// 首次创建时给一个"执行/取消"模板，让用户能直接看到可工作的最小配置。
    /// 反序列化时 ConfigureFileHelper 会先调 ctor 再覆盖字段，因此 JSON 里有数据时这两条默认按钮不会保留。
    /// </summary>
    public MultiButtonPromptSettings()
    {
        Buttons.Add(new MultiButtonPromptButton { Name = "执行", Icon = "\uE73E" });
        Buttons.Add(new MultiButtonPromptButton { Name = "取消", Icon = "\uE711" });
    }
}
