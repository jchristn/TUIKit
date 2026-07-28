namespace TUIKit
{
    using System;

    /// <summary>
    /// An immutable run of text sharing a single <see cref="CellStyle"/>. Spans are the building
    /// blocks of <see cref="StyledText"/>. Instances are value types and are safe to share across
    /// threads.
    /// </summary>
    public readonly struct StyledSpan : IEquatable<StyledSpan>
    {
        /// <summary>
        /// Gets the text of the run. Never null.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the style applied to the run.
        /// </summary>
        public CellStyle Style { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StyledSpan"/> struct.
        /// </summary>
        /// <param name="text">The run text. Must not be null.</param>
        /// <param name="style">The style applied to the run.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public StyledSpan(string text, CellStyle style)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            Text = text;
            Style = style;
        }

        /// <summary>
        /// Returns a copy of this span with a different style.
        /// </summary>
        /// <param name="style">The new style.</param>
        /// <returns>The derived span.</returns>
        public StyledSpan WithStyle(CellStyle style)
        {
            return new StyledSpan(Text, style);
        }

        /// <inheritdoc/>
        public bool Equals(StyledSpan other)
        {
            return string.Equals(Text, other.Text, StringComparison.Ordinal) && Style.Equals(other.Style);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is StyledSpan other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Text != null ? StringComparer.Ordinal.GetHashCode(Text) : 0;
                return (hash * 31) ^ Style.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Text;
        }
    }
}
