namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A labeled checkbox. Space or Enter toggles it, as does a left click. While hover tracking is
    /// on, the checkbox renders with <see cref="HoverStyle"/> when the pointer is over it.
    /// </summary>
    public sealed class Checkbox : IWidget, IFocusable, IMouseAware
    {
        private readonly string _Label;
        private bool _Hovered;

        /// <summary>
        /// Gets or sets a value indicating whether the checkbox is checked.
        /// </summary>
        public bool Checked { get; set; }

        /// <summary>
        /// Gets or sets the style used while the pointer is over the checkbox. Defaults to bold
        /// default text.
        /// </summary>
        public CellStyle HoverStyle { get; set; } = CellStyle.Default.WithAttributes(CellAttributes.Bold);

        /// <summary>
        /// Initializes a new instance of the <see cref="Checkbox"/> class.
        /// </summary>
        /// <param name="label">The label. Must not be null.</param>
        /// <param name="isChecked">The initial checked state. Defaults to false.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="label"/> is null.</exception>
        public Checkbox(string label, bool isChecked = false)
        {
            _Label = label ?? throw new ArgumentNullException(nameof(label));
            Checked = isChecked;
        }

        /// <summary>
        /// Toggles the checkbox on Space or Enter.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key toggled the checkbox; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Enter || (key.Code == KeyCode.Character && key.Rune == ' '))
            {
                Checked = !Checked;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Toggles the checkbox on a left press and tracks hover for <see cref="HoverStyle"/>
        /// rendering. Enter/Move/Leave events are observed but never consumed.
        /// </summary>
        /// <param name="mouse">The mouse event in widget-local coordinates. Must not be null.</param>
        /// <returns><c>true</c> when a press toggled the checkbox; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            switch (mouse.Kind)
            {
                case MouseEventKind.Press:
                    if (mouse.Button == MouseButton.Left)
                    {
                        Checked = !Checked;
                        return true;
                    }

                    return false;
                case MouseEventKind.Enter:
                    _Hovered = true;
                    return false;
                case MouseEventKind.Leave:
                    _Hovered = false;
                    return false;
                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(Math.Min(available.Width, _Label.Length + 4), available.Height > 0 ? 1 : 0);
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            string mark = Checked ? "[x] " : "[ ] ";
            surface.DrawText(0, 0, mark + _Label, _Hovered ? HoverStyle : CellStyle.Default);
        }
    }
}
