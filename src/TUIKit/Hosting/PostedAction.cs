namespace TUIKit.Hosting
{
    using System;
    using System.Diagnostics;

    // An action queued with TuiApplication.Post, carrying the trace context of the posting thread and
    // the time it was enqueued so the loop thread can parent its span and measure queue wait.
    internal sealed class PostedAction
    {
        internal PostedAction(Action action, ActivityContext parentContext, long enqueuedTimestamp)
        {
            Action = action;
            ParentContext = parentContext;
            EnqueuedTimestamp = enqueuedTimestamp;
        }

        internal Action Action { get; }

        internal ActivityContext ParentContext { get; }

        internal long EnqueuedTimestamp { get; }
    }
}
