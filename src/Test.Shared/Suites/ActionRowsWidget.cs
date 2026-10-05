namespace Test.Shared.Suites
{
    using System;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A custom-rendered test widget that draws one row per item with two inline buttons,
    /// <c>[Open] o</c> and <c>[Delete] d</c>, recording each in a <see cref="ClickRegionMap{TAction}"/>
    /// whose action text is <c>"row:open"</c> or <c>"row:delete"</c>. It follows the documented rule:
    /// clear the map on every render and record areas in drawing coordinates.
    /// </summary>
    public sealed class ActionRowsWidget : IWidget, IMouseAware
    {
        private readonly int _Rows;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionRowsWidget"/> class.
        /// </summary>
        /// <param name="rows">The number of rows. Must be zero or greater.</param>
        public ActionRowsWidget(int rows)
        {
            if (rows < 0)
                throw new ArgumentOutOfRangeException(nameof(rows));

            _Rows = rows;
        }

        /// <summary>
        /// Gets the click map the widget records into. Never null.
        /// </summary>
        public ClickRegionMap<string> Map { get; } = new ClickRegionMap<string>();

        /// <summary>
        /// Gets the last value returned by <see cref="InlineButton.Draw"/> for the second button on row 0.
        /// </summary>
        public int LastDeleteWidth { get; private set; }

        /// <summary>
        /// Passes the event to the map.
        /// </summary>
        /// <param name="mouse">The event in widget-local coordinates.</param>
        /// <returns>Whether a button was invoked.</returns>
        public bool HandleMouse(MouseEvent mouse)
        {
            return Map.HandleMouse(mouse);
        }

        /// <summary>
        /// Takes all available space.
        /// </summary>
        /// <param name="available">The available size.</param>
        /// <returns>The available size.</returns>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <summary>
        /// Draws the rows and records the buttons.
        /// </summary>
        /// <param name="surface">The surface.</param>
        public void Render(ISurface surface)
        {
            Map.Clear();
            for (int row = 0; row < _Rows && row < surface.Size.Height; row++)
            {
                int x = surface.DrawText(0, row, "r" + row.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + " ", CellStyle.Default);
                x += InlineButton.Draw(surface, x, row, "Open", "o", row + ":open", Map);
                x += 1;
                int width = InlineButton.Draw(surface, x, row, "Delete", "d", row + ":delete", Map);
                if (row == 0)
                    LastDeleteWidth = width;
            }
        }
    }
}
