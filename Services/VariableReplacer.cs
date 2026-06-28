using System.Text.RegularExpressions;
using ClassIsland.Core.Abstractions.Services;

namespace InquiryWindow.Services;

/// <summary>
/// 占位符替换。支持 {time} {date} {subject} {nextSubject}。
/// 找不到对应值时渲染为空字符串，不抛异常，不保留原 {xxx} 文本。
/// </summary>
public static class VariableReplacer
{
    private static readonly Regex VarPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);

    public static string Replace(string? template, ILessonsService lessons, IExactTimeService time)
    {
        if (string.IsNullOrEmpty(template)) return "";

        var now = time.GetCurrentLocalDateTime();
        return VarPattern.Replace(template, match =>
        {
            return match.Groups[1].Value switch
            {
                "time"        => now.ToString("HH:mm"),
                "date"        => now.ToString("yyyy-MM-dd"),
                "subject"     => lessons.CurrentSubject?.Name ?? "",
                "nextSubject" => lessons.NextClassSubject?.Name ?? "",
                _ => ""
            };
        });
    }
}
