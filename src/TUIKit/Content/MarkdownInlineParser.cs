namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit;

    // Parses inline Markdown (emphasis, strong, strike, code spans, links, images, autolinks, escapes) into styled
    // text for the width-aware layout. Emphasis nests. Unmatched delimiters render literally; never throws.
    internal sealed class MarkdownInlineParser
    {
        private const string _EscapableCharacters = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

        private readonly MarkdownRenderOptions _Options;
        private readonly List<StyledSpan> _Spans = new List<StyledSpan>();
        private readonly StringBuilder _Run = new StringBuilder();
        private CellStyle _RunStyle;
        private bool _HasRun;

        private MarkdownInlineParser(MarkdownRenderOptions options)
        {
            _Options = options;
        }

        internal static StyledText Parse(string text, CellStyle baseStyle, MarkdownRenderOptions options)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            MarkdownInlineParser parser = new MarkdownInlineParser(options);
            parser.ParseRange(text, 0, text.Length, baseStyle);
            parser.Flush();
            if (parser._Spans.Count == 0)
                return StyledText.Empty;

            return new StyledText(parser._Spans);
        }

        private void ParseRange(string text, int start, int end, CellStyle style)
        {
            MarkdownStyles styles = _Options.Styles;
            int i = start;
            while (i < end)
            {
                char c = text[i];

                if (c == '\\' && i + 1 < end && _EscapableCharacters.IndexOf(text[i + 1]) >= 0)
                {
                    Emit(text[i + 1].ToString(), style);
                    i += 2;
                    continue;
                }

                if (c == '`')
                {
                    int run = RunLength(text, i, end, '`');
                    int close = FindBacktickRun(text, i + run, end, run);
                    if (close >= 0)
                    {
                        string code = text.Substring(i + run, close - (i + run));
                        if (code.Length >= 2 && code[0] == ' ' && code[code.Length - 1] == ' ' && code.Trim().Length > 0)
                            code = code.Substring(1, code.Length - 2);

                        Emit(code, MarkdownStyles.Overlay(style, styles.InlineCode));
                        i = close + run;
                        continue;
                    }

                    Emit(text.Substring(i, run), style);
                    i += run;
                    continue;
                }

                if (c == '!' && i + 1 < end && text[i + 1] == '[')
                {
                    if (TryLink(text, i + 1, end, out int labelEnd, out int afterLink, out string url))
                    {
                        string alt = text.Substring(i + 2, labelEnd - (i + 2));
                        Emit("[image: ", style);
                        ParseRange(alt, 0, alt.Length, style);
                        Emit("]", style);
                        i = afterLink;
                        continue;
                    }
                }

                if (c == '[')
                {
                    if (TryLink(text, i, end, out int labelEnd, out int afterLink, out string url))
                    {
                        CellStyle linkStyle = MarkdownStyles.Overlay(style, styles.Link);
                        string label = text.Substring(i + 1, labelEnd - (i + 1));
                        ParseRange(label, 0, label.Length, linkStyle);
                        if (_Options.ShowLinkUrls && url.Length > 0 && !string.Equals(label.Trim(), url, StringComparison.Ordinal))
                            Emit(" (" + url + ")", MarkdownStyles.Overlay(style, styles.LinkUrl));

                        i = afterLink;
                        continue;
                    }
                }

                if (c == '<')
                {
                    int close = TryAutolink(text, i, end);
                    if (close > 0)
                    {
                        Emit(text.Substring(i + 1, close - i - 1), MarkdownStyles.Overlay(style, styles.Link));
                        i = close + 1;
                        continue;
                    }
                }

                if (c == '~' && RunLength(text, i, end, '~') >= 2)
                {
                    int run = RunLength(text, i, end, '~');
                    int close = FindCloser(text, i + 2, end, '~', 2);
                    if (run == 2 && IsLeftFlanking(text, i, 2, end) && close >= 0)
                    {
                        ParseRange(text, i + 2, close, MarkdownStyles.Overlay(style, styles.Strikethrough));
                        i = close + 2;
                        continue;
                    }

                    Emit(text.Substring(i, run), style);
                    i += run;
                    continue;
                }

                if (c == '*' || c == '_')
                {
                    int run = RunLength(text, i, end, c);
                    int length = Math.Min(run, 3);
                    if (CanOpen(text, i, length, end, c))
                    {
                        int close = FindCloser(text, i + length, end, c, length);
                        if (close >= 0)
                        {
                            CellStyle inner = style;
                            if (length != 2)
                                inner = MarkdownStyles.Overlay(inner, styles.Emphasis);
                            if (length >= 2)
                                inner = MarkdownStyles.Overlay(inner, styles.Strong);

                            // The closer is the last 'length' characters of the closing run, so a run like "***"
                            // after "**bold *italic" closes the inner emphasis first.
                            ParseRange(text, i + length, close, inner);
                            i = close + length;
                            continue;
                        }
                    }

                    Emit(c.ToString(), style);
                    i++;
                    continue;
                }

                Emit(c.ToString(), style);
                i++;
            }
        }

        // Finds the index where a closing delimiter of exactly 'length' characters starts, skipping escapes and
        // code spans. A closing run longer than 'length' closes with its last characters; a single-character
        // delimiter is never closed by a run of exactly two, which belongs to a nested strong span.
        private static int FindCloser(string text, int from, int end, char delimiter, int length)
        {
            int i = from;
            while (i < end)
            {
                char c = text[i];
                if (c == '\\')
                {
                    i += 2;
                    continue;
                }

                if (c == '`')
                {
                    int run = RunLength(text, i, end, '`');
                    int close = FindBacktickRun(text, i + run, end, run);
                    i = close >= 0 ? close + run : i + run;
                    continue;
                }

                if (c != delimiter)
                {
                    i++;
                    continue;
                }

                int runLength = RunLength(text, i, end, delimiter);
                bool rightFlanking = i > from && !char.IsWhiteSpace(text[i - 1]);
                bool lengthMatches = runLength == length || (runLength > length && !(length == 1 && runLength == 2));
                if (delimiter == '~')
                    lengthMatches = runLength == 2;

                if (rightFlanking && lengthMatches)
                {
                    int closeStart = i + runLength - length;
                    int after = i + runLength;
                    if (delimiter != '_' || after >= end || !char.IsLetterOrDigit(text[after]))
                        return closeStart;
                }

                i += runLength;
            }

            return -1;
        }

        private static bool CanOpen(string text, int index, int length, int end, char delimiter)
        {
            if (!IsLeftFlanking(text, index, length, end))
                return false;

            // An underscore inside a word (snake_case) is literal.
            if (delimiter == '_' && index > 0 && char.IsLetterOrDigit(text[index - 1]))
                return false;

            return true;
        }

        private static bool IsLeftFlanking(string text, int index, int length, int end)
        {
            int after = index + length;
            return after < end && !char.IsWhiteSpace(text[after]);
        }

        // Matches [label](destination "title") starting at the '['. Brackets in the label nest.
        private static bool TryLink(string text, int open, int end, out int labelEnd, out int afterLink, out string url)
        {
            labelEnd = -1;
            afterLink = -1;
            url = string.Empty;

            int depth = 0;
            int i = open;
            while (i < end)
            {
                char c = text[i];
                if (c == '\\')
                {
                    i += 2;
                    continue;
                }

                if (c == '`')
                {
                    int run = RunLength(text, i, end, '`');
                    int close = FindBacktickRun(text, i + run, end, run);
                    i = close >= 0 ? close + run : i + run;
                    continue;
                }

                if (c == '[')
                    depth++;
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        labelEnd = i;
                        break;
                    }
                }

                i++;
            }

            if (labelEnd < 0 || labelEnd + 1 >= end || text[labelEnd + 1] != '(')
                return false;

            int parens = 0;
            int j = labelEnd + 1;
            int destinationEnd = -1;
            while (j < end)
            {
                char c = text[j];
                if (c == '\\')
                {
                    j += 2;
                    continue;
                }

                if (c == '(')
                    parens++;
                else if (c == ')')
                {
                    parens--;
                    if (parens == 0)
                    {
                        destinationEnd = j;
                        break;
                    }
                }

                j++;
            }

            if (destinationEnd < 0)
                return false;

            string inside = text.Substring(labelEnd + 2, destinationEnd - labelEnd - 2).Trim();
            int space = inside.IndexOf(' ');
            string destination = space >= 0 ? inside.Substring(0, space) : inside;
            if (destination.StartsWith("<", StringComparison.Ordinal) && destination.EndsWith(">", StringComparison.Ordinal) && destination.Length >= 2)
                destination = destination.Substring(1, destination.Length - 2);

            url = destination;
            afterLink = destinationEnd + 1;
            return true;
        }

        // Returns the index of the closing '>' of an autolink such as <https://example.com>, or -1.
        private static int TryAutolink(string text, int open, int end)
        {
            int close = text.IndexOf('>', open + 1);
            if (close < 0 || close >= end)
                return -1;

            string body = text.Substring(open + 1, close - open - 1);
            if (body.Length == 0 || body.IndexOf(' ') >= 0 || body.IndexOf('<') >= 0)
                return -1;

            int colon = body.IndexOf(':');
            if (colon >= 2 && colon <= 32)
            {
                for (int k = 0; k < colon; k++)
                {
                    char ch = body[k];
                    bool valid = (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (k > 0 && ((ch >= '0' && ch <= '9') || ch == '+' || ch == '.' || ch == '-'));
                    if (!valid)
                        return -1;
                }

                return close;
            }

            int at = body.IndexOf('@');
            if (at > 0 && body.IndexOf('.', at) > at + 1)
                return close;

            return -1;
        }

        private static int RunLength(string text, int index, int end, char c)
        {
            int run = 0;
            while (index + run < end && text[index + run] == c)
                run++;

            return run;
        }

        private static int FindBacktickRun(string text, int from, int end, int length)
        {
            int i = from;
            while (i < end)
            {
                if (text[i] != '`')
                {
                    i++;
                    continue;
                }

                int run = RunLength(text, i, end, '`');
                if (run == length)
                    return i;

                i += run;
            }

            return -1;
        }

        private void Emit(string value, CellStyle style)
        {
            if (value.Length == 0)
                return;

            if (_HasRun && style != _RunStyle)
                Flush();

            if (!_HasRun)
            {
                _RunStyle = style;
                _HasRun = true;
            }

            _Run.Append(value);
        }

        private void Flush()
        {
            if (_HasRun && _Run.Length > 0)
                _Spans.Add(new StyledSpan(_Run.ToString(), _RunStyle));

            _Run.Clear();
            _HasRun = false;
        }
    }
}
