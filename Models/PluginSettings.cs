using CommunityToolkit.Mvvm.ComponentModel;
using InquiryWindow.Services;

namespace InquiryWindow.Models;

/// <summary>
/// 插件级全局设置。实例由 <see cref="PluginSettingsStore"/> 单例持有，
/// 整个进程（包括所有 Action 弹窗、设置页）共享同一份。
/// </summary>
public partial class PluginSettings : ObservableObject
{
    /// <summary>
    /// 是否为 Action 弹窗（询问窗 / 多按钮询问）启用亚克力背景。
    /// 默认 false：保持和 v1 行为一致，避免给用户带来突如其来的视觉变化。
    /// </summary>
    [ObservableProperty]
    private bool _isAcrylicEnabled;
}
