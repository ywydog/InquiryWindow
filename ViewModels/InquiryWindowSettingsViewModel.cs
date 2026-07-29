using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InquiryWindow.Models;
using InquiryWindow.Services;

namespace InquiryWindow.ViewModels;

/// <summary>
/// InquiryWindow 主设置页的 ViewModel：只管插件级全局设置（亚克力背景等）。
/// 按钮预设库已拆到 <see cref="ButtonPresetSettingsViewModel"/> 与独立页面。
/// </summary>
public partial class InquiryWindowSettingsViewModel : ObservableObject
{
    /// <summary>
    /// 跨 Action 共享的插件级设置（亚克力背景开关 + 透明度）。
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
    /// XAML 里用绑定直接改字段（<see cref="PluginSettings.UseAcrylicBackground"/>、
    /// <see cref="PluginSettings.AcrylicTintOpacity"/>），点击应用或失焦时调用本方法。
    /// </summary>
    [RelayCommand]
    public void SavePluginSettings()
    {
        PluginSettingsStore.Instance.SaveNow();
    }
}
