using System;
using System.Globalization;
using System.Text;
using ClassIsland.Core.Controls.IconSources;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.Shared;
using FluentAvalonia.UI.Controls;
using InquiryWindow.Helpers.SimpleExpression;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    /// 记录一次图标变更日志（含码点），用于排查图标改动是否生效。
    /// 图片图标没有码点，日志中改为输出图片路径。
    /// </summary>
    public static void LogIconChange(string source, string? oldExpression, string? newExpression)
    {
        var logger = GetLogger();
        logger.LogInformation(
            "[图标变更] {Source}：{OldExpression}（{OldCodePoints}） → {NewExpression}（{NewCodePoints}）",
            source,
            string.IsNullOrWhiteSpace(oldExpression) ? "空" : oldExpression,
            DescribeCodePoints(oldExpression),
            string.IsNullOrWhiteSpace(newExpression) ? "空" : newExpression,
            DescribeCodePoints(newExpression));
    }

    /// <summary>
    /// 把图标表达式解析为便于阅读的码点描述，如 <c>U+E710</c>；
    /// 图片图标返回路径并标注无码点，无法解析时返回「未知」。
    /// </summary>
    public static string DescribeCodePoints(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return "无码点";
        }

        var value = expression.Trim();
        if (TryGetLegacyGlyph(value, out var legacyGlyph))
        {
            return FormatGlyphCodePoints(legacyGlyph);
        }

        try
        {
            var parsed = SimpleExpressionParser.Parse(value);
            if (parsed.FunctionName.Equals("img", StringComparison.OrdinalIgnoreCase))
            {
                var path = parsed.Arguments.FirstOrDefault();
                return $"图片（{path ?? "未指定路径"}，无码点）";
            }

            if (parsed.FunctionName.Equals("fluent", StringComparison.OrdinalIgnoreCase) ||
                parsed.FunctionName.Equals("lucide", StringComparison.OrdinalIgnoreCase))
            {
                return FormatGlyphCodePoints(parsed.Arguments.FirstOrDefault() ?? "");
            }
        }
        catch (FormatException)
        {
            // 非法表达式按下面的字形字符兜底处理。
        }

        // 历史数据里的单个字形字符。
        return FormatGlyphCodePoints(value);
    }

    private static string FormatGlyphCodePoints(string glyph)
    {
        if (string.IsNullOrEmpty(glyph))
        {
            return "无码点";
        }

        var builder = new StringBuilder();
        foreach (var rune in glyph.EnumerateRunes())
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(CultureInfo.InvariantCulture, $"U+{rune.Value:X4}");
        }

        return builder.ToString();
    }

    private static ILogger? _logger;

    private static ILogger GetLogger()
    {
        if (_logger is not null)
        {
            return _logger;
        }

        try
        {
            _logger = IAppHost.GetService<ILoggerFactory>().CreateLogger("InquiryWindow.Icon");
            return _logger;
        }
        catch (Exception)
        {
            // 宿主尚未初始化完成（或运行在无日志服务的环境）时退回静默日志。
            // 不缓存回退实例，等宿主就绪后再解析真实 Logger。
            return NullLogger.Instance;
        }
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
