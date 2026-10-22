namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Theming;

    /// <summary>
    /// An ordered activity log with status markers and connected, wrapping rows. Clips at the bottom;
    /// place in a ScrollView for scrolling. Use only from the UI loop; marshal worker updates with Post.
    /// </summary>
    public sealed class ActivityTimeline : IWidget, IThemeable
    {
        private readonly List<ActivityEntry> _Entries = new List<ActivityEntry>();
        private int _Spacing = 1;

        /// <summary>Gets the entries in insertion order as a read-only view. Never null.</summary>
        public IReadOnlyList<ActivityEntry> Entries
        {
            get { return _Entries.AsReadOnly(); }
        }

        /// <summary>Gets or sets the number of connecting rows between entries. Defaults to 1; minimum 0.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for a negative value.</exception>
        public int Spacing
        {
            get { return _Spacing; }
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _Spacing = value;
            }
        }

        /// <summary>Gets or sets ASCII markers instead of Unicode. Defaults to false; ApplyTheme follows the theme.</summary>
        public bool UseAsciiGlyphs { get; set; }

        /// <summary>Gets or sets the text and fill style. Defaults to the terminal default.</summary>
        public CellStyle NormalStyle { get; set; } = CellStyle.Default;

        /// <summary>Gets or sets connector, pending, and detail styling. Defaults to dim text.</summary>
        public CellStyle MutedStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);

        /// <summary>Gets or sets the active marker style. Defaults to cyan.</summary>
        public CellStyle ActiveStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(6));

        /// <summary>Gets or sets the completed marker style. Defaults to green.</summary>
        public CellStyle CompletedStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(2));

        /// <summary>Gets or sets the failed marker style. Defaults to red.</summary>
        public CellStyle FailedStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(1));

        /// <summary>Adds an activity and returns it for subsequent updates on the UI loop.</summary>
        /// <param name="text">Primary text. Must not be null.</param>
        /// <param name="status">Initial status. Defaults to Completed.</param>
        /// <param name="detail">Optional detail. Defaults to null.</param>
        /// <returns>The added entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown when text is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined status.</exception>
        public ActivityEntry Add(string text, ActivityStatus status = ActivityStatus.Completed, string? detail = null)
        {
            ActivityEntry entry = new ActivityEntry(text, status, detail);
            _Entries.Add(entry);
            return entry;
        }

        /// <summary>Removes all activities.</summary>
        public void Clear()
        {
            _Entries.Clear();
        }

        /// <summary>Applies theme text, muted, accent, success, error, and ASCII preferences.</summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when theme is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));
            NormalStyle = theme.Text;
            MutedStyle = theme.Disabled;
            ActiveStyle = theme.Accent;
            CompletedStyle = theme.Success;
            FailedStyle = theme.Error;
            UseAsciiGlyphs = theme.UseAsciiBorders;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            if (available.IsEmpty)
                return Size.Empty;
            long height = 0;
            int width = Math.Max(1, available.Width - 3);
            foreach (ActivityEntry entry in _Entries)
            {
                if (height > 0)
                    height += Spacing;
                height += PromptTextLayout.Wrap(entry.Text, width).Count;
                if (!string.IsNullOrEmpty(entry.Detail))
                    height += PromptTextLayout.Wrap(entry.Detail!, width).Count;
                if (height >= available.Height)
                    return new Size(available.Width, available.Height);
            }
            return new Size(available.Width, (int)height);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));
            if (surface.Size.IsEmpty)
                return;
            surface.Fill(new Rect(0, 0, surface.Size.Width, surface.Size.Height), Cell.Blank(NormalStyle));
            int y = 0;
            int width = Math.Max(1, surface.Size.Width - 3);
            for (int i = 0; i < _Entries.Count && y < surface.Size.Height; i++)
            {
                ActivityEntry entry = _Entries[i];
                CellStyle markerStyle = entry.Status == ActivityStatus.Active ? ActiveStyle
                    : entry.Status == ActivityStatus.Completed ? CompletedStyle
                    : entry.Status == ActivityStatus.Failed ? FailedStyle : MutedStyle;
                IReadOnlyList<string> rows = PromptTextLayout.Wrap(entry.Text, width);
                for (int row = 0; row < rows.Count && y < surface.Size.Height; row++, y++)
                {
                    surface.DrawText(0, y, row == 0 ? Marker(entry.Status) : Connector(i),
                        (row == 0 ? markerStyle : MutedStyle).Over(NormalStyle));
                    surface.DrawText(3, y, rows[row], NormalStyle);
                }
                if (!string.IsNullOrEmpty(entry.Detail))
                {
                    foreach (string row in PromptTextLayout.Wrap(entry.Detail!, width))
                    {
                        if (y >= surface.Size.Height)
                            break;
                        surface.DrawText(0, y, Connector(i), MutedStyle.Over(NormalStyle));
                        surface.DrawText(3, y++, row, MutedStyle.Over(NormalStyle));
                    }
                }
                if (i + 1 < _Entries.Count)
                {
                    for (int gap = 0; gap < Spacing && y < surface.Size.Height; gap++)
                        surface.DrawText(0, y++, Connector(i), MutedStyle.Over(NormalStyle));
                }
            }
        }

        private string Connector(int index)
        {
            return index + 1 < _Entries.Count ? (UseAsciiGlyphs ? "|" : "│") : " ";
        }

        private string Marker(ActivityStatus status)
        {
            switch (status)
            {
                case ActivityStatus.Pending: return UseAsciiGlyphs ? "o" : "○";
                case ActivityStatus.Active: return UseAsciiGlyphs ? "*" : "●";
                case ActivityStatus.Failed: return UseAsciiGlyphs ? "x" : "×";
                default: return UseAsciiGlyphs ? "+" : "◇";
            }
        }
    }
}
