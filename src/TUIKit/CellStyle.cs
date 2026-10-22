namespace TUIKit
{
    using System;

    /// <summary>
    /// An immutable description of how a cell is painted: foreground and background colors, text
    /// attributes, an optional link identifier, and an optional absolute hyperlink target. Instances are value types and are safe to share
    /// across threads. Use the <c>With*</c> methods to derive a modified copy.
    /// </summary>
    public readonly struct CellStyle : IEquatable<CellStyle>
    {
        /// <summary>
        /// Gets the foreground color.
        /// </summary>
        public Color Foreground { get; }

        /// <summary>
        /// Gets the background color.
        /// </summary>
        public Color Background { get; }

        /// <summary>
        /// Gets the text attributes applied to the cell.
        /// </summary>
        public CellAttributes Attributes { get; }

        /// <summary>
        /// Gets the identifier of the link this cell belongs to, or zero when the cell is not part
        /// of a link. Link registration and hit-testing are handled by higher layers.
        /// </summary>
        public int LinkId { get; }

        /// <summary>Gets the absolute hyperlink target, or null for no terminal hyperlink.</summary>
        public Uri? Hyperlink { get; }

        /// <summary>Returns a copy with an absolute HTTP, HTTPS, file, or mailto target, or null to remove it.</summary>
        /// <param name="uri">The target, or null.</param>
        /// <returns>The derived style.</returns>
        /// <exception cref="ArgumentException">The target is relative, unsafe, or uses an unsupported scheme.</exception>
        public CellStyle WithHyperlink(Uri? uri)
        {
            if (uri != null)
                Terminal.HyperlinkOutput.Validate(uri);
            return new CellStyle(Foreground, Background, Attributes, LinkId, uri);
        }

        private CellStyle(Color foreground, Color background, CellAttributes attributes, int linkId, Uri? hyperlink)
        {
            Foreground = foreground;
            Background = background;
            Attributes = attributes;
            LinkId = linkId;
            Hyperlink = hyperlink;
        }

        /// <summary>
        /// Gets the default style: default foreground and background, no attributes, and no link.
        /// </summary>
        public static CellStyle Default
        {
            get { return new CellStyle(Color.Default, Color.Default, CellAttributes.None, 0); }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CellStyle"/> struct.
        /// </summary>
        /// <param name="foreground">The foreground color.</param>
        /// <param name="background">The background color.</param>
        /// <param name="attributes">The text attributes. Defaults to <see cref="CellAttributes.None"/>.</param>
        /// <param name="linkId">The link identifier, or zero for no link. Defaults to zero.</param>
        public CellStyle(Color foreground, Color background, CellAttributes attributes = CellAttributes.None, int linkId = 0)
        {
            Foreground = foreground;
            Background = background;
            Attributes = attributes;
            LinkId = linkId;
            Hyperlink = null;
        }

        /// <summary>
        /// Returns a copy of this style with a different foreground color.
        /// </summary>
        /// <param name="foreground">The new foreground color.</param>
        /// <returns>The derived style.</returns>
        public CellStyle WithForeground(Color foreground)
        {
            return new CellStyle(foreground, Background, Attributes, LinkId, Hyperlink);
        }

        /// <summary>
        /// Returns a copy of this style with a different background color.
        /// </summary>
        /// <param name="background">The new background color.</param>
        /// <returns>The derived style.</returns>
        public CellStyle WithBackground(Color background)
        {
            return new CellStyle(Foreground, background, Attributes, LinkId, Hyperlink);
        }

        /// <summary>
        /// Returns a copy of this style with the supplied attributes replacing the current set.
        /// </summary>
        /// <param name="attributes">The new attribute set.</param>
        /// <returns>The derived style.</returns>
        public CellStyle WithAttributes(CellAttributes attributes)
        {
            return new CellStyle(Foreground, Background, attributes, LinkId, Hyperlink);
        }

        /// <summary>
        /// Returns a copy of this style with the supplied attribute added or removed.
        /// </summary>
        /// <param name="attribute">The attribute to toggle.</param>
        /// <param name="enabled">Whether the attribute should be present.</param>
        /// <returns>The derived style.</returns>
        public CellStyle WithAttribute(CellAttributes attribute, bool enabled)
        {
            CellAttributes updated = enabled ? (Attributes | attribute) : (Attributes & ~attribute);
            return new CellStyle(Foreground, Background, updated, LinkId, Hyperlink);
        }

        /// <summary>
        /// Returns a copy of this style with a different link identifier.
        /// </summary>
        /// <param name="linkId">The new link identifier, or zero for no link.</param>
        /// <returns>The derived style.</returns>
        public CellStyle WithLinkId(int linkId)
        {
            return new CellStyle(Foreground, Background, Attributes, linkId, Hyperlink);
        }

        /// <summary>
        /// Composes this style over a background style: any channel left at
        /// <see cref="ColorKind.Default"/> here inherits the corresponding color from
        /// <paramref name="background"/>. Attributes and link id are taken from this style. Used so
        /// themed panes show their background behind text that does not set its own colors.
        /// </summary>
        /// <param name="background">The background style to inherit default colors from.</param>
        /// <returns>The composed style.</returns>
        public CellStyle Over(CellStyle background)
        {
            Color foreground = Foreground.Kind != ColorKind.Default ? Foreground : background.Foreground;
            Color back = Background.Kind != ColorKind.Default ? Background : background.Background;
            return new CellStyle(foreground, back, Attributes, LinkId, Hyperlink);
        }

        /// <summary>
        /// Determines whether the supplied attribute is present.
        /// </summary>
        /// <param name="attribute">The attribute to test.</param>
        /// <returns><c>true</c> if present; otherwise <c>false</c>.</returns>
        public bool HasAttribute(CellAttributes attribute)
        {
            return (Attributes & attribute) == attribute;
        }

        /// <inheritdoc/>
        public bool Equals(CellStyle other)
        {
            return Foreground.Equals(other.Foreground)
                && Background.Equals(other.Background)
                && Attributes == other.Attributes
                && LinkId == other.LinkId
                && string.Equals(Hyperlink?.AbsoluteUri, other.Hyperlink?.AbsoluteUri, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is CellStyle other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) ^ Foreground.GetHashCode();
                hash = (hash * 31) ^ Background.GetHashCode();
                hash = (hash * 31) ^ (int)Attributes;
                hash = (hash * 31) ^ LinkId;
                hash = (hash * 31) ^ (Hyperlink == null ? 0 : StringComparer.Ordinal.GetHashCode(Hyperlink.AbsoluteUri));
                return hash;
            }
        }

        /// <summary>
        /// Determines whether two styles are equal.
        /// </summary>
        /// <param name="left">The first style.</param>
        /// <param name="right">The second style.</param>
        /// <returns><c>true</c> if the styles are equal; otherwise <c>false</c>.</returns>
        public static bool operator ==(CellStyle left, CellStyle right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two styles are not equal.
        /// </summary>
        /// <param name="left">The first style.</param>
        /// <param name="right">The second style.</param>
        /// <returns><c>true</c> if the styles differ; otherwise <c>false</c>.</returns>
        public static bool operator !=(CellStyle left, CellStyle right)
        {
            return !left.Equals(right);
        }
    }
}
