namespace TUIKit.Input
{
    using System;

    /// <summary>
    /// A URL found by <see cref="LinkScanner"/>: its position in the source string and its target.
    /// Instances are value types.
    /// </summary>
    public readonly struct LinkMatch : IEquatable<LinkMatch>
    {
        /// <summary>
        /// Gets the zero-based character index where the URL begins.
        /// </summary>
        public int Start { get; }

        /// <summary>
        /// Gets the length of the URL in characters.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the matched URL text. Never null.
        /// </summary>
        public string Uri { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkMatch"/> struct.
        /// </summary>
        /// <param name="start">The zero-based start index.</param>
        /// <param name="length">The length in characters.</param>
        /// <param name="uri">The matched URL. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is null.</exception>
        public LinkMatch(int start, int length, string uri)
        {
            Start = start;
            Length = length;
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        }

        /// <inheritdoc/>
        public bool Equals(LinkMatch other)
        {
            return Start == other.Start && Length == other.Length && string.Equals(Uri, other.Uri, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is LinkMatch other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Start;
                hash = (hash * 31) ^ Length;
                hash = (hash * 31) ^ (Uri != null ? StringComparer.Ordinal.GetHashCode(Uri) : 0);
                return hash;
            }
        }
    }
}
