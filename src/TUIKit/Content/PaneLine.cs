namespace TUIKit.Content
{
    using TUIKit;

    /// <summary>
    /// A single committed line of pane content, identified so it can be updated in place after it is
    /// on screen. Mutated only under the owning pane's lock.
    /// </summary>
    internal sealed class PaneLine
    {
        internal long Id { get; }

        internal StyledText Content { get; set; }

        internal PaneLine(long id, StyledText content)
        {
            Id = id;
            Content = content;
        }
    }
}
