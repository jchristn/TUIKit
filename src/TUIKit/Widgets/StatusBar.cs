namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// A single-row widget that renders contextual keybinding hints as <c>key label</c> pairs across
    /// the width, the footer bar most good TUIs carry. Hints are added fluently and truncate when the
    /// width runs out.
    /// </summary>
    public sealed class StatusBar : IWidget
    {
        private readonly List<string> _Keys = new List<string>();
        private readonly List<string> _Labels = new List<string>();

        /// <summary>
        /// Gets or sets the style for the key text. Defaults to bold cyan.
        /// </summary>
        public CellStyle KeyStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets or sets the style for the label text. Defaults to the default style.
        /// </summary>
        public CellStyle LabelStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets the number of hints.
        /// </summary>
        public int Count
        {
            get { return _Keys.Count; }
        }

        /// <summary>
        /// Adds a hint.
        /// </summary>
        /// <param name="key">The key text, such as <c>"^Q"</c>. Must not be null.</param>
        /// <param name="label">The action label. Must not be null.</param>
        /// <returns>This status bar, for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public StatusBar Add(string key, string label)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (label == null)
                throw new ArgumentNullException(nameof(label));

            _Keys.Add(key);
            _Labels.Add(label);
            return this;
        }

        /// <summary>
        /// Removes all hints.
        /// </summary>
        public void Clear()
        {
            _Keys.Clear();
            _Labels.Clear();
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, available.Height > 0 ? 1 : 0);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            if (width <= 0 || surface.Size.Height <= 0)
                return;

            int cursor = 0;
            for (int i = 0; i < _Keys.Count && cursor < width; i++)
            {
                cursor += surface.DrawText(cursor, 0, _Keys[i], KeyStyle);
                if (cursor < width)
                    cursor += surface.DrawText(cursor, 0, " " + _Labels[i] + "   ", LabelStyle);
            }
        }
    }
}
