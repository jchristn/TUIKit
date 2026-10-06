namespace TUIKit.Input
{
    /// <summary>
    /// Where a hint source's own hints go relative to the hints of the sources inside it on the focus
    /// path (see <see cref="Widgets.IKeyHintSourceOptions.HintOrder"/>).
    /// </summary>
    public enum KeyHintOrder
    {
        /// <summary>The inner (more focused) sources' hints come first. The default and the 1.4.0 order.</summary>
        InnerFirst = 0,

        /// <summary>This source's own hints come before those of the sources inside it.</summary>
        OwnFirst = 1
    }
}
