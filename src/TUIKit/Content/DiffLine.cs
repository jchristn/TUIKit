namespace TUIKit.Content
{
    /// <summary>
    /// One rendered diff line: its kind, its text without the <c>+</c>, <c>-</c>, or space marker (header and
    /// metadata lines keep their whole text), and its old and new line numbers (0 when the line has none).
    /// </summary>
    internal readonly struct DiffLine
    {
        internal DiffLineKind Kind { get; }

        internal string Text { get; }

        internal int OldNumber { get; }

        internal int NewNumber { get; }

        internal DiffLine(DiffLineKind kind, string text)
            : this(kind, text, 0, 0)
        {
        }

        internal DiffLine(DiffLineKind kind, string text, int oldNumber, int newNumber)
        {
            Kind = kind;
            Text = text;
            OldNumber = oldNumber;
            NewNumber = newNumber;
        }

        internal bool HasMarker
        {
            get { return Kind == DiffLineKind.Context || Kind == DiffLineKind.Added || Kind == DiffLineKind.Removed; }
        }

        internal string Drawn
        {
            get
            {
                switch (Kind)
                {
                    case DiffLineKind.Added:
                        return "+" + Text;
                    case DiffLineKind.Removed:
                        return "-" + Text;
                    case DiffLineKind.Context:
                        return " " + Text;
                    default:
                        return Text;
                }
            }
        }
    }
}
