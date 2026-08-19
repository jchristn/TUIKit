namespace TUIKit.Ascii
{
    /// <summary>
    /// The base class for the library's built-in FIGlet fonts, each of which ships as an embedded
    /// <c>.flf</c> resource. A concrete font supplies only its resource name and registered name; this
    /// base loads and parses the resource once at construction. Instances are immutable and
    /// thread-safe.
    /// </summary>
    public abstract class EmbeddedFigletFont : AsciiFontBase
    {
        private EmbeddedFigletFont(FigletFontData data)
            : base(data.Metrics, data.Glyphs)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EmbeddedFigletFont"/> class from an embedded
        /// resource.
        /// </summary>
        /// <param name="resourceName">The fully-qualified manifest resource name of the <c>.flf</c> file.</param>
        /// <param name="registeredName">The registered name to expose as <see cref="AsciiFontBase.Name"/>.</param>
        /// <exception cref="AsciiFontException">
        /// Thrown when the resource is missing or is not a valid FIGlet font.
        /// </exception>
        protected EmbeddedFigletFont(string resourceName, string registeredName)
            : this(FigletFontLoader.LoadEmbedded(resourceName, registeredName))
        {
        }
    }
}
