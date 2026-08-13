namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// A dialog that presents a list of options, each of which can be checked independently. Up/Down
    /// move the cursor, Space toggles the option under it, and the check-all key (default 'a') toggles
    /// every option. Enter completes with the checked indices as an
    /// <see cref="IReadOnlyList{Int32}"/> (possibly empty); Escape cancels and completes with null.
    /// </summary>
    /// <typeparam name="T">The option type.</typeparam>
    public sealed class MultiSelectModal<T> : DialogModal
    {
        private readonly CheckList<T> _List;
        private readonly int _NaturalWidth;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiSelectModal{T}"/> class.
        /// </summary>
        /// <param name="title">The dialog title. Must not be null.</param>
        /// <param name="options">The options. Must not be null or empty.</param>
        /// <param name="display">
        /// A selector mapping an option to its label, or null when <typeparamref name="T"/> is
        /// <see cref="string"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="title"/> or <paramref name="options"/> is null, or when
        /// <paramref name="display"/> is null and <typeparamref name="T"/> is not <see cref="string"/>.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="options"/> is empty.</exception>
        public MultiSelectModal(string title, IReadOnlyList<T> options, Func<T, string>? display = null)
        {
            if (title == null)
                throw new ArgumentNullException(nameof(title));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (options.Count == 0)
                throw new ArgumentException("At least one option is required.", nameof(options));

            _List = new CheckList<T>(options, display);
            Title = title;
            FooterHint = "Space: toggle · a: all · Enter: ok · Esc: cancel";

            int natural = TUIKit.Unicode.Graphemes.MeasureWidth(title);
            for (int i = 0; i < options.Count; i++)
            {
                int rowWidth = 4 + TUIKit.Unicode.Graphemes.MeasureWidth(DisplayOf(options[i], display));
                if (rowWidth > natural)
                    natural = rowWidth;
            }

            _NaturalWidth = Math.Max(24, natural);
            MinContentWidth = Math.Min(24, _NaturalWidth);
            MaxContentHeight = Math.Max(1, options.Count);
        }

        /// <summary>
        /// Gets the wrapped check list, exposed so callers can pre-check options before showing the
        /// dialog.
        /// </summary>
        public CheckList<T> List
        {
            get { return _List; }
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Enter)
            {
                Close(_List.CheckedIndices);
                return true;
            }

            if (key.Code == KeyCode.Escape)
            {
                RequestClose(null);
                return true;
            }

            _List.HandleKey(key);
            return true;
        }

        /// <inheritdoc/>
        protected override int MeasureContentWidth(int availableWidth)
        {
            return _NaturalWidth;
        }

        /// <inheritdoc/>
        protected override int MeasureContentHeight(int contentWidth)
        {
            return Math.Max(1, _List.Items.Count);
        }

        /// <inheritdoc/>
        protected override void RenderContent(ISurface content)
        {
            _List.IsFocused = true;
            _List.Render(content);
        }

        private static string DisplayOf(T item, Func<T, string>? display)
        {
            if (display != null)
                return display(item) ?? string.Empty;

            object? boxed = item;
            return boxed == null ? string.Empty : (string)boxed;
        }
    }
}
