namespace TUIKit.Widgets
{
    using System;
    using TUIKit;

    /// <summary>
    /// A single-line widget that renders styled text.
    /// </summary>
    public sealed class Label : IWidget
    {
        private StyledText _Content;

        /// <summary>
        /// Gets or sets the label content. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public StyledText Content
        {
            get { return _Content; }
            set { _Content = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Label"/> class.
        /// </summary>
        /// <param name="content">The content. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
        public Label(StyledText content)
        {
            _Content = content ?? throw new ArgumentNullException(nameof(content));
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            int width = Math.Min(available.Width, _Content.Width);
            return new Size(width, available.Height > 0 ? 1 : 0);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            surface.DrawStyledText(0, 0, _Content);
        }
    }
}
