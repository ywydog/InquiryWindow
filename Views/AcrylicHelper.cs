using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using InquiryWindow.Models;
using InquiryWindow.Services;

namespace InquiryWindow.Views;

/// <summary>
/// 给 Action 弹窗（询问窗 / 多按钮询问）应用/取消亚克力背景的助手。
/// 参考 ConvenientText 的实现思路：把窗口设为 ExtendClientAreaToDecorationsHint + AcrylicBlur
/// 透明，并加上圆角 Border 保证圆角处裁切正确。
///
/// 由于 v1 弹窗仍需要 OS 标题栏和拖动行为（不是无边框），这里走的是另一种"软亚克力"风格：
/// <list type="bullet">
///   <item>窗口整体背景设为透明</item>
///   <item>TransparencyLevelHint 设为 AcrylicBlur（依赖 Win10/11 的 Acrylic 合成）</item>
///   <item>内部 Border 使用半透明色，使其在亚克力基底上看起来像一块磨砂玻璃卡片</item>
/// </list>
///
/// 这样既保留 OS 标题栏和可拖动区域，又能享受亚克力的视觉。
/// </summary>
public static class AcrylicHelper
{
    /// <summary>
    /// 根据 <see cref="PluginSettings.IsAcrylicEnabled"/> 的当前值，
    /// 给传入的窗口应用或取消亚克力背景。
    /// 关闭窗口时再次调用设为 false 不会留任何残留状态。
    /// </summary>
    public static void Apply(Window window)
    {
        var settings = PluginSettingsStore.Instance.Settings;
        Apply(window, settings.IsAcrylicEnabled);
    }

    /// <summary>
    /// 强制把窗口设为启用或禁用亚克力背景。
    /// </summary>
    /// <param name="window">目标窗口。</param>
    /// <param name="enabled">true=启用亚克力；false=回退到默认不透明背景。</param>
    public static void Apply(Window window, bool enabled)
    {
        if (window is null) return;

        if (enabled)
        {
            // 亚克力：窗口整体透明，让 OS 桌面纹理透出来；内部 Border 用半透明色做磨砂玻璃卡片。
            window.TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur };
            window.Background = Brushes.Transparent;
        }
        else
        {
            // 关闭亚克力：把窗口恢复到不透明状态，避免透出桌面。
            // 这里不强制覆盖为某个具体颜色，保留 XAML 里的设置。
            window.TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
            window.Background = null;
        }
    }

    /// <summary>
    /// 把弹窗内的 Border 背景在"亚克力卡片"和"不透明卡片"之间切换。
    /// 询问窗 / 多按钮询问的 XAML 用 <c>LayerFillColorAltBrush</c> 作为默认背景，
    /// 启用亚克力时换成半透明的 <c>SystemControlBackgroundChromeMediumLowBrush</c>。
    /// </summary>
    public static IBrush? GetCardBrush(bool isAcrylic)
    {
        var key = isAcrylic
            ? "SystemControlBackgroundChromeMediumLowBrush"
            : "LayerFillColorAltBrush";

        // 资源可能在 App.Current.Resources 或某个 MergedDictionaries 里，
        // TryFindResource(object key) 会按 Application 的合并字典查找。
        if (Application.Current is { } app &&
            app.TryFindResource(key, out var res) &&
            res is IBrush brush)
        {
            return brush;
        }
        return null;
    }
}
