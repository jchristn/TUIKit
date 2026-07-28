namespace TUIKit.Unicode
{
    using System;

    /// <summary>
    /// A single user-perceived character (an extended grapheme cluster) together with the number
    /// of terminal columns it occupies. Instances are value types and are safe to share across
    /// threads.
    /// </summary>
    public readonly struct Grapheme : IEquatable<Grapheme>
    {
        /// <summary>
        /// Gets the text of the cluster. May contain multiple UTF-16 code units (for example an
        /// emoji ZWJ sequence). Never null.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the number of terminal columns the cluster occupies: 0 for a lone combining mark,
        /// 1 for a narrow character, or 2 for a wide or emoji character.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Grapheme"/> struct.
        /// </summary>
        /// <param name="text">The cluster text. Must not be null.</param>
        /// <param name="width">The column width (0, 1, or 2).</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public Grapheme(string text, int width)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            Text = text;
            Width = width;
        }

        /// <inheritdoc/>
        public bool Equals(Grapheme other)
        {
            return Width == other.Width && string.Equals(Text, other.Text, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is Grapheme other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Text != null ? StringComparer.Ordinal.GetHashCode(Text) : 0;
                return (hash * 31) ^ Width;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Text;
        }
    }
}
