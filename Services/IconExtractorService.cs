using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace InquiryWindow.Services;

/// <summary>
/// 封装 exe 图标提取。失败时返回 null，调用方应自行处理 null 情况。
/// 实现按平台拆分：<see cref="IconExtractorService.Windows"/> 在 Windows 上调
/// <c>System.Drawing.Icon.ExtractAssociatedIcon</c>；非 Windows 平台见
/// <see cref="IconExtractorService.Other"/>，直接返回 null。
/// </summary>
public static partial class IconExtractorService
{
    /// <summary>
    /// 尝试从 .exe 文件提取关联图标并转为 Avalonia Bitmap。
    /// </summary>
    /// <param name="path">目标路径</param>
    /// <returns>成功返回 Bitmap，失败返回 null</returns>
    public static AvaloniaBitmap? TryExtract(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (!File.Exists(path)) return null;
        if (!string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)) return null;
        return ExtractIcon(path);
    }

    /// <summary>平台相关实现：在 Windows 上用 System.Drawing；其他平台返回 null。</summary>
    private static partial AvaloniaBitmap? ExtractIcon(string path);
}
