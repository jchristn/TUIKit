namespace TUIKit.Ascii
{
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// The contract every ASCII-art font implements, whether built-in or supplied by a consumer. Kept
    /// deliberately small — a name, layout metrics, and per-character glyph lookup — so third-party
    /// fonts are first-class and the composition engine in <see cref="AsciiArt"/> works against any
    /// implementation. Implementations are expected to be immutable and therefore thread-safe.
    /// </summary>
    public interface IAsciiFont
    {
        /// <summary>
        /// Gets the registered name of the font, the stable key used with
        /// <see cref="AsciiFontLibrary"/>. Never null.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the font's layout metrics (height, hardblank, default packing mode, smush rules).
        /// Never null.
        /// </summary>
        AsciiFontMetrics Metrics { get; }

        /// <summary>
        /// Attempts to get the glyph for a character.
        /// </summary>
        /// <param name="c">The character to look up.</param>
        /// <param name="glyph">
        /// When this method returns <c>true</c>, the glyph for <paramref name="c"/>; otherwise null.
        /// </param>
        /// <returns><c>true</c> when the font defines the character; otherwise <c>false</c>.</returns>
        bool TryGetGlyph(char c, out AsciiGlyph glyph);

        /// <summary>
        /// Gets the characters the font defines. Never null.
        /// </summary>
        IReadOnlyCollection<char> SupportedCharacters { get; }

        /// <summary>
        /// Asynchronously enumerates the characters the font defines. Provided as the async companion
        /// to <see cref="SupportedCharacters"/>; enumeration is in-memory and completes promptly.
        /// </summary>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>An async sequence of the supported characters.</returns>
        IAsyncEnumerable<char> GetSupportedCharactersAsync(CancellationToken token);
    }
}
