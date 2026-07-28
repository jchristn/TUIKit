namespace TUIKit.Content
{
    using System;
    using TUIKit;

    /// <summary>
    /// A handle to a committed pane line that allows its content to be replaced in place — for
    /// example turning <c>running…</c> into <c>done (1.2s)</c> or advancing a progress bar. Handles
    /// remain valid until the line is evicted from scrollback, after which updates are ignored.
    /// Handles are thread-safe.
    /// </summary>
    public sealed class PaneLineHandle
    {
        private readonly Pane _Pane;
        private readonly long _Id;

        internal PaneLineHandle(Pane pane, long id)
        {
            _Pane = pane;
            _Id = id;
        }

        /// <summary>
        /// Gets the identifier of the line this handle refers to.
        /// </summary>
        public long Id
        {
            get { return _Id; }
        }

        /// <summary>
        /// Replaces the line's content with the supplied styled text.
        /// </summary>
        /// <param name="content">The new content. Must not be null.</param>
        /// <returns><c>true</c> when the line was found and updated; <c>false</c> when it has been evicted.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
        public bool Update(StyledText content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            return _Pane.UpdateLine(_Id, content);
        }

        /// <summary>
        /// Replaces the line's content with the supplied plain text in the default style.
        /// </summary>
        /// <param name="content">The new content. Must not be null.</param>
        /// <returns><c>true</c> when the line was found and updated; <c>false</c> when it has been evicted.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
        public bool Update(string content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            return _Pane.UpdateLine(_Id, Text.From(content));
        }
    }
}
