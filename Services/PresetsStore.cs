using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using ClassIsland.Shared.Helpers;
using InquiryWindow.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InquiryWindow.Services;

/// <summary>
/// 按钮预设库：落盘到 <c>&lt;PluginConfig&gt;/presets.json</c>，
/// 启动时加载，编辑后自动写回。
/// </summary>
public class PresetsStore
{
    private static PresetsStore? _instance;
    private static readonly object LockObj = new();

    public static PresetsStore Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (LockObj)
            {
                _instance ??= new PresetsStore();
            }
            return _instance;
        }
    }

    private string _path = string.Empty;
    private readonly ILogger _logger;
    private bool _loaded;

    public PresetsData Data { get; private set; } = new();

    private PresetsStore(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger<PresetsStore>.Instance;
    }

    /// <summary>
    /// 插件根目录的 PluginConfigFolder，由 Plugin.cs 在初始化时注入。
    /// </summary>
    public static string PluginConfigFolder { get; set; } = string.Empty;

    public ObservableCollection<ButtonPreset> Presets => Data.Presets;

    /// <summary>
    /// 从磁盘加载预设库。重复调用安全。
    /// </summary>
    public void Load()
    {
        if (_loaded) return;

        var folder = !string.IsNullOrEmpty(PluginConfigFolder)
            ? PluginConfigFolder
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ClassIsland", "Plugins", "InquiryWindow");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "presets.json");

        try
        {
            Data = ConfigureFileHelper.LoadConfig<PresetsData>(_path);
            Data.Presets ??= new ObservableCollection<ButtonPreset>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载预设库失败，使用空库：{Path}", _path);
            Data = new PresetsData();
        }

        Data.Presets.CollectionChanged += OnPresetsChanged;
        foreach (var p in Data.Presets)
        {
            p.PropertyChanged += OnPresetPropertyChanged;
        }
        _loaded = true;
    }

    /// <summary>
    /// 立即保存预设库到磁盘。
    /// </summary>
    public void Save()
    {
        if (!_loaded || string.IsNullOrEmpty(_path)) return;
        try
        {
            ConfigureFileHelper.SaveConfig(_path, Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存预设库失败：{Path}", _path);
        }
    }

    public ButtonPreset AddPreset(string name = "新预设", string icon = "\uE10F")
    {
        Load();
        var preset = new ButtonPreset { Name = name, Icon = icon };
        Data.Presets.Add(preset);
        return preset;
    }

    public bool RemovePreset(ButtonPreset preset)
    {
        Load();
        return Data.Presets.Remove(preset);
    }

    private void OnPresetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (ButtonPreset p in e.NewItems)
            {
                p.PropertyChanged += OnPresetPropertyChanged;
            }
        }
        if (e.OldItems != null)
        {
            foreach (ButtonPreset p in e.OldItems)
            {
                p.PropertyChanged -= OnPresetPropertyChanged;
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // Reset 不会给出 NewItems/OldItems，已有的订阅不会自动解绑。
            // 我们的使用场景下不会触发 Reset（Data.Presets 在 Load 之后只增不减），这里兜个空。
        }
        Save();
    }

    private void OnPresetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Save();
    }
}
