using CommunityToolkit.Mvvm.ComponentModel;

namespace InquiryWindow.Models;

public partial class InquiryWindowActionSettings : ObservableObject
{
    /// <summary>
    /// OS 窗口标题栏文字（任务栏/任务切换器可见）。不支持变量替换。
    /// </summary>
    [ObservableProperty]
    private string _windowTitle = "ClassIsland - 询问窗";

    /// <summary>
    /// 窗口内顶部小标题。支持变量替换。
    /// </summary>
    [ObservableProperty]
    private string _dialogTitle = "是否执行以下操作？";

    /// <summary>
    /// 窗口正文（多行）。支持变量替换。
    /// </summary>
    [ObservableProperty]
    private string _dialogBody = "";

    /// <summary>
    /// 启动目标路径（.exe / 任意文件 / 文件夹）。不支持变量替换。
    /// </summary>
    [ObservableProperty]
    private string _targetPath = "";

    /// <summary>
    /// 是否在弹窗中显示目标路径。默认 false。
    /// </summary>
    [ObservableProperty]
    private bool _showPath;

    /// <summary>
    /// 是否在弹窗中显示目标 .exe 的关联图标。默认 false。
    /// </summary>
    [ObservableProperty]
    private bool _showIcon;
}
