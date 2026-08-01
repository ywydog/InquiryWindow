using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InquiryWindow.Models;
using InquiryWindow.Services;

namespace InquiryWindow.ViewModels;

/// <summary>
/// InquiryWindow 主设置页的 ViewModel。
///
/// Android 兼容说明（new/for-android-2.2 分支专用）：
/// 主设置页只保留"加载全局设置"占位，桌面端的弹窗外观/亚克力背景等设置已移除
/// （WindowTransparencyLevel.AcrylicBlur 在 Android 上不支持）。
/// </summary>
public partial class InquiryWindowSettingsViewModel : ObservableObject
{
    /// <summary>
    /// 跨 Action 共享的插件级设置。
    /// 暴露为属性便于 XAML 直接绑定到 PluginSettings 的字段。
    /// </summary>
    public PluginSettings PluginSettings => PluginSettingsStore.Instance.Data;

    public InquiryWindowSettingsViewModel()
    {
        // 兜底加载插件全局设置。
        PluginSettingsStore.Instance.Load();
    }

    /// <summary>
    /// 设置页保存入口：把当前 <see cref="PluginSettings"/> 落盘。
    /// </summary>
    [RelayCommand]
    public void SavePluginSettings()
    {
        PluginSettingsStore.Instance.SaveNow();
    }
}
