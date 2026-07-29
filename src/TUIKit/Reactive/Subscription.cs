namespace TUIKit.Reactive
{
    using System;

    /// <summary>
    /// A disposable handle returned by <see cref="Observable{T}.Subscribe"/>. Disposing it detaches
    /// the handler. Disposing more than once is safe.
    /// </summary>
    internal sealed class Subscription : IDisposable
    {
        private Action? _Unsubscribe;

        internal Subscription(Action unsubscribe)
        {
            _Unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            Action? action = _Unsubscribe;
            _Unsubscribe = null;
            action?.Invoke();
        }
    }
}
