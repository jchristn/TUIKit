namespace TUIKit.Terminal
{
    /// <summary>Controls OSC 8 output independently of color. Plain text remains readable when disabled.</summary>
    public enum HyperlinkMode
    {
        /// <summary>Use the detected terminal capability; disable for redirected output.</summary>
        Auto,
        /// <summary>Emit hyperlinks even when support cannot be detected, including explicit writer output.</summary>
        Always,
        /// <summary>Never emit hyperlink escapes.</summary>
        Never
    }
}
