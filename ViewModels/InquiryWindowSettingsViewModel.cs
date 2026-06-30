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

    /// <summary>
    /// 单调递增的预设序号，避免删除中间预设后命名撞车。
    /// </summary>
    private int _nextPresetNumber = 1;

    public InquiryWindowSettingsViewModel()
    {
        // 保证预设库从磁盘加载（如果 Plugin.Initialize 之前没跑过）。
        // Load 内部会用空 PluginConfigFolder 时延后，等 Plugin 注入后再加载。
        PresetsStore.Instance.Load();
        // 初始化序号：基于现有预设名中最大的数字 + 1。
        _nextPresetNumber = ComputeNextNumber();
    }

    [RelayCommand]
    public void AddPreset()
    {
        var name = "新预设 " + _nextPresetNumber;
        _nextPresetNumber++;
        var preset = PresetsStore.Instance.AddPreset(name, "\uE10F");
        SelectedPreset = preset;
    }

    private int ComputeNextNumber()
    {
        // 扫描现有预设名中「新预设 N」里的最大 N，作为下次新建的起点。
        var max = 0;
        foreach (var p in Presets)
        {
            if (p.Name.StartsWith("新预设 ") &&
                int.TryParse(p.Name.AsSpan("新预设 ".Length), out var n))
            {
                if (n > max) max = n;
            }
        }
        return max + 1;
    }
}
