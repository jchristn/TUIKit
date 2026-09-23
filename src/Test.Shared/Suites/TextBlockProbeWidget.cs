namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Widgets;

    /// <summary>
    /// A minimal widget that renders a fixed list of lines, one per row starting at the surface origin,
    /// so selection tests can seed a region with deterministic multi-row text and read it back from the
    /// composited buffer.
    /// </summary>
    public sealed class TextBlockProbeWidget : IWidget
    {
        private readonly IReadOnlyList<string> _Lines;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextBlockProbeWidget"/> class.
        /// </summary>
        /// <param name="lines">The lines to render, top to bottom. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="lines"/> is null.</exception>
        public TextBlockProbeWidget(IReadOnlyList<string> lines)
        {
            _Lines = lines ?? throw new ArgumentNullException(nameof(lines));
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            for (int row = 0; row < _Lines.Count && row < surface.Size.Height; row++)
                surface.DrawStyledText(0, row, Text.From(_Lines[row]));
        }
    }
}
