namespace TUIKit.Ascii
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Renders text into multi-row ASCII art using an <see cref="IAsciiFont"/>. The composition engine
    /// (horizontal packing, kerning, and the six FIGlet horizontal smushing rules) lives here and
    /// works against the <see cref="IAsciiFont"/> surface, so it applies uniformly to built-in and
    /// third-party fonts alike. The returned rows are plain strings with the font's hardblank already
    /// substituted for spaces, ready to color and place. All members are thread-safe.
    /// </summary>
    public static class AsciiArt
    {
        private const string _UnderscoreTargets = "|/\\[]{}()<>";

        /// <summary>
        /// Renders text into ASCII art rows.
        /// </summary>
        /// <param name="text">The text to render. Must not be null. Embedded newlines start a new block of rows.</param>
        /// <param name="font">The font to render with. Must not be null.</param>
        /// <param name="options">Rendering options, or null for defaults (font layout, left aligned, trimmed).</param>
        /// <returns>The composed rows, top to bottom. Never null; every row shares the same length.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="font"/> is null.</exception>
        public static IReadOnlyList<string> Render(string text, IAsciiFont font, AsciiArtOptions? options = null)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (font == null)
                throw new ArgumentNullException(nameof(font));

            AsciiFontMetrics metrics = font.Metrics;
            AsciiLayoutMode layout = options?.Layout ?? metrics.DefaultLayout;
            int height = metrics.Height;

            string[] segments = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            List<string> rows = new List<string>(segments.Length * height);

            for (int s = 0; s < segments.Length; s++)
            {
                string[] block = ComposeLine(font, segments[s], layout, metrics);
                for (int r = 0; r < block.Length; r++)
                    rows.Add(block[r].Replace(metrics.HardBlank, ' '));
            }

            bool trim = options?.TrimBlankColumns ?? true;
            int maxWidth = options?.MaxWidth ?? 0;
            AsciiArtAlignment alignment = options?.Alignment ?? AsciiArtAlignment.Left;

            PadToUniformWidth(rows);
            if (trim)
                TrimBlankColumns(rows);
            AlignToWidth(rows, maxWidth, alignment);

            return rows;
        }

        /// <summary>
        /// Asynchronously renders text into ASCII art rows. The async companion to
        /// <see cref="Render(string, IAsciiFont, AsciiArtOptions)"/>; the work is in-memory and
        /// completes promptly.
        /// </summary>
        /// <param name="text">The text to render. Must not be null.</param>
        /// <param name="font">The font to render with. Must not be null.</param>
        /// <param name="options">Rendering options, or null for defaults.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>A task producing the composed rows.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> or <paramref name="font"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is requested.</exception>
        public static Task<IReadOnlyList<string>> RenderAsync(
            string text,
            IAsciiFont font,
            AsciiArtOptions? options = null,
            CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Render(text, font, options));
        }

        private static string[] ComposeLine(IAsciiFont font, string text, AsciiLayoutMode layout, AsciiFontMetrics metrics)
        {
            int height = metrics.Height;
            string[] output = new string[height];
            for (int r = 0; r < height; r++)
                output[r] = string.Empty;

            bool first = true;
            for (int i = 0; i < text.Length; i++)
            {
                AsciiGlyph? glyph = Resolve(font, text[i]);
                if (glyph == null || glyph.Width == 0)
                    continue;

                if (first)
                {
                    for (int r = 0; r < height; r++)
                        output[r] = glyph.Row(r);

                    first = false;
                    continue;
                }

                int amount = SmushAmount(output, glyph, layout, metrics);
                for (int r = 0; r < height; r++)
                {
                    string line = output[r];
                    string grow = glyph.Row(r);
                    int keep = line.Length - amount;
                    if (keep < 0)
                        keep = 0;

                    StringBuilder builder = new StringBuilder(keep + grow.Length);
                    builder.Append(line, 0, keep);

                    for (int c = 0; c < amount; c++)
                    {
                        char left = keep + c < line.Length ? line[keep + c] : ' ';
                        char right = c < grow.Length ? grow[c] : ' ';
                        char? merged = SmushPair(left, right, layout, metrics);
                        builder.Append(merged ?? right);
                    }

                    if (amount < grow.Length)
                        builder.Append(grow, amount, grow.Length - amount);

                    output[r] = builder.ToString();
                }
            }

            return output;
        }

        private static AsciiGlyph? Resolve(IAsciiFont font, char c)
        {
            if (font.TryGetGlyph(c, out AsciiGlyph glyph))
                return glyph;
            if (c != ' ' && font.TryGetGlyph(' ', out AsciiGlyph space))
                return space;

            return null;
        }

        private static int SmushAmount(string[] output, AsciiGlyph glyph, AsciiLayoutMode layout, AsciiFontMetrics metrics)
        {
            if (layout == AsciiLayoutMode.FullWidth)
                return 0;

            int lineLength = output[0].Length;
            int maxSmush = glyph.Width;

            for (int r = 0; r < output.Length; r++)
            {
                string line = output[r];
                string grow = glyph.Row(r);

                int lineTrailing = TrailingSpaces(line);
                int glyphLeading = LeadingSpaces(grow);
                int amount = lineTrailing + glyphLeading;

                int leftIndex = line.Length - lineTrailing - 1;
                int rightIndex = glyphLeading;
                if (leftIndex >= 0 && rightIndex < grow.Length)
                {
                    char left = line[leftIndex];
                    char right = grow[rightIndex];
                    if (left != ' ' && right != ' ' && SmushPair(left, right, layout, metrics) != null)
                        amount += 1;
                }

                if (amount < maxSmush)
                    maxSmush = amount;
            }

            if (maxSmush > lineLength)
                maxSmush = lineLength;
            if (maxSmush < 0)
                maxSmush = 0;

            return maxSmush;
        }

        private static char? SmushPair(char left, char right, AsciiLayoutMode layout, AsciiFontMetrics metrics)
        {
            if (left == ' ')
                return right;
            if (right == ' ')
                return left;

            // Kerning and full width never merge two ink columns.
            if (layout != AsciiLayoutMode.Smushing)
                return null;

            char hardBlank = metrics.HardBlank;
            AsciiSmushRule rules = metrics.SmushRules;

            if (rules == AsciiSmushRule.None)
            {
                // Universal smushing: the later (right) character wins unless it is a hardblank.
                if (left == hardBlank)
                    return right;
                if (right == hardBlank)
                    return left;

                return right;
            }

            if ((rules & AsciiSmushRule.HardBlank) != 0 && left == hardBlank && right == hardBlank)
                return hardBlank;
            if (left == hardBlank || right == hardBlank)
                return null;

            if ((rules & AsciiSmushRule.EqualCharacter) != 0 && left == right)
                return left;

            if ((rules & AsciiSmushRule.Underscore) != 0)
            {
                if (left == '_' && _UnderscoreTargets.IndexOf(right) >= 0)
                    return right;
                if (right == '_' && _UnderscoreTargets.IndexOf(left) >= 0)
                    return left;
            }

            if ((rules & AsciiSmushRule.Hierarchy) != 0)
            {
                char? hierarchy = SmushHierarchy(left, right);
                if (hierarchy != null)
                    return hierarchy;
            }

            if ((rules & AsciiSmushRule.OppositePair) != 0)
            {
                if ((left == '[' && right == ']') || (left == ']' && right == '['))
                    return '|';
                if ((left == '{' && right == '}') || (left == '}' && right == '{'))
                    return '|';
                if ((left == '(' && right == ')') || (left == ')' && right == '('))
                    return '|';
            }

            if ((rules & AsciiSmushRule.BigX) != 0)
            {
                if (left == '/' && right == '\\')
                    return '|';
                if (left == '\\' && right == '/')
                    return 'Y';
                if (left == '>' && right == '<')
                    return 'X';
            }

            return null;
        }

        private static char? SmushHierarchy(char left, char right)
        {
            int leftClass = HierarchyClass(left);
            int rightClass = HierarchyClass(right);
            if (leftClass == 0 || rightClass == 0 || leftClass == rightClass)
                return null;

            return leftClass > rightClass ? left : right;
        }

        private static int HierarchyClass(char c)
        {
            switch (c)
            {
                case '|':
                    return 1;
                case '/':
                case '\\':
                    return 2;
                case '[':
                case ']':
                    return 3;
                case '{':
                case '}':
                    return 4;
                case '(':
                case ')':
                    return 5;
                case '<':
                case '>':
                    return 6;
                default:
                    return 0;
            }
        }

        private static int LeadingSpaces(string value)
        {
            int count = 0;
            while (count < value.Length && value[count] == ' ')
                count++;

            return count;
        }

        private static int TrailingSpaces(string value)
        {
            int count = 0;
            while (count < value.Length && value[value.Length - 1 - count] == ' ')
                count++;

            return count;
        }

        private static void PadToUniformWidth(List<string> rows)
        {
            int width = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Length > width)
                    width = rows[i].Length;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Length < width)
                    rows[i] = rows[i] + new string(' ', width - rows[i].Length);
            }
        }

        private static void TrimBlankColumns(List<string> rows)
        {
            if (rows.Count == 0)
                return;

            int width = rows[0].Length;
            if (width == 0)
                return;

            int left = 0;
            while (left < width && ColumnIsBlank(rows, left))
                left++;

            if (left == width)
            {
                // Every column is blank; collapse to zero width, preserving row count.
                for (int i = 0; i < rows.Count; i++)
                    rows[i] = string.Empty;
                return;
            }

            int right = width - 1;
            while (right > left && ColumnIsBlank(rows, right))
                right--;

            if (left == 0 && right == width - 1)
                return;

            for (int i = 0; i < rows.Count; i++)
                rows[i] = rows[i].Substring(left, right - left + 1);
        }

        private static bool ColumnIsBlank(List<string> rows, int column)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (column < rows[i].Length && rows[i][column] != ' ')
                    return false;
            }

            return true;
        }

        private static void AlignToWidth(List<string> rows, int maxWidth, AsciiArtAlignment alignment)
        {
            if (maxWidth <= 0 || rows.Count == 0)
                return;

            int width = rows[0].Length;
            if (width >= maxWidth)
                return;

            int total = maxWidth - width;
            int leftPad;
            switch (alignment)
            {
                case AsciiArtAlignment.Right:
                    leftPad = total;
                    break;
                case AsciiArtAlignment.Center:
                    leftPad = total / 2;
                    break;
                default:
                    leftPad = 0;
                    break;
            }

            int rightPad = total - leftPad;
            string leftString = leftPad > 0 ? new string(' ', leftPad) : string.Empty;
            string rightString = rightPad > 0 ? new string(' ', rightPad) : string.Empty;
            for (int i = 0; i < rows.Count; i++)
                rows[i] = leftString + rows[i] + rightString;
        }
    }
}
