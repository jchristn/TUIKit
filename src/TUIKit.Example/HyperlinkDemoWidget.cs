namespace TUIKit.Example
{
    using System;
    using System.IO;
    using TUIKit;
    using TUIKit.Widgets;

    internal sealed class HyperlinkDemoWidget : IWidget
    {
        private readonly Uri _FileUri = new Uri(Path.GetFullPath("README.md"));

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(available.Height, 9));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            surface.DrawStyledText(0, 0, Text.From("URL and file hyperlinks").Bold());
            surface.DrawStyledText(0, 2, Text.From("URL:  ").Append(StyledText.Link("Visit TUIKit on GitHub", new Uri("https://github.com/jchristn/TUIKit"))));
            surface.DrawStyledText(0, 3, Text.From("File: ").Append(StyledText.Link("Open local README.md", _FileUri)));
            surface.DrawStyledText(0, 5, Text.From("Click a link to open it (mouse capture on)."));
            surface.DrawStyledText(0, 6, Text.From("Native links also use your terminal's modifier."));
            surface.DrawStyledText(0, 7, Text.From("Normal clicks open files on this machine."));
            surface.DrawStyledText(0, 8, Text.From("File URI: " + _FileUri.AbsoluteUri).Dim());
        }
    }
}
