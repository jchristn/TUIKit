namespace TUIKit.Hosting
{
    using TUIKit.Content;

    internal readonly struct PaneCursor
    {
        internal PaneCursor(Pane pane, long id)
        {
            Pane = pane;
            Id = id;
        }

        internal Pane Pane { get; }

        internal long Id { get; }
    }
}
