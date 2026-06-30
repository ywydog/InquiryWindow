using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using InquiryWindow.Models;
using InquiryWindow.Services;

namespace InquiryWindow.ViewModels;

/// <summary>
/// 插件 ViewPage 的 ViewModel：管理按钮预设库。
/// </summary>
public class InquiryWindowSettingsViewModel : ObservableObject
{
    public ObservableCollection<ButtonPreset> Presets => PresetsStore.Instance.Presets;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemovePresetCommand))]
    private ButtonPreset? _selectedPreset;

    public InquiryWindowSettingsViewModel()
    {
        PresetsStore.Instance.Load();
    }

    [RelayCommand]
    private void AddPreset()
    {
        var preset = PresetsStore.Instance.AddPreset(
            "新预设 " + (Presets.Count + 1),
            "\uE10F");
        SelectedPreset = preset;
    }

    [RelayCommand(CanExecute = nameof(CanRemovePreset))]
    private void RemovePreset(ButtonPreset? preset)
    {
        if (preset == null) return;
        if (SelectedPreset == preset)
        {
            SelectedPreset = null;
        }
        PresetsStore.Instance.RemovePreset(preset);
    }

    private bool CanRemovePreset(ButtonPreset? preset) => preset != null;
}
