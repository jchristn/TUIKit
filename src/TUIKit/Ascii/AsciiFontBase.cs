namespace TUIKit.Ascii
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;

    /// <summary>
    /// The base class for ASCII-art fonts. It stores a font's metrics and its immutable glyph table
    /// and implements the <see cref="IAsciiFont"/> lookup surface, so a concrete font only supplies
    /// data. The horizontal composition, kerning, and smushing engine lives in <see cref="AsciiArt"/>
    /// and operates on the <see cref="IAsciiFont"/> surface, so it applies uniformly to every font.
    /// Instances are immutable and thread-safe.
    /// </summary>
    public abstract class AsciiFontBase : IAsciiFont
    {
        private readonly Dictionary<char, AsciiGlyph> _Glyphs;

        /// <inheritdoc/>
        public string Name
        {
            get { return Metrics.Name; }
        }

        /// <inheritdoc/>
        public AsciiFontMetrics Metrics { get; }

        /// <inheritdoc/>
        public IReadOnlyCollection<char> SupportedCharacters
        {
            get { return _Glyphs.Keys; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsciiFontBase"/> class.
        /// </summary>
        /// <param name="metrics">The font metrics. Must not be null.</param>
        /// <param name="glyphs">
        /// The character-to-glyph map. Must not be null and every glyph must match
        /// <see cref="AsciiFontMetrics.Height"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="AsciiFontException">Thrown when a glyph's height does not match the font height.</exception>
        protected AsciiFontBase(AsciiFontMetrics metrics, IReadOnlyDictionary<char, AsciiGlyph> glyphs)
        {
            if (metrics == null)
                throw new ArgumentNullException(nameof(metrics));
            if (glyphs == null)
                throw new ArgumentNullException(nameof(glyphs));

            Metrics = metrics;
            _Glyphs = new Dictionary<char, AsciiGlyph>(glyphs.Count);
            foreach (KeyValuePair<char, AsciiGlyph> pair in glyphs)
            {
                if (pair.Value == null)
                    throw new ArgumentNullException(nameof(glyphs), "Glyph map must not contain a null glyph.");
                if (pair.Value.Height != metrics.Height)
                    throw new AsciiFontException(
                        "Font '" + metrics.Name + "' glyph for '" + pair.Key + "' has height "
                        + pair.Value.Height + " but the font height is " + metrics.Height + ".");

                _Glyphs[pair.Key] = pair.Value;
            }
        }

        /// <inheritdoc/>
        public bool TryGetGlyph(char c, out AsciiGlyph glyph)
        {
            return _Glyphs.TryGetValue(c, out glyph!);
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<char> GetSupportedCharactersAsync([EnumeratorCancellation] CancellationToken token)
        {
            foreach (char c in _Glyphs.Keys)
            {
                token.ThrowIfCancellationRequested();
                yield return c;
            }

            await System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
