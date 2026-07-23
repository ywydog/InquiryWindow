using System.IO;
using ClassIsland.Shared.Helpers;
using InquiryWindow.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InquiryWindow.Services;

/// <summary>
/// 插件级全局设置（亚克力背景开关等）的持久化与单例访问。
/// 落盘到 <c>&lt;PluginConfig&gt;/plugin-settings.json</c>。
/// </summary>
public class PluginSettingsStore
{
    private static PluginSettingsStore? _instance;
    private static readonly object StaticLock = new();

    public static PluginSettingsStore Instance
    {
        get
        {
            if (_instance is not null) return _instance;
            lock (StaticLock)
            {
                _instance ??= new PluginSettingsStore();
            }
            return _instance;
        }
    }

    private readonly object _loadLock = new();
    private readonly ILogger _logger;
    private string? _path;
    private bool _loaded;

    /// <summary>
    /// 当前生效的设置。所有 Action 弹窗、设置页都从这里读取。
    /// </summary>
    public PluginSettings Settings { get; private set; } = new();

    /// <summary>
    /// 插件根目录的 PluginConfigFolder，由 Plugin.cs 在初始化时注入。
    /// </summary>
    public static string PluginConfigFolder { get; set; } = string.Empty;

    private PluginSettingsStore(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger<PluginSettingsStore>.Instance;
    }

    /// <summary>
    /// 从磁盘加载设置。重复调用安全。若 <see cref="PluginConfigFolder"/> 尚未被注入，
    /// 调用会被延后（不会用 LocalAppData 兜底，因为之后 Plugin.Initialize 再次调用会读到正确路径）。
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
            _path = Path.Combine(folder, "plugin-settings.json");

            try
            {
                var loaded = ConfigureFileHelper.LoadConfig<PluginSettings>(_path);
                if (loaded is not null)
                {
                    Settings = loaded;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载插件设置失败，使用默认设置：{Path}", _path);
                Settings = new PluginSettings();
            }

            Settings.PropertyChanged += OnSettingChanged;
            _loaded = true;
        }
    }

    /// <summary>
    /// 立即保存到磁盘。绕过 debounce。
    /// </summary>
    public void SaveNow()
    {
        lock (_loadLock)
        {
            if (!_loaded || string.IsNullOrEmpty(_path)) return;
            try
            {
                ConfigureFileHelper.SaveConfig(_path, Settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存插件设置失败：{Path}", _path);
            }
        }
    }

    private CancellationTokenSource? _saveDebounce;

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

    private void OnSettingChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        SaveDebounced();
    }
}
