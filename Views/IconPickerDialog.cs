using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using ClassIsland.Core.Controls;
using FluentAvalonia.UI.Controls;

namespace InquiryWindow.Views;

/// <summary>
/// Fluent SystemIcons 字体（私有区 0xE000..0xF4D3）图标选择器。
/// 思路参考 SystemTools/FloatingWindowTriggerSettings：
/// - 一次性缓存所有 glyph 字符
/// - 弹窗用 ListBox + VirtualizingStackPanel + 每行 WrapPanel 做虚拟化
/// - 返回点击的字符（已是真实 Unicode glyph），未选则返回 null
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

        var dialog = new ContentDialog
        {
            Title = title,
            PrimaryButtonText = "关闭",
            DefaultButton = ContentDialogButton.Primary
        };

        string? selected = null;
        dialog.Content = BuildPickerContent(rows, token =>
        {
            selected = token;
            dialog.Hide();
        }, highlightGlyph);

        await dialog.ShowAsync(owner);
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
