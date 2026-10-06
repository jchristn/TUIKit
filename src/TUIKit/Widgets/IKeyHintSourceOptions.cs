namespace TUIKit.Widgets
{
    using TUIKit.Input;

    /// <summary>
    /// Optional companion to <see cref="IKeyHintSource"/> that changes how <see cref="KeyHintResolver"/>
    /// merges a container's hints with the focus path: whether its own hints come before its children's,
    /// and whether it replaces the sources outside it. Read on the UI thread.
    /// </summary>
    public interface IKeyHintSourceOptions
    {
        /// <summary>
        /// Gets where this source's hints go relative to the sources inside it on the focus path.
        /// <see cref="KeyHintOrder.InnerFirst"/> is the default resolver order.
        /// </summary>
        KeyHintOrder HintOrder { get; }

        /// <summary>
        /// Gets a value indicating whether the sources outside this one (its ancestors on the focus path)
        /// are skipped. Application hints and command hints are still added. When several sources on the
        /// path are exclusive, the innermost one decides.
        /// </summary>
        bool Exclusive { get; }
    }
}
