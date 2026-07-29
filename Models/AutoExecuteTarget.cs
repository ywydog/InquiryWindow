using CommunityToolkit.Mvvm.ComponentModel;

namespace InquiryWindow.Models;

/// <summary>
/// 表示"自动执行"下拉框中的一个可选项。
/// <para>
/// 索引 <see cref="ButtonIndex"/> == -1 表示"无事发生"占位（不会执行任何 Action 链，仅自动关闭弹窗）；
/// 其它索引对应 <see cref="MultiButtonPromptSettings.Buttons"/> 里的某个按钮。
/// </para>
/// <para>
/// 该类在 <see cref="MultiButtonPromptSettings.AutoExecuteTargets"/> 中按 Buttons 顺序生成，
/// 末尾追加一个"无事发生"占位。仅用于在 UI 下拉框中显示，不参与 Action 链执行。
/// </para>
/// </summary>
public partial class AutoExecuteTarget : ObservableObject
{
    /// <summary>下拉框中显示的名字。</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>是否是无事发生占位（true 时 <see cref="ButtonIndex"/> == -1）。</summary>
    [ObservableProperty]
    private bool _isNoAction;

    /// <summary>
    /// 在 <see cref="MultiButtonPromptSettings.Buttons"/> 里的索引。
    /// -1 表示"无事发生"，0 及以上表示真实按钮位置。
    /// </summary>
    [ObservableProperty]
    private int _buttonIndex = -1;
}
