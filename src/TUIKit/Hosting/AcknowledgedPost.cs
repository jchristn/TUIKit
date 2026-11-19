namespace TUIKit.Hosting
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    // Passed through the existing Action<Action> marshaller as Run. A host that
    // closes its queue can acknowledge discard without invoking the UI action.
    internal sealed class AcknowledgedPost
    {
        private readonly TaskCompletionSource<bool> _Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private Action? _Action;

        internal AcknowledgedPost(Action action) => _Action = action;

        internal Task<bool> Completion => _Completion.Task;

        internal void Run()
        {
            Action? action = Interlocked.Exchange(ref _Action, null);
            if (action == null)
                return;

            try
            {
                action();
                _Completion.TrySetResult(true);
            }
            catch (Exception ex)
            {
                _Completion.TrySetException(ex);
            }
        }

        internal void Discard()
        {
            if (Interlocked.Exchange(ref _Action, null) != null)
                _Completion.TrySetResult(false);
        }
    }
}
