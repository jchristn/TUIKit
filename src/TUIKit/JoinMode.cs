namespace TUIKit
{
    /// <summary>
    /// How a joined box treats box lines already on the surface where its outline crosses them (see
    /// <see cref="SurfaceExtensions.DrawJoinedBox(ISurface, Rect, CellStyle, BorderStyle, string?, CellStyle, JoinMode)"/>).
    /// </summary>
    public enum JoinMode
    {
        /// <summary>
        /// Merge with existing lines: where both have an arm this box's weight wins, and arms from other
        /// lines keep their own weight, so a heavy box meeting a light line produces mixed junctions such as
        /// <c>┱</c>. The behavior of every joined box before 1.5.0.
        /// </summary>
        Merge = 0,

        /// <summary>
        /// Draw this box whole: every cell of its outline is drawn in the box's own weight only. Lines from
        /// neighbours that end on the outline stay connected, but their arms inside the outline cells take
        /// this box's weight, so a heavy focused box shows only heavy glyphs (<c>┳</c>, never <c>┱</c>).
        /// Mixed-weight junctions are rare in terminal fonts, so this also keeps the focused frame from
        /// breaking up into fallback glyphs. Cells outside the outline are untouched. ASCII borders draw
        /// the same as <see cref="Merge"/>, since they have no mixed junctions.
        /// </summary>
        OverlayWhole = 1,

        /// <summary>
        /// Ignore existing lines: draw a plain box over them, exactly like
        /// <see cref="SurfaceExtensions.DrawBox(ISurface, Rect, CellStyle, BorderStyle, string?, CellStyle)"/>.
        /// </summary>
        None = 2
    }
}
