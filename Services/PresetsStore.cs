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
    private static readonly object StaticLock = new();

    public static PresetsStore Instance
    {
        get
        {
            if (_instance is not null) return _instance;
            lock (StaticLock)
            {
                _instance ??= new PresetsStore();
            }
            return _instance;
        }
    }

    private readonly object _loadLock = new();
    private readonly ILogger _logger;
    private readonly HashSet<ButtonPreset> _trackedPresets = new();

    private CancellationTokenSource? _saveDebounce;
    private string? _path;
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
    /// 若 <see cref="PluginConfigFolder"/> 尚未被注入，调用会被延后
    /// （不会用 LocalAppData 兜底，因为之后 Plugin.Initialize 再次调用会读到正确路径）。
    /// </summary>
    public void Load()
    {
        lock (_loadLock)
        {
            if (_loaded) return;

            if (string.IsNullOrEmpty(PluginConfigFolder))
            {
                // 插件还没初始化完，等下次再试。
                return;
            }

            var folder = PluginConfigFolder;
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
                Track(p);
            }
            _loaded = true;
        }
    }

    /// <summary>
    /// 立即保存预设库到磁盘。绕过 debounce。
    /// </summary>
    public void SaveNow()
    {
        lock (_loadLock)
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
    }

    /// <summary>
    /// 防抖落盘：把 N ms 内的多次写盘请求合并为一次，避免每键写盘。
    /// </summary>
    public void SaveDebounced(int delayMs = 400)
    {
        _saveDebounce?.Cancel();
        var cts = new CancellationTokenSource();
        _saveDebounce = cts;

        Task.Delay(delayMs, cts.Token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;
            SaveNow();
        }, TaskScheduler.Default);
    }

    /// <summary>兼容旧调用，立即落盘。</summary>
    public void Save() => SaveNow();

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

    private void Track(ButtonPreset p)
    {
        if (_trackedPresets.Add(p))
        {
            p.PropertyChanged += OnPresetPropertyChanged;
        }
    }

    private void Untrack(ButtonPreset p)
    {
        if (_trackedPresets.Remove(p))
        {
            p.PropertyChanged -= OnPresetPropertyChanged;
        }
    }

    private void OnPresetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        lock (_loadLock)
        {
            if (e.NewItems is not null)
            {
                foreach (ButtonPreset p in e.NewItems) Track(p);
            }
            if (e.OldItems is not null)
            {
                foreach (ButtonPreset p in e.OldItems) Untrack(p);
            }
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                // Reset 不提供 OldItems，需要从 _trackedPresets 兜底解绑，
                // 然后把新集合里的所有项重新订阅。
                foreach (var p in _trackedPresets.ToList())
                {
                    Untrack(p);
                }
                foreach (var p in Data.Presets)
                {
                    Track(p);
                }
            }
            SaveDebounced();
        }
    }

    private void OnPresetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        SaveDebounced();
    }
}
