namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Unicode;

    /// <summary>
    /// A vertical list of labeled values with optional section headers, for status panels, telemetry
    /// sidebars, and property inspectors. Labels sit at the left; values are right-aligned to the panel
    /// width and truncated (never the label) when space is tight. Rows are keyed by label, so setting
    /// the same label again updates its value in place, which keeps a live-updating panel flicker-free.
    /// </summary>
    /// <remarks>
    /// All mutators and rendering are thread-safe, so a background thread may update values while the
    /// render thread draws.
    /// </remarks>
    public sealed class DefinitionList : IWidget
    {
        private readonly object _Sync = new object();
        private readonly List<DefinitionRow> _Rows = new List<DefinitionRow>();

        /// <summary>
        /// Gets or sets the style for labels. Defaults to a muted gray.
        /// </summary>
        public CellStyle LabelStyle { get; set; } = CellStyle.Default.WithForeground(Color.FromPalette(8));

        /// <summary>
        /// Gets or sets the style for values. Defaults to the terminal default.
        /// </summary>
        public CellStyle ValueStyle { get; set; } = CellStyle.Default;

        /// <summary>
        /// Gets or sets the style for section headers. Defaults to bold cyan.
        /// </summary>
        public CellStyle SectionStyle { get; set; } = CellStyle.Default
            .WithForeground(Color.FromPalette(6))
            .WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Gets a snapshot of the current rows in order. Never null.
        /// </summary>
        public IReadOnlyList<DefinitionRow> Rows
        {
            get
            {
                lock (_Sync)
                    return new List<DefinitionRow>(_Rows);
            }
        }

        /// <summary>
        /// Sets the value for a label, adding a new row when the label is not present or updating the
        /// existing row in place when it is.
        /// </summary>
        /// <param name="label">The row label. Must not be null.</param>
        /// <param name="value">The value. Null is treated as an empty value.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public void Set(string label, string? value)
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));

            lock (_Sync)
            {
                for (int i = 0; i < _Rows.Count; i++)
                {
                    if (!_Rows[i].IsSection && string.Equals(_Rows[i].Label, label, StringComparison.Ordinal))
                    {
                        _Rows[i] = new DefinitionRow(label, value ?? string.Empty, false);
                        return;
                    }
                }

                _Rows.Add(new DefinitionRow(label, value ?? string.Empty, false));
            }
        }

        /// <summary>
        /// Appends a section header.
        /// </summary>
        /// <param name="title">The section title. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> is null.</exception>
        public void AddSection(string title)
        {
            if (title == null)
                throw new ArgumentNullException(nameof(title));

            lock (_Sync)
                _Rows.Add(new DefinitionRow(title, null, true));
        }

        /// <summary>
        /// Removes the labeled-value row with the supplied label, if present.
        /// </summary>
        /// <param name="label">The label. Must not be null.</param>
        /// <returns><c>true</c> when a row was removed; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public bool Remove(string label)
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));

            lock (_Sync)
            {
                for (int i = 0; i < _Rows.Count; i++)
                {
                    if (!_Rows[i].IsSection && string.Equals(_Rows[i].Label, label, StringComparison.Ordinal))
                    {
                        _Rows.RemoveAt(i);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Removes every row.
        /// </summary>
        public void Clear()
        {
            lock (_Sync)
                _Rows.Clear();
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            lock (_Sync)
                return new Size(available.Width, Math.Min(_Rows.Count, available.Height));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0)
                return;

            List<DefinitionRow> rows;
            lock (_Sync)
                rows = new List<DefinitionRow>(_Rows);

            int count = Math.Min(rows.Count, height);
            for (int i = 0; i < count; i++)
            {
                DefinitionRow row = rows[i];
                if (row.IsSection)
                {
                    surface.DrawText(0, i, Fit(row.Label, width), SectionStyle);
                    continue;
                }

                string value = row.Value ?? string.Empty;
                int valueWidth = Graphemes.MeasureWidth(value);
                int labelWidth = Graphemes.MeasureWidth(row.Label);

                surface.DrawText(0, i, Fit(row.Label, width), LabelStyle);

                if (valueWidth > 0 && width - labelWidth - 1 >= 1)
                {
                    int maxValueWidth = width - labelWidth - 1;
                    string fitted = Fit(value, maxValueWidth);
                    int fittedWidth = Graphemes.MeasureWidth(fitted);
                    int valueX = width - fittedWidth;
                    surface.DrawText(valueX, i, fitted, ValueStyle);
                }
            }
        }

        private static string Fit(string text, int maxWidth)
        {
            if (maxWidth < 1)
                return string.Empty;
            if (Graphemes.MeasureWidth(text) <= maxWidth)
                return text;

            string clipped = text;
            while (clipped.Length > 0 && Graphemes.MeasureWidth(clipped) > maxWidth - 1)
                clipped = clipped.Substring(0, clipped.Length - 1);

            return clipped + "…";
        }
    }
}
