namespace TUIKit.Input
{
    /// <summary>
    /// The scope in which a key binding is active.
    /// </summary>
    public enum CommandScope
    {
        /// <summary>Active regardless of which pane has focus.</summary>
        Global = 0,

        /// <summary>Active only while a specific focus context is current. Overrides a global binding for the same chord.</summary>
        FocusContext = 1
    }
}
