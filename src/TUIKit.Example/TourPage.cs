namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Widgets;

    /// <summary>
    /// One page of the guided tour: a feature title, a short description, the live widget that
    /// demonstrates it, and the code snippet showing how to build it.
    /// </summary>
    internal sealed class TourPage
    {
        internal string Title { get; }

        internal string Description { get; }

        internal IWidget Demo { get; }

        internal IReadOnlyList<string> Code { get; }

        internal TourPage(string title, string description, IWidget demo, IReadOnlyList<string> code)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Demo = demo ?? throw new ArgumentNullException(nameof(demo));
            Code = code ?? throw new ArgumentNullException(nameof(code));
        }
    }
}
