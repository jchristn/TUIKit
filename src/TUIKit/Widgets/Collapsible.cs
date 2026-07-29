namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A header line that expands or collapses a child widget. Useful for tool-call output, log
    /// groups, and detail blocks that should be scannable when collapsed. Enter or Space toggles it.
    /// </summary>
    public sealed class Collapsible : IWidget
    {
        private readonly string _Header;
        private readonly IWidget _Child;

        /// <summary>
        /// Gets or sets a value indicating whether the child is shown. Defaults to true.
        /// </summary>
        public bool Expanded { get; set; } = true;

        /// <summary>
        /// Gets or sets the header style. Defaults to bold.
        /// </summary>
        public CellStyle HeaderStyle { get; set; } = CellStyle.Default.WithAttribute(CellAttributes.Bold, true);

        /// <summary>
        /// Initializes a new instance of the <see cref="Collapsible"/> class.
        /// </summary>
        /// <param name="header">The header text. Must not be null.</param>
        /// <param name="child">The child widget shown when expanded. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public Collapsible(string header, IWidget child)
        {
            _Header = header ?? throw new ArgumentNullException(nameof(header));
            _Child = child ?? throw new ArgumentNullException(nameof(child));
        }

        /// <summary>
        /// Toggles the expanded state.
        /// </summary>
        public void Toggle()
        {
            Expanded = !Expanded;
        }

        /// <summary>
        /// Toggles on Enter or Space.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key toggled the section; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Enter || (key.Code == KeyCode.Character && key.Rune == ' '))
            {
                Toggle();
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            int height = 1;
            if (Expanded && available.Height > 1)
                height += _Child.Measure(new Size(available.Width, available.Height - 1)).Height;

            return new Size(available.Width, Math.Min(available.Height, height));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            string disclosure = Expanded ? "▼ " : "▶ ";
            surface.DrawText(0, 0, disclosure + _Header, HeaderStyle);

            if (Expanded && surface is BufferSurface buffer && surface.Size.Height > 1)
                _Child.Render(buffer.CreateView(new Rect(0, 1, surface.Size.Width, surface.Size.Height - 1)));
        }
    }
}
