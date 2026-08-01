using System.IO;
using ClassIsland.Shared.Helpers;
using InquiryWindow.Models;

namespace InquiryWindow.Services;

/// <summary>
/// 插件级全局设置存储：落盘到 <c>&lt;PluginConfig&gt;/plugin-settings.json</c>。
/// 与 <see cref="PresetsStore"/> 解耦：前者管按钮预设库，本类管跨 Action 共享的选项。
///
/// Android 兼容说明（new/for-android-2.2 分支专用）：
/// 本分支去掉了 Microsoft.Extensions.Logging 依赖（避免 Android 上偶发的
/// TypeLoadException 触发 cctor / ModuleInitialize），改为不记录日志。
/// Windows / 桌面端的实现见 main / new/for2.2 分支。
/// </summary>
public class PluginSettingsStore
{
    private static PluginSettingsStore? _instance;
    private static readonly object StaticLock = new();

    /// <summary>
    /// 兼容层：保留静态 <see cref="Instance"/> 属性。
    /// </summary>
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
    private string? _path;
    private bool _loaded;

    /// <summary>
    /// 公共构造函数：支持 DI 容器直接 new。
    /// </summary>
    public PluginSettingsStore()
    {
    }

    /// <summary>
    /// 插件根目录，由 <see cref="Plugin"/> 在初始化时注入。
    /// </summary>
    public static string PluginConfigFolder { get; set; } = string.Empty;

    public PluginSettings Data { get; private set; } = new();

    public event EventHandler? DataChanged;

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
            catch
            {
                // 加载失败时静默回退到默认设置，避免破坏 Android 上的插件加载。
                Data = new PluginSettings();
            }

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
            catch
            {
                // 落盘失败时静默忽略。
            }
        }
    }
}
