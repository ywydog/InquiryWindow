using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace InquiryWindow.Services;

/// <summary>
/// IconExtractorService 的非 Windows 实现：直接返回 null（不调用 System.Drawing）。
/// 在 Android / macOS / Linux 等不支持 <c>System.Drawing.Common</c> 的平台上使用。
/// </summary>
public static partial class IconExtractorService
{
    private static partial AvaloniaBitmap? ExtractIcon(string path) => null;
}
