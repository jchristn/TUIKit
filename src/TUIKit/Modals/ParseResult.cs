namespace TUIKit.Modals
{
    using System;

    /// <summary>
    /// The outcome of parsing typed text into a list item for <see cref="ListEditorModal{T}"/>. A
    /// success carries the parsed <see cref="Value"/>; a failure carries a human-readable
    /// <see cref="Error"/> describing why the text was rejected. This is a named type, used instead of
    /// a tuple, so the add path can reject bad input with a message without allocating or returning an
    /// unnamed pair. Instances are immutable value types and safe to share across threads.
    /// </summary>
    /// <typeparam name="T">The parsed item type.</typeparam>
    public readonly struct ParseResult<T>
    {
        private readonly bool _Ok;
        private readonly T _Value;
        private readonly string? _Error;

        private ParseResult(bool ok, T value, string? error)
        {
            _Ok = ok;
            _Value = value;
            _Error = error;
        }

        /// <summary>
        /// Gets a value indicating whether the parse succeeded. When <c>true</c>, <see cref="Value"/>
        /// holds the parsed item and <see cref="Error"/> is null; when <c>false</c>, <see cref="Error"/>
        /// holds the reason and <see cref="Value"/> is the type default.
        /// </summary>
        public bool Ok
        {
            get { return _Ok; }
        }

        /// <summary>
        /// Gets the parsed value. Meaningful only when <see cref="Ok"/> is <c>true</c>; otherwise the
        /// default value of <typeparamref name="T"/>.
        /// </summary>
        public T Value
        {
            get { return _Value; }
        }

        /// <summary>
        /// Gets the failure message, or null when <see cref="Ok"/> is <c>true</c>.
        /// </summary>
        public string? Error
        {
            get { return _Error; }
        }

        /// <summary>
        /// Creates a successful result carrying the parsed value.
        /// </summary>
        /// <param name="value">The parsed value. May be null when <typeparamref name="T"/> is a reference type.</param>
        /// <returns>A result whose <see cref="Ok"/> is <c>true</c>.</returns>
        public static ParseResult<T> Success(T value)
        {
            return new ParseResult<T>(true, value, null);
        }

        /// <summary>
        /// Creates a failed result carrying an error message.
        /// </summary>
        /// <param name="error">The reason the text was rejected. Must not be null or empty.</param>
        /// <returns>A result whose <see cref="Ok"/> is <c>false</c>.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="error"/> is null or empty.</exception>
        public static ParseResult<T> Failure(string error)
        {
            if (string.IsNullOrEmpty(error))
                throw new ArgumentException("A failure message is required.", nameof(error));

            return new ParseResult<T>(false, default!, error);
        }
    }
}
