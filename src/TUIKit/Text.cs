namespace TUIKit
{
    /// <summary>
    /// Entry point for the fluent styled-text builder. <c>Text.From("hi").Bold().Red()</c> creates
    /// styled text without constructing spans by hand. All members are thread-safe.
    /// </summary>
    public static class Text
    {
        /// <summary>
        /// Gets empty styled text.
        /// </summary>
        public static StyledText Empty
        {
            get { return StyledText.Empty; }
        }

        /// <summary>
        /// Creates styled text from a string using the default style.
        /// </summary>
        /// <param name="value">The text. Must not be null.</param>
        /// <returns>The styled text.</returns>
        public static StyledText From(string value)
        {
            return StyledText.From(value);
        }

        /// <summary>
        /// Creates styled text from a string using the supplied style.
        /// </summary>
        /// <param name="value">The text. Must not be null.</param>
        /// <param name="style">The style to apply.</param>
        /// <returns>The styled text.</returns>
        public static StyledText From(string value, CellStyle style)
        {
            return StyledText.From(value, style);
        }
    }
}
