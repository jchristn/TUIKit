namespace TUIKit.Ascii
{
    /// <summary>
    /// A concrete <see cref="AsciiFontBase"/> produced by <see cref="FigletFontLoader"/> for fonts
    /// loaded from a stream or string at runtime. Built-in embedded fonts derive from
    /// <see cref="EmbeddedFigletFont"/> instead.
    /// </summary>
    internal sealed class FigletFont : AsciiFontBase
    {
        internal FigletFont(FigletFontData data)
            : base(data.Metrics, data.Glyphs)
        {
        }
    }
}
