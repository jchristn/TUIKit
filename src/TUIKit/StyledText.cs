namespace TUIKit
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using TUIKit.Unicode;

    /// <summary>
    /// An immutable sequence of <see cref="StyledSpan"/> runs forming a single logical piece of
    /// styled text. Fluent methods return a new instance with a style transform applied to every
    /// span, so <c>Text.From("hi").Bold().Red()</c> yields bold red text. Instances are immutable
    /// and safe to share across threads.
    /// </summary>
    public sealed class StyledText
    {
        private readonly StyledSpan[] _Spans;

        /// <summary>
        /// Gets the spans that make up this text, in order. Never null.
        /// </summary>
        public IReadOnlyList<StyledSpan> Spans
        {
            get { return _Spans; }
        }

        /// <summary>
        /// Gets the total terminal column width of the text, honoring grapheme clustering and wide
        /// glyphs.
        /// </summary>
        public int Width
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _Spans.Length; i++)
                    total += Graphemes.MeasureWidth(_Spans[i].Text);

                return total;
            }
        }

        /// <summary>
        /// Gets an empty styled text with no spans.
        /// </summary>
        public static StyledText Empty
        {
            get { return new StyledText(Array.Empty<StyledSpan>()); }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StyledText"/> class from a set of spans.
        /// </summary>
        /// <param name="spans">The spans, in order. Must not be null and must not contain nulls.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="spans"/> is null.</exception>
        public StyledText(IReadOnlyList<StyledSpan> spans)
        {
            if (spans == null)
                throw new ArgumentNullException(nameof(spans));

            StyledSpan[] copy = new StyledSpan[spans.Count];
            for (int i = 0; i < spans.Count; i++)
                copy[i] = spans[i];

            _Spans = copy;
        }

        private StyledText(StyledSpan[] spans)
        {
            _Spans = spans;
        }

        /// <summary>
        /// Creates styled text from a string using the default style.
        /// </summary>
        /// <param name="value">The text. Must not be null.</param>
        /// <returns>The styled text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
        public static StyledText From(string value)
        {
            return From(value, CellStyle.Default);
        }

        /// <summary>
        /// Creates styled text from a string using the supplied style.
        /// </summary>
        /// <param name="value">The text. Must not be null.</param>
        /// <param name="style">The style to apply.</param>
        /// <returns>The styled text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
        public static StyledText From(string value, CellStyle style)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return new StyledText(new[] { new StyledSpan(value, style) });
        }

        /// <summary>
        /// Returns a copy of this text with the supplied text appended in the default style.
        /// </summary>
        /// <param name="value">The text to append. Must not be null.</param>
        /// <returns>The combined text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
        public StyledText Append(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            return Append(From(value));
        }

        /// <summary>
        /// Returns a copy of this text with another styled text appended.
        /// </summary>
        /// <param name="other">The text to append. Must not be null.</param>
        /// <returns>The combined text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
        public StyledText Append(StyledText other)
        {
            if (other == null)
                throw new ArgumentNullException(nameof(other));

            StyledSpan[] combined = new StyledSpan[_Spans.Length + other._Spans.Length];
            Array.Copy(_Spans, 0, combined, 0, _Spans.Length);
            Array.Copy(other._Spans, 0, combined, _Spans.Length, other._Spans.Length);
            return new StyledText(combined);
        }

        /// <summary>
        /// Returns a copy of this text with every span's foreground set to the supplied color.
        /// </summary>
        /// <param name="color">The foreground color.</param>
        /// <returns>The restyled text.</returns>
        public StyledText Foreground(Color color)
        {
            return Transform(style => style.WithForeground(color));
        }

        /// <summary>
        /// Returns a copy of this text with every span's background set to the supplied color.
        /// </summary>
        /// <param name="color">The background color.</param>
        /// <returns>The restyled text.</returns>
        public StyledText Background(Color color)
        {
            return Transform(style => style.WithBackground(color));
        }

        /// <summary>
        /// Returns a copy of this text with the bold attribute applied to every span.
        /// </summary>
        /// <returns>The restyled text.</returns>
        public StyledText Bold()
        {
            return Transform(style => style.WithAttribute(CellAttributes.Bold, true));
        }

        /// <summary>
        /// Returns a copy of this text with the dim attribute applied to every span.
        /// </summary>
        /// <returns>The restyled text.</returns>
        public StyledText Dim()
        {
            return Transform(style => style.WithAttribute(CellAttributes.Dim, true));
        }

        /// <summary>
        /// Returns a copy of this text with the italic attribute applied to every span.
        /// </summary>
        /// <returns>The restyled text.</returns>
        public StyledText Italic()
        {
            return Transform(style => style.WithAttribute(CellAttributes.Italic, true));
        }

        /// <summary>
        /// Returns a copy of this text with the underline attribute applied to every span.
        /// </summary>
        /// <returns>The restyled text.</returns>
        public StyledText Underline()
        {
            return Transform(style => style.WithAttribute(CellAttributes.Underline, true));
        }

        /// <summary>
        /// Returns a copy of this text with the strikethrough attribute applied to every span.
        /// </summary>
        /// <returns>The restyled text.</returns>
        public StyledText Strikethrough()
        {
            return Transform(style => style.WithAttribute(CellAttributes.Strikethrough, true));
        }

        /// <summary>
        /// Returns a copy of this text with the reverse-video attribute applied to every span.
        /// </summary>
        /// <returns>The restyled text.</returns>
        public StyledText Reverse()
        {
            return Transform(style => style.WithAttribute(CellAttributes.Reverse, true));
        }

        /// <summary>
        /// Returns a copy of this text with the foreground set to standard palette red (index 1).
        /// </summary>
        /// <returns>The restyled text.</returns>
        public StyledText Red()
        {
            return Foreground(Color.FromPalette(1));
        }

        /// <summary>
        /// Returns a copy of this text with the foreground set to standard palette green (index 2).
        /// </summary>
        /// <returns>The restyled text.</returns>
        public StyledText Green()
        {
            return Foreground(Color.FromPalette(2));
        }

        /// <summary>
        /// Returns a copy of this text with the foreground set to standard palette yellow (index 3).
        /// </summary>
        /// <returns>The restyled text.</returns>
        public StyledText Yellow()
        {
            return Foreground(Color.FromPalette(3));
        }

        /// <summary>
        /// Returns a copy of this text with the foreground set to standard palette blue (index 4).
        /// </summary>
        /// <returns>The restyled text.</returns>
        public StyledText Blue()
        {
            return Foreground(Color.FromPalette(4));
        }

        /// <summary>
        /// Returns a copy of this text with the foreground set to standard palette magenta (index 5).
        /// </summary>
        /// <returns>The restyled text.</returns>
        public StyledText Magenta()
        {
            return Foreground(Color.FromPalette(5));
        }

        /// <summary>
        /// Returns a copy of this text with the foreground set to standard palette cyan (index 6).
        /// </summary>
        /// <returns>The restyled text.</returns>
        public StyledText Cyan()
        {
            return Foreground(Color.FromPalette(6));
        }

        /// <summary>
        /// Returns the plain, unstyled concatenation of every span's text.
        /// </summary>
        /// <returns>The plain text. Never null.</returns>
        public string ToPlainString()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < _Spans.Length; i++)
                builder.Append(_Spans[i].Text);

            return builder.ToString();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return ToPlainString();
        }

        private StyledText Transform(Func<CellStyle, CellStyle> transform)
        {
            StyledSpan[] next = new StyledSpan[_Spans.Length];
            for (int i = 0; i < _Spans.Length; i++)
                next[i] = _Spans[i].WithStyle(transform(_Spans[i].Style));

            return new StyledText(next);
        }
    }
}
