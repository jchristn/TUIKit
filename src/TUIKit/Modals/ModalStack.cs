namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A stack of active modals implementing a focus trap: input goes to the topmost modal, and
    /// nested modals render on top of the ones beneath them. Background panes keep updating behind the
    /// stack; only input is trapped.
    /// </summary>
    /// <remarks>All members are thread-safe.</remarks>
    public sealed class ModalStack
    {
        private readonly object _Sync = new object();
        private readonly List<Modal> _Modals = new List<Modal>();

        /// <summary>
        /// Gets a value indicating whether any modal is active (input should be trapped).
        /// </summary>
        public bool IsActive
        {
            get { lock (_Sync) { return _Modals.Count > 0; } }
        }

        /// <summary>
        /// Gets the number of active modals.
        /// </summary>
        public int Count
        {
            get { lock (_Sync) { return _Modals.Count; } }
        }

        /// <summary>
        /// Gets the topmost modal, or null when none are active.
        /// </summary>
        public Modal? Top
        {
            get
            {
                lock (_Sync)
                {
                    return _Modals.Count > 0 ? _Modals[_Modals.Count - 1] : null;
                }
            }
        }

        /// <summary>
        /// Pushes a modal onto the stack, making it the focus target.
        /// </summary>
        /// <param name="modal">The modal. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="modal"/> is null.</exception>
        public void Push(Modal modal)
        {
            if (modal == null)
                throw new ArgumentNullException(nameof(modal));

            lock (_Sync)
                _Modals.Add(modal);
        }

        /// <summary>
        /// Routes a key to the topmost modal.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when a modal consumed the key; otherwise <c>false</c>.</returns>
        public bool HandleKey(KeyEvent key)
        {
            Modal? top;
            lock (_Sync)
                top = _Modals.Count > 0 ? _Modals[_Modals.Count - 1] : null;

            if (top == null)
                return false;

            bool handled = top.HandleKey(key);
            RemoveClosed();
            return handled;
        }

        /// <summary>
        /// Removes modals that have closed, from the top down.
        /// </summary>
        public void RemoveClosed()
        {
            lock (_Sync)
            {
                for (int i = _Modals.Count - 1; i >= 0; i--)
                {
                    if (_Modals[i].IsClosed)
                        _Modals.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Renders every active modal from bottom to top so the topmost appears in front.
        /// </summary>
        /// <param name="surface">The screen surface. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            Modal[] snapshot;
            lock (_Sync)
                snapshot = _Modals.ToArray();

            for (int i = 0; i < snapshot.Length; i++)
                snapshot[i].Render(surface);
        }
    }
}
