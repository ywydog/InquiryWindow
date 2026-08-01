using System.IO;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace InquiryWindow.Services;

/// <summary>
/// 封装 exe 图标提取。失败时返回 null，调用方应自行处理 null 情况。
///
/// 平台说明（new/for-android-2.2 分支专用）：
/// 本分支是面向 ClassIsland Android 的"无 System.Drawing.Common"实现，
/// Android 上 .NET 不提供 GDI+，因此本类不实现任何实际提取逻辑，
/// 始终返回 null。Windows-only 的实现见 main / new/for2.2 分支的
/// <c>IconExtractorService.Windows.cs</c>。
/// </summary>
public static class IconExtractorService
{
    /// <summary>
    /// 尝试从 .exe 文件提取关联图标并转为 Avalonia Bitmap。
    /// </summary>
    /// <param name="path">目标路径</param>
    /// <returns>Android 分支始终返回 null（不支持 GDI+）。</returns>
    public static AvaloniaBitmap? TryExtract(string? path) => null;
}
