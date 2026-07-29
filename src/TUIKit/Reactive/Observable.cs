namespace TUIKit.Reactive
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A thread-safe observable value cell for one-way data binding. Set <see cref="Value"/> and every
    /// subscriber is notified; bind it to a widget's setter with <see cref="Bind"/> so the UI follows
    /// the model without manual wiring. Notifications fire only when the value actually changes
    /// (compared with the default equality comparer), so redundant assignments are cheap.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public sealed class Observable<T>
    {
        private readonly object _Lock = new object();
        private readonly List<Action<T>> _Handlers = new List<Action<T>>();
        private T _Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="Observable{T}"/> class.
        /// </summary>
        /// <param name="initial">The initial value.</param>
        public Observable(T initial)
        {
            _Value = initial;
        }

        /// <summary>
        /// Gets or sets the current value. Setting to an equal value is a no-op and raises nothing.
        /// </summary>
        public T Value
        {
            get
            {
                lock (_Lock)
                {
                    return _Value;
                }
            }

            set
            {
                Action<T>[] handlers;
                lock (_Lock)
                {
                    if (EqualityComparer<T>.Default.Equals(_Value, value))
                        return;

                    _Value = value;
                    handlers = _Handlers.ToArray();
                }

                for (int i = 0; i < handlers.Length; i++)
                    handlers[i](value);
            }
        }

        /// <summary>
        /// Gets the number of active subscribers.
        /// </summary>
        public int SubscriberCount
        {
            get
            {
                lock (_Lock)
                {
                    return _Handlers.Count;
                }
            }
        }

        /// <summary>
        /// Subscribes to value changes. The handler runs on every subsequent change until the returned
        /// handle is disposed. The current value is not delivered immediately; read <see cref="Value"/>
        /// for that.
        /// </summary>
        /// <param name="handler">The change handler. Must not be null.</param>
        /// <returns>A handle that detaches the handler when disposed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is null.</exception>
        public IDisposable Subscribe(Action<T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_Lock)
            {
                _Handlers.Add(handler);
            }

            return new Subscription(() =>
            {
                lock (_Lock)
                {
                    _Handlers.Remove(handler);
                }
            });
        }

        /// <summary>
        /// Binds this observable to a setter: the setter runs once with the current value and again on
        /// every change. Useful for pushing a model value into a widget.
        /// </summary>
        /// <param name="setter">The target setter. Must not be null.</param>
        /// <returns>A handle that stops the binding when disposed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="setter"/> is null.</exception>
        public IDisposable Bind(Action<T> setter)
        {
            if (setter == null)
                throw new ArgumentNullException(nameof(setter));

            setter(Value);
            return Subscribe(setter);
        }
    }
}
