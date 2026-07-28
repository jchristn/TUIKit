namespace TUIKit
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Unicode;

    /// <summary>
    /// Convenience drawing helpers layered over the minimal <see cref="ISurface"/> contract. These
    /// are extension methods rather than interface members so the surface abstraction stays small
    /// and additive changes do not break implementers on frameworks without default interface
    /// methods.
    /// </summary>
    public static class SurfaceExtensions
    {
        /// <summary>
        /// Draws text starting at the supplied coordinate, honoring grapheme clustering and wide
        /// glyphs. A wide glyph that would overflow the right edge is replaced by a blank space.
        /// </summary>
        /// <param name="surface">The target surface. Must not be null.</param>
        /// <param name="x">The starting zero-based column.</param>
        /// <param name="y">The zero-based row.</param>
        /// <param name="text">The text to draw. Must not be null.</param>
        /// <param name="style">The style to apply to every cell.</param>
        /// <returns>The number of columns advanced.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="surface"/> or <paramref name="text"/> is null.
        /// </exception>
        public static int DrawText(this ISurface surface, int x, int y, string text, CellStyle style)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            IReadOnlyList<Grapheme> clusters = Graphemes.Split(text);
            int cursor = x;

            for (int i = 0; i < clusters.Count; i++)
            {
                Grapheme cluster = clusters[i];

                if (cluster.Width == 0)
                    continue;

                if (cluster.Width == 2)
                {
                    if (cursor + 1 >= surface.Size.Width)
                    {
                        surface.Set(cursor, y, Cell.Blank(style));
                        cursor += 1;
                        continue;
                    }

                    surface.Set(cursor, y, Cell.Glyph(cluster.Text, style, 2));
                    surface.Set(cursor + 1, y, Cell.Continuation(style));
                    cursor += 2;
                }
                else
                {
                    surface.Set(cursor, y, Cell.Glyph(cluster.Text, style, 1));
                    cursor += 1;
                }
            }

            return cursor - x;
        }

        /// <summary>
        /// Draws styled text starting at the supplied coordinate, painting each span with its own
        /// style and advancing across grapheme clusters and wide glyphs.
        /// </summary>
        /// <param name="surface">The target surface. Must not be null.</param>
        /// <param name="x">The starting zero-based column.</param>
        /// <param name="y">The zero-based row.</param>
        /// <param name="text">The styled text to draw. Must not be null.</param>
        /// <returns>The number of columns advanced.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="surface"/> or <paramref name="text"/> is null.
        /// </exception>
        public static int DrawStyledText(this ISurface surface, int x, int y, StyledText text)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            int cursor = x;
            IReadOnlyList<StyledSpan> spans = text.Spans;
            for (int i = 0; i < spans.Count; i++)
                cursor += surface.DrawText(cursor, y, spans[i].Text, spans[i].Style);

            return cursor - x;
        }

        /// <summary>
        /// Draws styled text, composing each span over a base style so spans that do not set their
        /// own foreground or background inherit it (for example a themed pane background).
        /// </summary>
        /// <param name="surface">The target surface. Must not be null.</param>
        /// <param name="x">The starting zero-based column.</param>
        /// <param name="y">The zero-based row.</param>
        /// <param name="text">The styled text to draw. Must not be null.</param>
        /// <param name="baseStyle">The base style to inherit default colors from.</param>
        /// <returns>The number of columns advanced.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="surface"/> or <paramref name="text"/> is null.
        /// </exception>
        public static int DrawStyledText(this ISurface surface, int x, int y, StyledText text, CellStyle baseStyle)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            int cursor = x;
            IReadOnlyList<StyledSpan> spans = text.Spans;
            for (int i = 0; i < spans.Count; i++)
                cursor += surface.DrawText(cursor, y, spans[i].Text, spans[i].Style.Over(baseStyle));

            return cursor - x;
        }

        /// <summary>
        /// Draws a single-line box (border) around the supplied rectangle using box-drawing glyphs,
        /// with an optional title centered on the top edge.
        /// </summary>
        /// <param name="surface">The target surface. Must not be null.</param>
        /// <param name="rect">The rectangle to outline, in local coordinates.</param>
        /// <param name="style">The style for the border and title.</param>
        /// <param name="title">An optional title drawn on the top edge, or null.</param>
        /// <param name="ascii">When true, uses ASCII glyphs instead of box-drawing glyphs.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
        public static void DrawBox(this ISurface surface, Rect rect, CellStyle style, string? title = null, bool ascii = false)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (rect.Width < 2 || rect.Height < 2)
                return;

            string horizontal = ascii ? "-" : "─";
            string vertical = ascii ? "|" : "│";
            string topLeft = ascii ? "+" : "┌";
            string topRight = ascii ? "+" : "┐";
            string bottomLeft = ascii ? "+" : "└";
            string bottomRight = ascii ? "+" : "┘";

            int left = rect.Left;
            int right = rect.Right - 1;
            int top = rect.Top;
            int bottom = rect.Bottom - 1;

            surface.Set(left, top, Cell.Glyph(topLeft, style, 1));
            surface.Set(right, top, Cell.Glyph(topRight, style, 1));
            surface.Set(left, bottom, Cell.Glyph(bottomLeft, style, 1));
            surface.Set(right, bottom, Cell.Glyph(bottomRight, style, 1));

            for (int x = left + 1; x < right; x++)
            {
                surface.Set(x, top, Cell.Glyph(horizontal, style, 1));
                surface.Set(x, bottom, Cell.Glyph(horizontal, style, 1));
            }

            for (int y = top + 1; y < bottom; y++)
            {
                surface.Set(left, y, Cell.Glyph(vertical, style, 1));
                surface.Set(right, y, Cell.Glyph(vertical, style, 1));
            }

            if (!string.IsNullOrEmpty(title))
            {
                string label = " " + title + " ";
                int available = right - left - 1;
                if (available > 1 && label.Length > available)
                    label = label.Substring(0, available);

                int titleWidth = TUIKit.Unicode.Graphemes.MeasureWidth(label);
                int start = left + 1 + Math.Max(0, ((right - left - 1) - titleWidth) / 2);
                surface.DrawText(start, top, label, style);
            }
        }

        /// <summary>
        /// Draws a single-line horizontal run of a repeated character.
        /// </summary>
        /// <param name="surface">The target surface. Must not be null.</param>
        /// <param name="x">The starting zero-based column.</param>
        /// <param name="y">The zero-based row.</param>
        /// <param name="length">The number of cells to fill. Values less than one are ignored.</param>
        /// <param name="glyph">The single-character glyph to repeat. Must not be null or empty.</param>
        /// <param name="style">The style to apply.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="glyph"/> is null or empty.</exception>
        public static void DrawHorizontalLine(this ISurface surface, int x, int y, int length, string glyph, CellStyle style)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (string.IsNullOrEmpty(glyph))
                throw new ArgumentException("Glyph must not be null or empty.", nameof(glyph));

            Cell cell = Cell.Glyph(glyph, style, 1);
            for (int i = 0; i < length; i++)
                surface.Set(x + i, y, cell);
        }
    }
}
