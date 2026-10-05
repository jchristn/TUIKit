namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A single-row widget that renders contextual keybinding hints as <c>key label</c> pairs across
    /// the width, the footer bar most good TUIs carry. Hints are added fluently and truncate when the
    /// width runs out. Set <see cref="HintSource"/> (or call <c>TuiApplication.BindKeyHints</c>) to have
    /// the bar list the keys of whatever holds focus, ahead of the fixed hints.
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
        /// Gets or sets a callback that supplies hints at render time, drawn before the fixed hints added
        /// with <see cref="Add"/> (which then act as always-shown, pinned hints). Use it to make the bar
        /// follow focus: <c>TuiApplication.BindKeyHints</c> sets it to resolve the current
        /// <c>FocusPath</c>. Null entries in the returned list are skipped. Defaults to null (fixed hints
        /// only, as in earlier versions). A supplied hint that does not fit in the remaining width is
        /// dropped whole, together with the rest of the list. Called on the render thread.
        /// </summary>
        public Func<IReadOnlyList<KeyHint>>? HintSource { get; set; }

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
            IReadOnlyList<KeyHint>? hints = HintSource?.Invoke();
            if (hints != null)
            {
                for (int i = 0; i < hints.Count && cursor < width; i++)
                {
                    KeyHint? hint = hints[i];
                    if (hint == null)
                        continue;

                    // A dynamic hint is drawn whole or not at all, so a narrow bar never shows half a key.
                    int needed = TUIKit.Unicode.TextFit.Width(hint.Key) + 1 + TUIKit.Unicode.TextFit.Width(hint.Description);
                    if (cursor + needed > width)
                        break;

                    cursor += surface.DrawText(cursor, 0, hint.Key, KeyStyle);
                    cursor += surface.DrawText(cursor, 0, " " + hint.Description + "   ", LabelStyle);
                }
            }

            for (int i = 0; i < _Keys.Count && cursor < width; i++)
            {
                cursor += surface.DrawText(cursor, 0, _Keys[i], KeyStyle);
                if (cursor < width)
                    cursor += surface.DrawText(cursor, 0, " " + _Labels[i] + "   ", LabelStyle);
            }
        }
    }
}
