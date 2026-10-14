namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit;

    // Search highlighting shared by Pane and MarkdownView: the default match style and the span rewrite that
    // paints every case-insensitive match of a term in a row. Stateless and thread-safe.
    internal static class SearchHighlighter
    {
        internal static CellStyle DefaultStyle
        {
            get { return CellStyle.Default.WithForeground(Color.FromRgb(0, 0, 0)).WithBackground(Color.FromPalette(3)); }
        }

        internal static bool Matches(StyledText row, string query)
        {
            return row.ToPlainString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static StyledText Highlight(StyledText row, string query, CellStyle highlight)
        {
            List<char> chars = new List<char>();
            List<CellStyle> styles = new List<CellStyle>();
            IReadOnlyList<StyledSpan> spans = row.Spans;
            for (int s = 0; s < spans.Count; s++)
            {
                string text = spans[s].Text;
                for (int c = 0; c < text.Length; c++)
                {
                    chars.Add(text[c]);
                    styles.Add(spans[s].Style);
                }
            }

            string plain = new string(chars.ToArray());
            int from = 0;
            bool any = false;
            while (from <= plain.Length - query.Length)
            {
                int index = plain.IndexOf(query, from, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    break;

                any = true;
                for (int i = index; i < index + query.Length && i < styles.Count; i++)
                    styles[i] = highlight;

                from = index + query.Length;
            }

            if (!any)
                return row;

            List<StyledSpan> result = new List<StyledSpan>();
            StringBuilder run = new StringBuilder();
            CellStyle current = CellStyle.Default;
            bool has = false;
            for (int i = 0; i < chars.Count; i++)
            {
                if (!has)
                {
                    current = styles[i];
                    has = true;
                }
                else if (styles[i] != current)
                {
                    result.Add(new StyledSpan(run.ToString(), current));
                    run.Clear();
                    current = styles[i];
                }

                run.Append(chars[i]);
            }

            if (has)
                result.Add(new StyledSpan(run.ToString(), current));

            return new StyledText(result);
        }
    }
}
