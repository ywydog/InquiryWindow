using System.Runtime.Versioning;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using SysIcon = System.Drawing.Icon;

namespace InquiryWindow.Services;

/// <summary>
/// IconExtractorService 的 Windows 实现：使用 <c>System.Drawing.Common</c> 提取 .exe 图标。
/// 仅在 <c>System.Drawing.Common</c> 包可用的平台编译（见 csproj Condition）。
/// </summary>
public static partial class IconExtractorService
{
    [SupportedOSPlatform("windows")]
    private static partial AvaloniaBitmap? ExtractIcon(string path)
    {
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
