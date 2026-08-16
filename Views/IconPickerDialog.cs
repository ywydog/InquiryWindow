using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ClassIsland.Core.Controls;

namespace InquiryWindow.Views;

/// <summary>
/// Fluent SystemIcons 字体（私有区 0xE000..0xF4D3）图标选择器。
/// 思路参考 SystemTools/FloatingWindowTriggerSettings：
/// - 一次性缓存所有 glyph 字符
/// - 弹窗用 ListBox + VirtualizingStackPanel + 每行 WrapPanel 做虚拟化
/// - 返回点击的字符（已是真实 Unicode glyph），未选则返回 null
///
/// Android 兼容：Android 的 AOT 会裁剪 <c>FAContentDialog.Hide(...)</c>，因此这里改用
/// 核心控件 <see cref="Popup"/> 承载内容，通过 <c>IsOpen</c> 实现打开/关闭。
/// </summary>
public static class IconPickerDialog
{
    private const int IconCodeStart = 0xE000;
    private const int IconCodeEnd = 0xF4D3;

    private const int IconCell = 36;       // 每个按钮 36×36
    private const int CellPadding = 1;     // 按钮 margin
    private const int IconFontSize = 21;
    private const int ContentHeight = 520; // 弹窗内容区固定高度

    // 静态缓存：仅初始化一次。
    private static List<string>? _iconGlyphs;

    /// <summary>
    /// 弹出图标选择器。
    /// </summary>
    /// <param name="owner">弹窗宿主 TopLevel。</param>
    /// <param name="title">弹窗标题。</param>
    /// <param name="highlightGlyph">当前已选 glyph（用于高亮，不传则不高亮）。</param>
    /// <returns>选中的 glyph 字符；用户取消则返回 null。</returns>
    public static async Task<string?> PickAsync(TopLevel owner, string title = "选择图标", string? highlightGlyph = null)
    {
        EnsureGlyphsLoaded();
        var rows = BuildVirtualizedRows(columns: 8);

        var popup = new Popup
        {
            IsLightDismissEnabled = false,
            Placement = PlacementMode.Center
        };

        string? selected = null;
        var picker = BuildPickerContent(rows, token =>
        {
            selected = token;
            popup.IsOpen = false;
        }, highlightGlyph);

        // 用 Border 包一层标题噪音最小的干净背景，替换原来 ContentDialog 的自带框架。
        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(4, 0, 4, 8)
        };
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        var closeButton = new Button
        {
            Content = "关闭",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        closeButton.Click += (_, _) => popup.IsOpen = false;
        header.Children.Add(closeButton);

        var root = new StackPanel
        {
            Spacing = 4,
            Children = { header, picker }
        };

        popup.Child = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(8),
            Background = Avalonia.Application.Current?.FindResource("LayerFillColorAltBrush")
                as Avalonia.Media.IBrush,
            Child = root
        };

        var tcs = new TaskCompletionSource();
        popup.Closed += (_, _) => tcs.TrySetResult();
        popup.IsOpen = true;
        await tcs.Task;
        return selected;
    }

    private static void EnsureGlyphsLoaded()
    {
        if (_iconGlyphs is { Count: > 0 }) return;
        _iconGlyphs = new List<string>(IconCodeEnd - IconCodeStart + 1);
        for (var code = IconCodeStart; code <= IconCodeEnd; code++)
        {
            _iconGlyphs.Add(char.ConvertFromUtf32(code));
        }
    }

    /// <summary>
    /// 把 glyph 列表按 columns 个一组切成"行"，每行用 WrapPanel 渲染。
    /// ListBox + VirtualizingStackPanel 让我们只渲染可见行，避免 5400+ 控件一次性建出来。
    /// </summary>
    private static ObservableCollection<IconRow> BuildVirtualizedRows(int columns)
    {
        var rows = new ObservableCollection<IconRow>();
        if (_iconGlyphs is null) return rows;

        foreach (var chunk in _iconGlyphs.Chunk(columns))
        {
            rows.Add(new IconRow(chunk.ToList()));
        }
        return rows;
    }

    private static Control BuildPickerContent(ObservableCollection<IconRow> rows, Action<string> onPick, string? highlightGlyph)
    {
        var listBox = new ListBox
        {
            ItemsSource = rows,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Avalonia.Media.Brushes.Transparent,
            Height = ContentHeight
        };
        listBox.ItemsPanel = new FuncTemplate<Panel>(() => new VirtualizingStackPanel());
        listBox.ItemTemplate = new FuncDataTemplate<IconRow?>((row, _) => BuildIconRow(row, onPick, highlightGlyph));
        ScrollViewer.SetVerticalScrollBarVisibility(listBox, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(listBox, ScrollBarVisibility.Disabled);

        return new Border { Padding = new Thickness(8), Child = listBox };
    }

    private static Control BuildIconRow(IconRow? row, Action<string> onPick, string? highlightGlyph)
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemWidth = IconCell,
            ItemHeight = IconCell,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        if (row?.Tokens is not { Count: > 0 })
        {
            return panel;
        }

        foreach (var glyph in row.Tokens)
        {
            panel.Children.Add(BuildIconButton(glyph, onPick, highlightGlyph));
        }
        return panel;
    }

    private static Button BuildIconButton(string glyph, Action<string> onPick, string? highlightGlyph)
    {
        var button = new Button
        {
            Width = IconCell - CellPadding * 2,
            Height = IconCell - CellPadding * 2,
            Margin = new Thickness(CellPadding),
            Padding = new Thickness(0),
            Content = new FluentIcon
            {
                Glyph = glyph,
                FontSize = IconFontSize,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        // 当前选中的图标加 accent 类高亮
        if (!string.IsNullOrEmpty(highlightGlyph) && string.Equals(highlightGlyph, glyph, StringComparison.Ordinal))
        {
            button.Classes.Add("accent");
        }
        button.Click += (_, _) => onPick(glyph);
        return button;
    }

    private sealed record IconRow(IReadOnlyList<string> Tokens);
}
