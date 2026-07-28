namespace TUIKit.Content
{
    using System;
    using System.Text;
    using TUIKit.Terminal;

    /// <summary>
    /// Builds OSC 52 clipboard-set sequences. OSC 52 works over SSH, so copy functions even on a
    /// remote session where the local clipboard is otherwise unreachable. All members are thread-safe.
    /// </summary>
    public static class ClipboardWriter
    {
        /// <summary>
        /// Encodes text as an OSC 52 clipboard-set escape sequence.
        /// </summary>
        /// <param name="text">The text to place on the clipboard. Must not be null.</param>
        /// <returns>The escape sequence for a backend to write.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public static string BuildSequence(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            return Ansi.SetClipboard(payload);
        }
    }
}
