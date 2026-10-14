namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Widgets;

    // The tour's "Markdown in your own widgets" page: a chat-style message list that draws each bubble from one
    // MarkdownLayoutCache, so every message is laid out once per width instead of once per frame. The status
    // line shows how many layouts the cache holds and how many lookups it has answered.
    internal sealed class MessageListWidget : IWidget, IFocusable, IMouseAware, IKeyHintSource
    {
        private static readonly string[] _Extra =
        {
            "Pinned the flaky test: `NetworkSuite.Retry` now runs alone.",
            "| Step | Time |\n|---|--:|\n| restore | 4.1s |\n| build | 9.8s |\n| test | 21.0s |",
            "- [x] cache warmed\n- [ ] canary at 5%",
            "> Ship it after the canary holds for an hour.",
        };

        private readonly MarkdownLayoutCache _Cache = new MarkdownLayoutCache();
        private readonly MarkdownRenderOptions _Options = new MarkdownRenderOptions();
        private readonly List<ChatMessage> _Messages = new List<ChatMessage>();
        private int _ScrollOffset;
        private int _LastTotal;
        private int _LastHeight;
        private int _Lookups;
        private int _Extras;

        internal MessageListWidget()
        {
            _Options.Table.Glyphs = MarkdownTableGlyphs.Rounded;
            _Messages.Add(new ChatMessage("you", "Why is **CI** slow today?"));
            _Messages.Add(new ChatMessage("agent", "Two suites dominate:\n\n| Suite | Time |\n|---|--:|\n| network | 11.8s |\n| integration | 42.0s |"));
            _Messages.Add(new ChatMessage("you", "Can we cache the integration fixtures?"));
            _Messages.Add(new ChatMessage("agent", "Yes. Options:\n\n1. Snapshot the database once per run\n2. Reuse containers between jobs\n\n> Option 1 is a one-line change."));
        }

        public bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Up)
            {
                _ScrollOffset = Math.Max(0, Math.Min(_ScrollOffset, Math.Max(0, _LastTotal - _LastHeight)) - 1);
                return true;
            }

            if (key.Code == KeyCode.Down)
            {
                int maxOffset = Math.Max(0, _LastTotal - _LastHeight);
                _ScrollOffset = Math.Min(maxOffset, Math.Min(_ScrollOffset, maxOffset) + 1);
                return true;
            }

            if (key.Code == KeyCode.Character && key.Modifiers == KeyModifiers.None && key.Rune == 'a')
            {
                _Messages.Add(new ChatMessage(_Extras % 2 == 0 ? "agent" : "you", _Extra[_Extras % _Extra.Length]));
                _Extras++;
                _ScrollOffset = int.MaxValue;
                return true;
            }

            return false;
        }

        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            return new[] { new KeyHint("A", "Add message", 5), new KeyHint("Up/Down", "Scroll") };
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            if (mouse.Button == MouseButton.WheelUp)
                return HandleKey(KeyEvent.Special(KeyCode.Up));
            if (mouse.Button == MouseButton.WheelDown)
                return HandleKey(KeyEvent.Special(KeyCode.Down));

            return false;
        }

        public Size Measure(Size available)
        {
            return available;
        }

        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width < 4 || height < 2)
                return;

            List<StyledText> rows = new List<StyledText>();
            CellStyle you = CellStyle.Default.WithForeground(Color.FromPalette(6)).WithAttribute(CellAttributes.Bold, true);
            CellStyle agent = CellStyle.Default.WithForeground(Color.FromPalette(5)).WithAttribute(CellAttributes.Bold, true);
            foreach (ChatMessage message in _Messages)
            {
                rows.Add(new StyledText(new[] { new StyledSpan(message.Author, message.Author == "you" ? you : agent) }));
                _Lookups++;
                foreach (StyledText row in _Cache.GetRows(message.Markdown, width - 2, _Options))
                    rows.Add(new StyledText(new[] { new StyledSpan("  ", CellStyle.Default) }).Append(row));

                rows.Add(StyledText.Empty);
            }

            int listHeight = height - 1;
            _LastTotal = rows.Count;
            _LastHeight = listHeight;
            _ScrollOffset = Math.Max(0, Math.Min(_ScrollOffset, Math.Max(0, rows.Count - listHeight)));
            for (int y = 0; y < listHeight && _ScrollOffset + y < rows.Count; y++)
                surface.DrawStyledText(0, y, rows[_ScrollOffset + y], CellStyle.Default);

            string status = "cache: " + _Cache.Count.ToString(CultureInfo.InvariantCulture) + " layouts at width "
                + _Cache.Width.ToString(CultureInfo.InvariantCulture) + ", " + _Lookups.ToString(CultureInfo.InvariantCulture) + " lookups";
            surface.DrawText(0, height - 1, status, CellStyle.Default.WithForeground(Color.FromPalette(8)));
        }
    }
}
