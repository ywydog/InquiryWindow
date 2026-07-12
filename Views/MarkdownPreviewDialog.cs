using System.Threading.Tasks;
using Avalonia.Controls;
using ClassIsland.Core.Helpers;
using FluentAvalonia.UI.Controls;
using Markdown.Avalonia;

namespace InquiryWindow.Views;

/// <summary>
/// 独立的 Markdown 预览弹窗：用户点设置页的"预览"按钮时弹出，
/// 让用户看到用 MarkdownConvertHelper.Engine 渲染后的真实效果。
/// 不在设置页里实时内联渲染（用户要求点按钮才看）。
/// </summary>
public static class MarkdownPreviewDialog
{
    /// <summary>
    /// 弹出 Markdown 预览。
    /// </summary>
    /// <param name="owner">弹窗宿主 TopLevel（一般传当前设置页所在的 Window）。</param>
    /// <param name="title">弹窗标题（默认"Markdown 预览"）。</param>
    /// <param name="markdown">要渲染的 Markdown 源文本。</param>
    public static async Task ShowAsync(TopLevel owner, string title, string markdown)
    {
        // 用 Border 包一层跟设置页/弹窗一致的浅灰底 + 圆角，
        // 模拟实际询问窗正文的视觉容器，让用户看到的就是弹出来的样子。
        var viewer = new MarkdownScrollViewer
        {
            Engine = MarkdownConvertHelper.Engine,
            Markdown = markdown,
            MinHeight = 60,
            MaxHeight = 480
        };

        var container = new Border
        {
            Padding = new Avalonia.Thickness(12),
            CornerRadius = new Avalonia.CornerRadius(6),
            Background = Avalonia.Application.Current?.FindResource("LayerFillColorAltBrush")
                as Avalonia.Media.IBrush,
            Child = viewer
        };

        var dialog = new ContentDialog
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Markdown 预览" : title,
            Content = container,
            PrimaryButtonText = "关闭",
            DefaultButton = ContentDialogButton.Primary
        };

        await dialog.ShowAsync(owner);
    }
}
