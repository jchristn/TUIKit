namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// Composes labeled input widgets into a form with a shared tab order and optional per-field
    /// validation. Tab and Shift+Tab move between fields; other keys go to the focused field.
    /// </summary>
    public sealed class Form : IWidget, IFocusable
    {
        private readonly List<FormField> _Fields = new List<FormField>();
        private readonly FocusManager _Focus = new FocusManager();

        /// <summary>
        /// Gets the number of fields.
        /// </summary>
        public int FieldCount
        {
            get { return _Fields.Count; }
        }

        /// <summary>
        /// Gets the zero-based index of the focused field, or -1 when the form is empty.
        /// </summary>
        public int FocusedIndex
        {
            get { return _Focus.FocusedIndex; }
        }

        /// <summary>
        /// Adds a field.
        /// </summary>
        /// <typeparam name="TWidget">The widget type, which must be interactive.</typeparam>
        /// <param name="label">The field caption. Must not be null.</param>
        /// <param name="widget">The input widget. Must not be null.</param>
        /// <param name="validator">An optional validator returning an error message, or null when valid.</param>
        /// <returns>The widget, for further configuration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public TWidget Add<TWidget>(string label, TWidget widget, Func<string?>? validator = null)
            where TWidget : IWidget, IFocusable
        {
            if (label == null)
                throw new ArgumentNullException(nameof(label));
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            _Fields.Add(new FormField(label, widget, widget, validator));
            _Focus.Register(widget);
            return widget;
        }

        /// <summary>
        /// Runs every field validator and returns the first error message, or null when all fields
        /// are valid.
        /// </summary>
        /// <returns>The first validation error, or null.</returns>
        public string? Validate()
        {
            for (int i = 0; i < _Fields.Count; i++)
            {
                if (_Fields[i].Validator != null)
                {
                    string? error = _Fields[i].Validator!();
                    if (error != null)
                        return error;
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            return _Focus.HandleKey(key);
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

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0)
                return;

            BufferSurface? buffer = surface as BufferSurface;
            int y = 0;
            for (int i = 0; i < _Fields.Count && y < height; i++)
            {
                FormField field = _Fields[i];
                bool focused = _Focus.FocusedIndex == i;
                CellStyle labelStyle = CellStyle.Default.WithForeground(Color.FromPalette((byte)(focused ? 6 : 8)));
                surface.DrawText(0, y, (focused ? "> " : "  ") + field.Label, labelStyle);
                y++;

                int fieldHeight = Math.Max(1, field.Widget.Measure(new Size(width - 2, height - y)).Height);
                if (buffer != null && y < height)
                    field.Widget.Render(buffer.CreateView(new Rect(2, y, width - 2, Math.Min(fieldHeight, height - y))));

                y += fieldHeight + 1;
            }
        }
    }
}
