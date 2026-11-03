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

    /// <summary>
    /// 亚克力背景开关。XAML 绑到 ToggleSwitch，同时驱动透明度滑块的 IsEnabled，
    /// 因此必须在 ViewModel 上暴露可通知属性；直接绑 POCO 字段时同页面的其它绑定
    /// 收不到变更通知（滑块不会跟着启用/禁用，百分比文本也不刷新）。
    /// </summary>
    public bool UseAcrylicBackground
    {
        get => PluginSettings.UseAcrylicBackground;
        set
        {
            if (PluginSettings.UseAcrylicBackground == value) return;
            PluginSettings.UseAcrylicBackground = value;
            OnPropertyChanged();
            PluginSettingsStore.Instance.SaveNow();
        }
    }

    /// <summary>
    /// 亚克力色调不透明度（0~1）。绑定到 Slider 和百分比文本，变动立即落盘
    /// （plugin-settings.json 很小，拖动期间的写入量可以接受）。
    /// </summary>
    public double AcrylicTintOpacity
    {
        get => PluginSettings.AcrylicTintOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(PluginSettings.AcrylicTintOpacity - clamped) < 0.0001) return;
            PluginSettings.AcrylicTintOpacity = clamped;
            OnPropertyChanged();
            PluginSettingsStore.Instance.SaveNow();
        }
    }

    public InquiryWindowSettingsViewModel()
    {
        // 兜底加载插件全局设置。
        PluginSettingsStore.Instance.Load();
    }

    /// <summary>
    /// 设置页保存入口：把当前 <see cref="PluginSettings"/> 落盘。
    /// 属性 setter 已即时落盘，这里保留给需要显式保存的调用方。
    /// </summary>
    [RelayCommand]
    public void SavePluginSettings()
    {
        PluginSettingsStore.Instance.SaveNow();
    }
}
