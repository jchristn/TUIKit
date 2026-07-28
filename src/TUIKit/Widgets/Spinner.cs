namespace TUIKit.Widgets
{
    using System;
    using TUIKit;

    /// <summary>
    /// An animated spinner. Advance it once per frame while a task runs and render its current frame.
    /// </summary>
    public sealed class Spinner : IWidget
    {
        private readonly string[] _Frames;
        private int _Index;

        /// <summary>
        /// Gets or sets the spinner color. Defaults to palette yellow.
        /// </summary>
        public Color Color { get; set; } = TUIKit.Color.FromPalette(3);

        /// <summary>
        /// Initializes a new instance of the <see cref="Spinner"/> class with the default braille frames.
        /// </summary>
        public Spinner()
            : this(new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Spinner"/> class with custom frames.
        /// </summary>
        /// <param name="frames">The animation frames. Must not be null or empty.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="frames"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="frames"/> is empty.</exception>
        public Spinner(string[] frames)
        {
            if (frames == null)
                throw new ArgumentNullException(nameof(frames));
            if (frames.Length == 0)
                throw new ArgumentException("At least one frame is required.", nameof(frames));

            _Frames = frames;
        }

        /// <summary>
        /// Gets the current frame glyph.
        /// </summary>
        public string CurrentFrame
        {
            get { return _Frames[_Index]; }
        }

        /// <summary>
        /// Advances to the next frame, wrapping around.
        /// </summary>
        public void Advance()
        {
            _Index = (_Index + 1) % _Frames.Length;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(Math.Min(available.Width, 1), available.Height > 0 ? 1 : 0);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            surface.DrawText(0, 0, _Frames[_Index], CellStyle.Default.WithForeground(Color));
        }
    }
}
