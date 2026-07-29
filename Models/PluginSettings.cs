namespace InquiryWindow.Models;

/// <summary>
/// 插件级全局设置。落盘到 <c>&lt;PluginConfig&gt;/plugin-settings.json</c>。
/// 与 Action 级别的 <see cref="InquiryWindowActionSettings"/> 不同：这里放的是
/// 跨 Action 共享、且与「插件本身」相关的选项（例如弹窗亚克力背景开关）。
/// </summary>
public class PluginSettings
{
    /// <summary>
    /// 是否为询问窗弹窗启用亚克力模糊背景。默认 false（沿用原纯色风格）。
    /// </summary>
    public bool UseAcrylicBackground { get; set; } = false;

    /// <summary>
    /// 亚克力背景的色调不透明度（0~1）。值越小背景越透明、桌面越清晰；
    /// 值越大背景越接近纯色，遮住桌面内容。默认 0.5。
    /// </summary>
    public double AcrylicTintOpacity { get; set; } = 0.5;
}
