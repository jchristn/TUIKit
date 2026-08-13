namespace TUIKit.Input
{
    /// <summary>
    /// Decides whether a key pressed in a multi-line editor submits the content or inserts a newline.
    /// Terminals disagree on how the "insert newline" gesture arrives — most send a bare carriage
    /// return for Enter and cannot distinguish Shift+Enter without the enhanced keyboard protocol, so
    /// the portable newline chord is Ctrl+J (line feed), while terminals that do report Shift+Enter
    /// send Enter with the Shift modifier. This resolver encodes both so every editor consumer stops
    /// reinventing the rule.
    /// </summary>
    /// <remarks>
    /// The default policy submits on a bare Enter and inserts a newline on Ctrl+J or Shift+Enter. Flip
    /// <see cref="EnterSubmits"/> to invert the primary gesture (newline on Enter, submit on Ctrl+J).
    /// Instances are lightweight and hold only configuration; they are safe to share across threads.
    /// </remarks>
    public sealed class SubmitKeyResolver
    {
        /// <summary>
        /// Gets or sets a value indicating whether a bare Enter submits (and Ctrl+J inserts a newline).
        /// When false the roles swap: a bare Enter inserts a newline and Ctrl+J submits. Defaults to true.
        /// </summary>
        public bool EnterSubmits { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether Shift+Enter inserts a newline on terminals that report
        /// it. Defaults to true.
        /// </summary>
        public bool ShiftEnterInsertsNewline { get; set; } = true;

        /// <summary>
        /// Resolves a key event to a submit decision.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns>
        /// <see cref="SubmitDecision.Submit"/>, <see cref="SubmitDecision.InsertNewline"/>, or
        /// <see cref="SubmitDecision.Ignore"/> when the key is not a submit-related gesture.
        /// </returns>
        public SubmitDecision Resolve(KeyEvent key)
        {
            if (key.Code == KeyCode.Enter)
            {
                if (ShiftEnterInsertsNewline && (key.Modifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                    return SubmitDecision.InsertNewline;

                return EnterSubmits ? SubmitDecision.Submit : SubmitDecision.InsertNewline;
            }

            if (key.Code == KeyCode.Character && key.Rune == 'j' && (key.Modifiers & KeyModifiers.Ctrl) == KeyModifiers.Ctrl)
                return EnterSubmits ? SubmitDecision.InsertNewline : SubmitDecision.Submit;

            return SubmitDecision.Ignore;
        }
    }
}
