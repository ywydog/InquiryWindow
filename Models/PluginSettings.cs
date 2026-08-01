namespace InquiryWindow.Models;

/// <summary>
/// 插件级全局设置。落盘到 <c>&lt;PluginConfig&gt;/plugin-settings.json</c>。
/// 与 Action 级别的 <see cref="InquiryWindowActionSettings"/> 不同：这里放的是
/// 跨 Action 共享、且与「插件本身」相关的选项。
///
/// Android 兼容说明（new/for-android-2.2 分支专用）：
/// 移除了亚克力背景相关字段（WindowTransparencyLevel.AcrylicBlur 在 Android 上不支持）。
/// </summary>
public class PluginSettings
{
}
