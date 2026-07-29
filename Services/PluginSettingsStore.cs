using System.IO;
using ClassIsland.Shared.Helpers;
using InquiryWindow.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace InquiryWindow.Services;

/// <summary>
/// 插件级全局设置存储：落盘到 <c>&lt;PluginConfig&gt;/plugin-settings.json</c>。
/// 与 <see cref="PresetsStore"/> 解耦：前者管按钮预设库，本类管跨 Action 共享的选项。
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
    /// 插件根目录，由 <see cref="Plugin"/> 在初始化时注入。
    /// </summary>
    public static string PluginConfigFolder { get; set; } = string.Empty;

    public PluginSettings Data { get; private set; } = new();

    public event EventHandler? DataChanged;

    private PluginSettingsStore(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger<PluginSettingsStore>.Instance;
    }

    /// <summary>
    /// 从磁盘加载。重复调用安全。若 <see cref="PluginConfigFolder"/> 尚未注入则延后。
    /// </summary>
    public void Load()
    {
        lock (_loadLock)
        {
            if (_loaded) return;

            if (string.IsNullOrEmpty(PluginConfigFolder))
            {
                return;
            }

            var folder = PluginConfigFolder;
            Directory.CreateDirectory(folder);
            _path = Path.Combine(folder, "plugin-settings.json");

            try
            {
                Data = ConfigureFileHelper.LoadConfig<PluginSettings>(_path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载插件全局设置失败，使用默认值：{Path}", _path);
                Data = new PluginSettings();
            }

            // 边界保护：旧版文件可能缺少新增字段。
            if (Data.AcrylicTintOpacity < 0) Data.AcrylicTintOpacity = 0;
            if (Data.AcrylicTintOpacity > 1) Data.AcrylicTintOpacity = 1;

            _loaded = true;
        }
    }

    /// <summary>
    /// 立即落盘并通知订阅者。设置页改动时由 VM 显式调用，避免 debounce 期间关 app 丢数据。
    /// </summary>
    public void SaveNow()
    {
        lock (_loadLock)
        {
            if (string.IsNullOrEmpty(_path))
            {
                // 还没加载过（多半是构造期单元测试场景），跳过落盘。
                return;
            }
            try
            {
                ConfigureFileHelper.SaveConfig(_path, Data);
                DataChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存插件全局设置失败：{Path}", _path);
            }
        }
    }
}
