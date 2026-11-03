using System;
using System.Collections.Generic;
using System.Text;

namespace InquiryWindow.Helpers.SimpleExpression;

/// <summary>
/// 极简函数表达式解析器：把 <c>name("参数1", "参数2")</c> 形式的字符串拆成函数名与参数列表。
///
/// <para>
/// 宿主 ClassIsland 的图标表达式协议（<c>fluent("…")</c> / <c>lucide("…")</c> / <c>img("…")</c>）
/// 由 <c>ClassIsland.Core.Helpers.UI.IconExpressionHelper</c> 解析，但其中的表达式解析器是
/// internal 的，插件侧无法直接复用。这里实现一个行为一致的版本，用于把已保存的表达式
/// 拆成函数名与参数，供图标编辑器预选标签页、回填参数与做只读展示。
/// </para>
/// </summary>
internal static class SimpleExpressionParser
{
    /// <summary>
    /// 解析表达式。格式非法时抛出 <see cref="FormatException"/>。
    /// </summary>
    public static Expression Parse(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var cursor = new Cursor(input);
        cursor.SkipWhiteSpace();

        var functionName = cursor.ReadIdentifier();
        if (functionName.Length == 0)
        {
            throw new FormatException("缺少函数名。");
        }

        cursor.SkipWhiteSpace();
        if (!cursor.TryConsume('('))
        {
            throw new FormatException("函数名后缺少左括号 '('。");
        }

        var arguments = cursor.ReadArguments();

        cursor.SkipWhiteSpace();
        if (!cursor.IsAtEnd)
        {
            throw new FormatException("表达式结尾存在多余字符。");
        }

        return new Expression(functionName, arguments);
    }

    /// <summary>
    /// 带游标状态的解析辅助器。
    /// </summary>
    private struct Cursor(string text)
    {
        private readonly string _text = text;
        private int _index;

        public bool IsAtEnd => _index >= _text.Length;

        public void SkipWhiteSpace()
        {
            while (_index < _text.Length && char.IsWhiteSpace(_text[_index]))
            {
                _index++;
            }
        }

        public bool TryConsume(char expected)
        {
            SkipWhiteSpace();
            if (_index < _text.Length && _text[_index] == expected)
            {
                _index++;
                return true;
            }
            return false;
        }

        public string ReadIdentifier()
        {
            SkipWhiteSpace();
            var start = _index;
            while (_index < _text.Length &&
                   (char.IsLetterOrDigit(_text[_index]) || _text[_index] is '_' or '.'))
            {
                _index++;
            }
            return _text[start.._index].Trim();
        }

        public string[] ReadArguments()
        {
            var arguments = new List<string>();

            SkipWhiteSpace();
            if (_index < _text.Length && _text[_index] == ')')
            {
                _index++;
                return arguments.ToArray();
            }

            while (true)
            {
                SkipWhiteSpace();
                if (IsAtEnd)
                {
                    throw new FormatException("解析参数时意外到达字符串结尾。");
                }

                arguments.Add(_text[_index] is '"' or '\'' ? ReadQuoted() : ReadUnquoted());

                SkipWhiteSpace();
                if (IsAtEnd)
                {
                    throw new FormatException("解析参数后意外到达字符串结尾。");
                }

                switch (_text[_index])
                {
                    case ',':
                        _index++;
                        continue;
                    case ')':
                        _index++;
                        return arguments.ToArray();
                    default:
                        throw new FormatException(
                            $"参数后应出现 ',' 或 ')'，但在位置 {_index} 遇到 '{_text[_index]}'。");
                }
            }
        }

        private string ReadQuoted()
        {
            var quote = _text[_index++];
            var builder = new StringBuilder();
            while (_index < _text.Length)
            {
                var current = _text[_index++];
                if (current == '\\')
                {
                    if (IsAtEnd)
                    {
                        throw new FormatException("字符串结尾存在不完整的转义序列。");
                    }
                    var escaped = _text[_index++];
                    builder.Append(escaped switch
                    {
                        '\\' => '\\',
                        '"' => '"',
                        '\'' => '\'',
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        _ => escaped
                    });
                    continue;
                }

                if (current == quote)
                {
                    return builder.ToString();
                }
                builder.Append(current);
            }

            throw new FormatException("字符串参数缺少结束引号。");
        }

        private string ReadUnquoted()
        {
            var start = _index;
            while (_index < _text.Length && _text[_index] is not (',' or ')'))
            {
                _index++;
            }
            return _text[start.._index].Trim();
        }
    }
}

/// <summary>
/// 解析结果：函数名 + 参数列表。
/// </summary>
internal sealed class Expression(string functionName, string[] arguments)
{
    public string FunctionName { get; } = functionName;

    public string[] Arguments { get; } = arguments;
}
