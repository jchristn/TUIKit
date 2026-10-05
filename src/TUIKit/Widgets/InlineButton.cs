namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Unicode;

    /// <summary>
    /// Draws a small clickable button inside custom-rendered content, such as <c>[Approve] a</c> at the
    /// end of a list row or a transcript card, and records its area in a
    /// <see cref="ClickRegionMap{TAction}"/> so a click on it raises the button's action. The key after
    /// the label tells keyboard users the equivalent key; give every button one.
    /// </summary>
    /// <remarks>
    /// A button that does not fit in the remaining width is not drawn at all and records no area, so a
    /// narrow surface never shows half a button that cannot be clicked. Stateless and thread-safe; draw on
    /// the render thread.
    /// </remarks>
    public static class InlineButton
    {
        /// <summary>
        /// Gets the width a button occupies: <c>[label]</c>, plus a space and the key when one is given.
        /// </summary>
        /// <param name="label">The label. Must not be null.</param>
        /// <param name="key">The key text, or null.</param>
        /// <returns>The width in cells.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public static int Measure(string label, string? key)
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));

            int width = TextFit.Width(label) + 2;
            if (!string.IsNullOrEmpty(key))
                width += 1 + TextFit.Width(key!);

            return width;
        }

        /// <summary>
        /// Draws a button at a position and records its area in the map.
        /// </summary>
        /// <typeparam name="TAction">The application's action type.</typeparam>
        /// <param name="surface">The target surface. Must not be null.</param>
        /// <param name="x">The zero-based column to start at.</param>
        /// <param name="y">The zero-based row.</param>
        /// <param name="label">The label, drawn in brackets. Must not be null.</param>
        /// <param name="key">The equivalent key, drawn after the label, or null.</param>
        /// <param name="action">The action a click raises.</param>
        /// <param name="map">The map that records the area. Must not be null.</param>
        /// <param name="style">The styles, or null for the defaults (see <see cref="InlineButtonStyle"/>).</param>
        /// <returns>The width drawn, or 0 when the button did not fit (nothing drawn, nothing recorded).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/>,
        /// <paramref name="label"/>, or <paramref name="map"/> is null.</exception>
        public static int Draw<TAction>(ISurface surface, int x, int y, string label, string? key, TAction action, ClickRegionMap<TAction> map, InlineButtonStyle? style = null)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (label == null)
                throw new ArgumentNullException(nameof(label));
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            int width = Measure(label, key);
            if (x < 0 || y < 0 || y >= surface.Size.Height || x + width > surface.Size.Width)
                return 0;

            InlineButtonStyle styles = style ?? new InlineButtonStyle();
            Rect area = new Rect(x, y, width, 1);
            CellStyle labelStyle = map.IsHovered(area) ? styles.Hover : styles.Label;
            int cursor = x + surface.DrawText(x, y, "[" + label + "]", labelStyle);
            if (!string.IsNullOrEmpty(key))
            {
                cursor += surface.DrawText(cursor, y, " ", styles.Key);
                surface.DrawText(cursor, y, key!, styles.Key);
            }

            map.Add(area, action, key);
            return width;
        }
    }
}
