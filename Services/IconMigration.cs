using System;
using System.Collections.Generic;
using ClassIsland.Core.Icons;
using InquiryWindow.Helpers.SimpleExpression;

namespace InquiryWindow.Services;

/// <summary>
/// 旧图标码点迁移。
///
/// <para>
/// 早期版本按 Segoe MDL2 的习惯把码点写进配置（例如把 <c>\uE82D</c> 当作“多按钮询问”），
/// 但 ClassIsland 实际使用 Fluent System Icons，同一个码点对应完全不同的字形。
/// 这里只迁移旧版本插件自己写入的默认值，用户后来从 Fluent 图标选择器里选的图标保持不变。
/// </para>
/// </summary>
internal static class IconMigration
{
    /// <summary>
    /// 旧默认码点 → 语义最接近的 Fluent 图标。
    /// </summary>
    private static readonly IReadOnlyDictionary<char, string> LegacyDefaults = new Dictionary<char, string>
    {
        ['\uE10F'] = FluentIcons.QuestionCircleRegular, // 按钮 / 预设默认图标
        ['\uE82D'] = FluentIcons.ChatMultipleRegular,   // 多按钮询问窗口默认图标
        ['\uE73E'] = FluentIcons.CheckmarkRegular,      // 默认「执行」按钮
        ['\uE711'] = FluentIcons.DismissRegular,        // 默认「取消」按钮
    };

    /// <summary>
    /// 尝试把单个图标值迁移为 <c>fluent("…")</c> 表达式。
    /// 支持 <c>fluent("…")</c>、裸字形字符与历史 <c>/uXXXX</c> 标记。
    /// </summary>
    /// <param name="expression">配置中保存的图标值。</param>
    /// <param name="migrated">迁移后的表达式；未命中时为 <see cref="string.Empty"/>。</param>
    /// <returns>发生迁移返回 <c>true</c>。</returns>
    public static bool TryMigrate(string? expression, out string migrated)
    {
        migrated = string.Empty;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        var value = expression.Trim();

        // 历史 /uXXXX、\uXXXX 标记，以及旧图标选择器直接返回的单个字形字符。
        if (IconExpressionService.TryGetLegacyGlyph(value, out var legacyGlyph))
        {
            return TryBuildExpression(legacyGlyph, out migrated);
        }

        if (value.Length == 1)
        {
            return TryBuildExpression(value, out migrated);
        }

        // fluent("…") 表达式：只替换参数里的旧默认字形。
        try
        {
            var parsed = SimpleExpressionParser.Parse(value);
            if (parsed.Arguments.Length == 1 &&
                parsed.FunctionName.Equals("fluent", StringComparison.OrdinalIgnoreCase) &&
                TryBuildExpression(parsed.Arguments[0], out migrated))
            {
                return true;
            }
        }
        catch (FormatException)
        {
            // 不是可解析的表达式，保持原值。
        }

        return false;
    }

    private static bool TryBuildExpression(string glyph, out string expression)
    {
        expression = string.Empty;
        if (glyph.Length != 1 || !LegacyDefaults.TryGetValue(glyph[0], out var fluentGlyph))
        {
            return false;
        }

        expression = IconExpressionService.FormatExpression("fluent", fluentGlyph);
        return true;
    }
}
