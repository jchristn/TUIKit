namespace TUIKit.Widgets
{
    using System;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A labeled checkbox. Space or Enter toggles it.
    /// </summary>
    public sealed class Checkbox : IWidget, IFocusable
    {
        private readonly string _Label;

        /// <summary>
        /// Gets or sets a value indicating whether the checkbox is checked.
        /// </summary>
        public bool Checked { get; set; }

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
            surface.DrawText(0, 0, mark + _Label, CellStyle.Default);
        }
    }
}
