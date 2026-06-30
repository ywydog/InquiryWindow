using System.IO;
using System.Runtime.Versioning;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using SysIcon = System.Drawing.Icon;

namespace InquiryWindow.Services;

/// <summary>
/// 封装 exe 图标提取。失败时返回 null，调用方应自行处理 null 情况。
/// </summary>
public static class IconExtractorService
{
    /// <summary>
    /// 尝试从 .exe 文件提取关联图标并转为 Avalonia Bitmap。
    /// </summary>
    /// <param name="path">目标路径</param>
    /// <returns>成功返回 Bitmap，失败返回 null</returns>
    [SupportedOSPlatform("windows")]
    public static AvaloniaBitmap? TryExtract(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (!File.Exists(path)) return null;
        if (!string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)) return null;

        try
        {
            using var icon = SysIcon.ExtractAssociatedIcon(path);
            if (icon == null) return null;

            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            return new AvaloniaBitmap(ms);
        }
        catch
        {
            return null;
        }
    }
}
