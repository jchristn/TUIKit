namespace TUIKit.Widgets
{
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Unicode;

    internal static class PromptTextLayout
    {
        internal static IReadOnlyList<string> Wrap(string text, int width)
        {
            List<string> rows = new List<string>();
            if (width <= 0)
                return rows;
            foreach (string line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                if (width == 1)
                {
                    IReadOnlyList<Grapheme> clusters = Graphemes.Split(line);
                    if (clusters.Count == 0)
                        rows.Add(string.Empty);
                    foreach (Grapheme cluster in clusters)
                        rows.Add(cluster.Width <= width ? cluster.Text : string.Empty);
                }
                else
                {
                    foreach (StyledText row in TextWrapper.Wrap(StyledText.From(line), width))
                        rows.Add(TextFit.Truncate(row.ToString(), width));
                }
            }
            return rows;
        }
    }
}
