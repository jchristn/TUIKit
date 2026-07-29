namespace TUIKit.Content
{
    /// <summary>
    /// A single line of a computed diff: its kind and text.
    /// </summary>
    internal readonly struct DiffLine
    {
        internal DiffLineKind Kind { get; }

        internal string Text { get; }

        internal DiffLine(DiffLineKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }
    }
}
