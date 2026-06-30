using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InquiryWindow.Models;
using InquiryWindow.Services;

namespace InquiryWindow.ViewModels;

/// <summary>
/// 插件 ViewPage 的 ViewModel：管理按钮预设库。
/// </summary>
public partial class InquiryWindowSettingsViewModel : ObservableObject
{
    public ObservableCollection<ButtonPreset> Presets => PresetsStore.Instance.Presets;

    [ObservableProperty]
    private ButtonPreset? _selectedPreset;

    public InquiryWindowSettingsViewModel()
    {
        // 保证预设库从磁盘加载（如果 Plugin.Initialize 之前没跑过）。
        PresetsStore.Instance.Load();
    }

    /// <summary>
    /// 创建一个新预设并返回。
    /// </summary>
    [RelayCommand]
    public void AddPreset()
    {
        var preset = PresetsStore.Instance.AddPreset(
            "新预设 " + (Presets.Count + 1),
            "\uE10F");
        SelectedPreset = preset;
    }
}
