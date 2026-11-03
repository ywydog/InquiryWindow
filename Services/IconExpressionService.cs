using System;
using System.Globalization;
using ClassIsland.Core.Controls.IconSources;
using ClassIsland.Core.Helpers.UI;
using FluentAvalonia.UI.Controls;

namespace InquiryWindow.Services;

/// <summary>
/// 图标表达式统一入口。完全采用宿主 ClassIsland 的图标表达式协议：
/// 配置只保存字符串表达式（如 <c>fluent("…")</c>、<c>lucide("…")</c>、<c>img("C:\…")</c>），
/// 渲染时解析为 <see cref="IconSource"/>，并兼容历史数据里的单个字形字符与 <c>/uXXXX</c> 标记。
/// </summary>
public static class IconExpressionService
{
    /// <summary>
    /// 新按钮 / 新预设使用的默认 Fluent 字形（与原单字形默认值保持一致）。
    /// </summary>
    public const string DefaultFluentGlyph = "\uE10F";

    /// <summary>
    /// 新按钮 / 新预设使用的默认图标表达式。
    /// </summary>
    public static string DefaultExpression => FormatExpression("fluent", DefaultFluentGlyph);

    private static bool _imageHandlerRegistered;
    private static readonly object ImageHandlerLock = new();

    /// <summary>
    /// 按 ClassIsland 的转义规则，把参数格式化为 <c>func("参数")</c> 形式的图标表达式。
    /// </summary>
    public static string FormatExpression(string function, string argument) =>
        $"{function}(\"" +
        argument.Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t") +
        "\")";

    /// <summary>
    /// 是否为历史遗留的 <c>/uXXXX</c> / <c>\uXXXX</c> 图标标记；是则输出对应字形字符。
    /// </summary>
    public static bool TryGetLegacyGlyph(string? raw, out string glyph)
    {
        glyph = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var value = raw.Trim();
        if (value.Length <= 2)
        {
            return false;
        }

        if (!value.StartsWith("/u", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("\\u", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!int.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var codePoint))
        {
            return false;
        }

        glyph = char.ConvertFromUtf32(codePoint);
        return true;
    }

    /// <summary>
    /// 解析图标表达式为 <see cref="FAIconSource"/>。支持表达式、单个字形字符与 <c>/uXXXX</c> 标记；
    /// 解析失败或为空时返回 <c>null</c>。
    /// </summary>
    public static FAIconSource? Resolve(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (TryGetLegacyGlyph(value, out var tokenGlyph))
        {
            value = tokenGlyph;
        }

        if (value.StartsWith("img(", StringComparison.OrdinalIgnoreCase))
        {
            EnsureImageHandler();
        }

        return IconExpressionHelper.TryParseOrNull(value);
    }

    /// <summary>
    /// 确保注册 <c>img</c> 图标表达式处理器。宿主较新版本或其它插件若已注册（重复注册会抛异常）
    /// 则自动跳过，保证图片图标在所有受支持版本上可用。
    /// </summary>
    private static void EnsureImageHandler()
    {
        if (_imageHandlerRegistered)
        {
            return;
        }

        lock (ImageHandlerLock)
        {
            if (_imageHandlerRegistered)
            {
                return;
            }

            _imageHandlerRegistered = true;
            try
            {
                if (IconExpressionHelper.IconExpressionHandlers.ContainsKey("img"))
                {
                    return;
                }

                IconExpressionHelper.RegisterHandler("img", args =>
                    args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                        ? new AdvancedImageIconSource { Uri = args[0] }
                        : null);
            }
            catch (ArgumentException)
            {
                // 宿主或其它插件已经注册过 img 处理器，跳过即可。
            }
        }
    }
}
