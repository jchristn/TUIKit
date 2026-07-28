namespace TUIKit.Modals
{
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// Base class for a modal dialog. A modal renders into a surface, handles keys while it holds the
    /// focus trap, and completes with a result. Await <see cref="Completion"/> to receive the result.
    /// Derived types call <see cref="Close"/> to dismiss with a value.
    /// </summary>
    public abstract class Modal
    {
        private readonly TaskCompletionSource<object?> _Completion =
            new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets the task that completes with the modal's result when it closes.
        /// </summary>
        public Task<object?> Completion
        {
            get { return _Completion.Task; }
        }

        /// <summary>
        /// Gets a value indicating whether the modal has closed.
        /// </summary>
        public bool IsClosed
        {
            get { return _Completion.Task.IsCompleted; }
        }

        /// <summary>
        /// Gets a value indicating whether the modal may be closed by an outside request such as
        /// Escape. Override to refuse (for example when there are unsaved changes). Defaults to true.
        /// </summary>
        public virtual bool CanClose
        {
            get { return true; }
        }

        /// <summary>
        /// Renders the modal into the supplied surface, which covers the whole screen. The modal
        /// draws its own centered box.
        /// </summary>
        /// <param name="surface">The screen surface. Must not be null.</param>
        public abstract void Render(ISurface surface);

        /// <summary>
        /// Handles a key event while the modal holds focus.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when the key was consumed; otherwise <c>false</c>.</returns>
        public abstract bool HandleKey(KeyEvent key);

        /// <summary>
        /// Requests a close from outside (for example Escape). Honors <see cref="CanClose"/>.
        /// </summary>
        /// <param name="result">The result to complete with when the close is allowed.</param>
        /// <returns><c>true</c> when the modal closed; <c>false</c> when it refused.</returns>
        public bool RequestClose(object? result)
        {
            if (!CanClose)
                return false;

            Close(result);
            return true;
        }

        /// <summary>
        /// Closes the modal with the supplied result. Safe to call once; later calls are ignored.
        /// </summary>
        /// <param name="result">The result value.</param>
        protected void Close(object? result)
        {
            _Completion.TrySetResult(result);
        }
    }
}
