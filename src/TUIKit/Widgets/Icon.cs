namespace TUIKit.Widgets
{
    using System;

    /// <summary>
    /// A single named icon carrying three renderings — a Nerd Font glyph, a portable Unicode symbol,
    /// and a plain ASCII fallback — so callers can degrade gracefully by <see cref="IconMode"/>.
    /// </summary>
    public readonly struct Icon
    {
        /// <summary>
        /// Gets the Nerd Font (private-use) glyph.
        /// </summary>
        public string Nerd { get; }

        /// <summary>
        /// Gets the portable Unicode symbol.
        /// </summary>
        public string Unicode { get; }

        /// <summary>
        /// Gets the plain ASCII fallback.
        /// </summary>
        public string Ascii { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Icon"/> struct.
        /// </summary>
        /// <param name="nerd">The Nerd Font glyph. Must not be null.</param>
        /// <param name="unicode">The Unicode symbol. Must not be null.</param>
        /// <param name="ascii">The ASCII fallback. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public Icon(string nerd, string unicode, string ascii)
        {
            Nerd = nerd ?? throw new ArgumentNullException(nameof(nerd));
            Unicode = unicode ?? throw new ArgumentNullException(nameof(unicode));
            Ascii = ascii ?? throw new ArgumentNullException(nameof(ascii));
        }

        /// <summary>
        /// Resolves the glyph for the given mode.
        /// </summary>
        /// <param name="mode">The icon mode.</param>
        /// <returns>The glyph string for the mode. Never null.</returns>
        public string For(IconMode mode)
        {
            switch (mode)
            {
                case IconMode.Nerd:
                    return Nerd;
                case IconMode.Ascii:
                    return Ascii;
                default:
                    return Unicode;
            }
        }

        /// <summary>
        /// Returns the glyph for the current global <see cref="Icons.Mode"/>.
        /// </summary>
        /// <returns>The resolved glyph.</returns>
        public override string ToString()
        {
            return For(Icons.Mode);
        }
    }
}
