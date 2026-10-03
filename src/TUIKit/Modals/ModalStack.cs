namespace TUIKit.Modals
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Diagnostics;
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

            TrackShown(modal);
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
        /// Routes a bracketed-paste event to the topmost modal.
        /// </summary>
        /// <param name="text">The pasted text. Must not be null.</param>
        /// <returns><c>true</c> when a modal consumed the paste; otherwise <c>false</c>.</returns>
        public bool HandlePaste(string text)
        {
            Modal? top;
            lock (_Sync)
                top = _Modals.Count > 0 ? _Modals[_Modals.Count - 1] : null;

            if (top == null)
                return false;

            bool handled = top.HandlePaste(text);
            RemoveClosed();
            return handled;
        }

        /// <summary>
        /// Routes a mouse event to the topmost modal.
        /// </summary>
        /// <param name="mouse">The mouse event, in absolute screen coordinates.</param>
        /// <returns><c>true</c> when a modal consumed the event; otherwise <c>false</c>.</returns>
        public bool HandleMouse(MouseEvent mouse)
        {
            Modal? top;
            lock (_Sync)
                top = _Modals.Count > 0 ? _Modals[_Modals.Count - 1] : null;

            if (top == null)
                return false;

            bool handled = top.HandleMouse(mouse);
            RemoveClosed();
            return handled;
        }

        /// <summary>
        /// Removes modals that have closed, from the top down.
        /// </summary>
        public void RemoveClosed()
        {
            List<Modal>? removed = null;
            lock (_Sync)
            {
                for (int i = _Modals.Count - 1; i >= 0; i--)
                {
                    if (_Modals[i].IsClosed)
                    {
                        if (removed == null)
                            removed = new List<Modal>();
                        removed.Add(_Modals[i]);
                        _Modals.RemoveAt(i);
                    }
                }
            }

            if (removed != null)
            {
                for (int i = 0; i < removed.Count; i++)
                    TrackRemoved(removed[i]);
            }
        }

        private static void TrackShown(Modal modal)
        {
            if (modal.TelemetryTracked)
                return;

            string type = modal.GetType().Name;
            modal.TelemetryTracked = true;
            modal.TelemetryShownTimestamp = TuiKitInstruments.Timestamp();
            modal.TelemetrySpan = TuiKitInstruments.StartDetachedActivity(TuiKitTelemetryNames.SpanModal);
            TuiKitInstruments.SetTag(modal.TelemetrySpan, TuiKitTelemetryNames.AttrModalType, type);
            TuiKitInstruments.Add(TuiKitInstruments.ModalShown, 1, TuiKitTelemetryNames.AttrModalType, type);
            TuiKitInstruments.Add(TuiKitInstruments.ModalActive, 1);
        }

        private static void TrackRemoved(Modal modal)
        {
            if (!modal.TelemetryTracked)
                return;

            modal.TelemetryTracked = false;
            Task<object?> completion = modal.Completion;
            bool completed = completion.Status == TaskStatus.RanToCompletion && completion.Result != null;
            string outcome = completed ? TuiKitTelemetryNames.ModalCompleted : TuiKitTelemetryNames.ModalDismissed;

            TagList tags = new TagList();
            tags.Add(TuiKitTelemetryNames.AttrModalType, modal.GetType().Name);
            tags.Add(TuiKitTelemetryNames.AttrModalOutcome, outcome);
            TuiKitInstruments.Record(TuiKitInstruments.ModalDuration, TuiKitInstruments.SecondsSince(modal.TelemetryShownTimestamp), in tags);
            TuiKitInstruments.Add(TuiKitInstruments.ModalActive, -1);

            Activity? span = modal.TelemetrySpan;
            modal.TelemetrySpan = null;
            TuiKitInstruments.SetTag(span, TuiKitTelemetryNames.AttrModalOutcome, outcome);
            TuiKitInstruments.MarkOk(span);
            TuiKitInstruments.Stop(span);
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
