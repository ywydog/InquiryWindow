using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Media;
using ClassIsland.Core;
using ClassIsland.Core.Controls;
using ClassIsland.Core.Icons;
using CommunityToolkit.Mvvm.ComponentModel;
using InquiryWindow.Helpers.SimpleExpression;
using InquiryWindow.Services;
using FluentAvalonia.UI.Controls;

namespace InquiryWindow.Models.UI;

/// <summary>
/// 图标表达式编辑器的 ViewModel：维护 Fluent / Lucide / 图像三类图标的展示与选择，
/// 并把用户的选择回吐为宿主协议的图标表达式字符串。
/// </summary>
internal partial class IconExpressionEditorViewModel : ObservableObject
{
    private static readonly Lazy<IconDescriptor[]> FluentCatalog = new(() => BuildCatalog<FluentIconKind>());
    private static readonly Lazy<IconDescriptor[]> LucideCatalog = new(() => BuildCatalog<LucideIconKind>());

    private readonly IconExpressionEditorItem[]?[] _cache = new IconExpressionEditorItem[2][];
    private bool _synchronizing;
    private bool _activated;
    private int _expressionType = -1;
    private string? _glyph;
    private IconExpressionEditorItem? _selectedItem;

    /// <summary>用户选定某个图标后触发，携带最终的图标表达式。</summary>
    public event Action<string>? ExpressionEdited;

    [ObservableProperty] private string _imagePath = "";
    [ObservableProperty] private string _query = "";
    [ObservableProperty] private int _selectedType;
    [ObservableProperty] private FAIconSource _preview = new FluentIconSource(IconExpressionService.DefaultFluentGlyph);
    [ObservableProperty] private string _message = "";
    [ObservableProperty] private bool _isBrowsing;
    [ObservableProperty] private IReadOnlyList<IconExpressionEditorItem> _filteredIcons = [];

    /// <summary>当前是否处于「图像」标签页。</summary>
    public bool IsImage => SelectedType == 2;

    /// <summary>当前筛选结果是否为空（用于显示空状态提示）。</summary>
    public bool HasNoResults => FilteredIcons.Count == 0;

    /// <summary>首次打开 Flyout 时调用，开始构建图标目录。</summary>
    public void Activate()
    {
        _activated = true;
        RefreshIcons();
    }

    /// <summary>
    /// 载入外部表达式：解析出预览图标、标签页与参数。无法解析的值保持原样，
    /// 直到用户主动选择新的图标才被替换。
    /// </summary>
    public void ApplyExpression(string? expression)
    {
        _synchronizing = true;
        try
        {
            var imagePath = "";
            _expressionType = -1;
            _glyph = null;

            var source = IconExpressionService.Resolve(expression);
            Preview = source ?? new FluentIconSource(IconExpressionService.DefaultFluentGlyph);
            Message = !string.IsNullOrWhiteSpace(expression) && source == null
                ? "无法预览此图标，请选择其它图标。"
                : "";

            if (expression?.Length == 1)
            {
                _expressionType = 0;
                _glyph = expression;
            }
            else if (!string.IsNullOrWhiteSpace(expression))
            {
                try
                {
                    var parsed = SimpleExpressionParser.Parse(expression);
                    // 仅映射不会丢失参数、且能完整还原的表达式。
                    if (parsed.Arguments.Length == 1)
                    {
                        _expressionType = parsed.FunctionName switch
                        {
                            "fluent" => 0,
                            "lucide" => 1,
                            "img" => 2,
                            _ => -1
                        };
                        if (_expressionType == 2)
                        {
                            imagePath = parsed.Arguments[0];
                        }
                        else if (_expressionType >= 0)
                        {
                            _glyph = parsed.Arguments[0];
                        }
                    }
                }
                catch (FormatException)
                {
                    // 外部传入的非法表达式保持原样，等待用户选择后替换。
                }
            }

            ImagePath = imagePath;
            if (_expressionType >= 0)
            {
                SelectedType = _expressionType;
            }
            UpdateSelection();
        }
        finally
        {
            _synchronizing = false;
        }
    }

    partial void OnImagePathChanged(string value)
    {
        if (!_synchronizing)
        {
            EditExpression(string.IsNullOrWhiteSpace(value)
                ? ""
                : IconExpressionService.FormatExpression("img", value));
        }
    }

    partial void OnSelectedTypeChanged(int value)
    {
        OnPropertyChanged(nameof(IsImage));
        RefreshIcons();
    }

    partial void OnQueryChanged(string value) => RefreshIcons();

    partial void OnFilteredIconsChanged(IReadOnlyList<IconExpressionEditorItem> value) =>
        OnPropertyChanged(nameof(HasNoResults));

    /// <summary>选中某个字体图标，回吐对应的图标表达式。</summary>
    public void SelectIcon(IconExpressionEditorItem item) =>
        EditExpression(IconExpressionService.FormatExpression(item.IsLucide ? "lucide" : "fluent", item.Glyph));

    private void EditExpression(string expression)
    {
        ApplyExpression(expression);
        ExpressionEdited?.Invoke(expression);
    }

    private void RefreshIcons()
    {
        if (!_activated || SelectedType is < 0 or > 1)
        {
            FilteredIcons = [];
            return;
        }

        var items = _cache[SelectedType] ??=
            (SelectedType == 0 ? FluentCatalog : LucideCatalog).Value
                .Select(x => new IconExpressionEditorItem(x, SelectedType == 1))
                .ToArray();

        var query = Query.Trim();
        if (query.Length == 0)
        {
            FilteredIcons = items;
            UpdateSelection();
            return;
        }

        var normalizedName = RemoveSeparators(query);
        var code = query;
        if (code.StartsWith("U+", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            code.StartsWith("\\u", StringComparison.OrdinalIgnoreCase))
        {
            code = code[2..];
        }
        var hasCodePoint = int.TryParse(code, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codePoint);

        FilteredIcons = items.Where(x =>
            x.Descriptor.SearchName.Contains(normalizedName, StringComparison.OrdinalIgnoreCase) ||
            (hasCodePoint && x.Descriptor.CodePoint == codePoint) ||
            x.Glyph == query).ToArray();
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        if (_selectedItem != null)
        {
            _selectedItem.IsSelected = false;
        }

        _selectedItem = _expressionType is 0 or 1
            ? _cache[_expressionType]?.FirstOrDefault(x => x.Glyph == _glyph)
            : null;

        if (_selectedItem != null)
        {
            _selectedItem.IsSelected = true;
        }
    }

    private static string RemoveSeparators(string name) =>
        string.Concat(name.Where(x => !char.IsWhiteSpace(x) && x is not '-' and not '_'));

    private static IconDescriptor[] BuildCatalog<T>() where T : struct, Enum =>
        Enum.GetNames<T>()
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(name =>
            {
                var codePoint = Convert.ToInt32(Enum.Parse<T>(name), CultureInfo.InvariantCulture);
                return new IconDescriptor(name, char.ConvertFromUtf32(codePoint), codePoint, RemoveSeparators(name));
            })
            .ToArray();
}

/// <summary>图标目录项的描述信息。</summary>
internal sealed record IconDescriptor(string Name, string Glyph, int CodePoint, string SearchName);

/// <summary>图标列表中的单个图标（含选中态）。</summary>
internal partial class IconExpressionEditorItem(IconDescriptor descriptor, bool isLucide) : ObservableObject
{
    public IconDescriptor Descriptor { get; } = descriptor;

    public bool IsLucide { get; } = isLucide;

    public string Glyph => Descriptor.Glyph;

    public string Description => $"{Descriptor.Name} · U+{Descriptor.CodePoint:X4}";

    public FontFamily FontFamily => IsLucide ? AppBase.LucideIconsFontFamily : AppBase.FluentIconsFontFamily;

    [ObservableProperty] private bool _isSelected;
}
