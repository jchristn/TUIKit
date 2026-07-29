namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// Displays several concurrent <see cref="ProgressTask"/> bars at once — one row each with a
    /// label, a proportional bar, and a percentage — for downloads, parallel jobs, or multi-stage
    /// pipelines. Completed tasks show a check mark. Add tasks up front and report progress from any
    /// thread; re-render to refresh.
    /// </summary>
    public sealed class MultiProgress : IWidget
    {
        private readonly List<ProgressTask> _Tasks = new List<ProgressTask>();

        /// <summary>
        /// Gets or sets the color of in-progress bars. Defaults to cyan.
        /// </summary>
        public Color Color { get; set; } = Color.FromPalette(6);

        /// <summary>
        /// Gets the number of tracked tasks.
        /// </summary>
        public int Count
        {
            get { return _Tasks.Count; }
        }

        /// <summary>
        /// Adds a task and returns it so the caller can report progress against it.
        /// </summary>
        /// <param name="label">The task label. Must not be null.</param>
        /// <param name="value">The initial completion fraction.</param>
        /// <returns>The created task.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public ProgressTask Add(string label, double value = 0)
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));

            ProgressTask task = new ProgressTask(label, value);
            _Tasks.Add(task);
            return task;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(available.Height, _Tasks.Count));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0 || _Tasks.Count == 0)
                return;

            int labelWidth = 0;
            for (int i = 0; i < _Tasks.Count; i++)
            {
                if (_Tasks[i].Label.Length > labelWidth)
                    labelWidth = _Tasks[i].Label.Length;
            }

            labelWidth = Math.Min(labelWidth, Math.Max(1, width / 3));

            for (int row = 0; row < height && row < _Tasks.Count; row++)
            {
                ProgressTask task = _Tasks[row];
                string label = Fit(task.Label, labelWidth).PadRight(labelWidth);
                surface.DrawText(0, row, label, CellStyle.Default);

                string percent = " " + ((int)Math.Round(task.Value * 100)).ToString() + "%";
                int barStart = labelWidth + 1;
                int barWidth = width - barStart - percent.Length;
                if (barWidth > 0)
                {
                    int filled = (int)Math.Round(task.Value * barWidth);
                    CellStyle barStyle = CellStyle.Default.WithForeground(task.IsComplete ? Color.FromPalette(2) : Color);
                    for (int x = 0; x < barWidth; x++)
                        surface.DrawText(barStart + x, row, x < filled ? "█" : "░", barStyle);
                }

                CellStyle percentStyle = task.IsComplete
                    ? CellStyle.Default.WithForeground(Color.FromPalette(2))
                    : CellStyle.Default;
                surface.DrawText(width - percent.Length, row, percent, percentStyle);
            }
        }

        private static string Fit(string text, int width)
        {
            if (text.Length <= width)
                return text;
            if (width <= 1)
                return text.Substring(0, Math.Max(0, width));

            return text.Substring(0, width - 1) + "…";
        }
    }
}
