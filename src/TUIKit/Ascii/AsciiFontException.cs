namespace TUIKit.Ascii
{
    using System;

    /// <summary>
    /// The exception thrown for ASCII-art font domain errors: a malformed FIGlet font, a lookup for a
    /// font name that is not registered, or a duplicate registration. Carries a contextual message
    /// describing the offending font or input.
    /// </summary>
    public class AsciiFontException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AsciiFontException"/> class.
        /// </summary>
        public AsciiFontException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsciiFontException"/> class with a message.
        /// </summary>
        /// <param name="message">The contextual error message.</param>
        public AsciiFontException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsciiFontException"/> class with a message and
        /// an inner exception.
        /// </summary>
        /// <param name="message">The contextual error message.</param>
        /// <param name="innerException">The underlying cause.</param>
        public AsciiFontException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
