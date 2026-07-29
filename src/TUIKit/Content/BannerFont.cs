namespace TUIKit.Content
{
    using System.Collections.Generic;

    /// <summary>
    /// A built-in 5×5 block font for <see cref="Banner"/>. Each glyph is five rows of five columns,
    /// with <c>#</c> marking ink and space marking gaps. Covers A–Z, 0–9, and a few punctuation
    /// marks; unknown characters render as blank. The font is embedded so banners need no external
    /// FIGlet font files and look identical on every platform.
    /// </summary>
    internal static class BannerFont
    {
        internal const int Rows = 5;

        private static readonly string[] _Blank = { "     ", "     ", "     ", "     ", "     " };

        private static readonly Dictionary<char, string[]> _Glyphs = Build();

        internal static string[] Glyph(char c)
        {
            char upper = char.ToUpperInvariant(c);
            if (_Glyphs.TryGetValue(upper, out string[]? glyph))
                return glyph;

            return _Blank;
        }

        private static Dictionary<char, string[]> Build()
        {
            Dictionary<char, string[]> g = new Dictionary<char, string[]>
            {
                { ' ', _Blank },
                { 'A', new[] { " ### ", "#   #", "#####", "#   #", "#   #" } },
                { 'B', new[] { "#### ", "#   #", "#### ", "#   #", "#### " } },
                { 'C', new[] { " ####", "#    ", "#    ", "#    ", " ####" } },
                { 'D', new[] { "#### ", "#   #", "#   #", "#   #", "#### " } },
                { 'E', new[] { "#####", "#    ", "#### ", "#    ", "#####" } },
                { 'F', new[] { "#####", "#    ", "#### ", "#    ", "#    " } },
                { 'G', new[] { " ####", "#    ", "#  ##", "#   #", " ####" } },
                { 'H', new[] { "#   #", "#   #", "#####", "#   #", "#   #" } },
                { 'I', new[] { "#####", "  #  ", "  #  ", "  #  ", "#####" } },
                { 'J', new[] { "#####", "   # ", "   # ", "#  # ", " ##  " } },
                { 'K', new[] { "#   #", "#  # ", "###  ", "#  # ", "#   #" } },
                { 'L', new[] { "#    ", "#    ", "#    ", "#    ", "#####" } },
                { 'M', new[] { "#   #", "## ##", "# # #", "#   #", "#   #" } },
                { 'N', new[] { "#   #", "##  #", "# # #", "#  ##", "#   #" } },
                { 'O', new[] { " ### ", "#   #", "#   #", "#   #", " ### " } },
                { 'P', new[] { "#### ", "#   #", "#### ", "#    ", "#    " } },
                { 'Q', new[] { " ### ", "#   #", "# # #", "#  # ", " ## #" } },
                { 'R', new[] { "#### ", "#   #", "#### ", "#  # ", "#   #" } },
                { 'S', new[] { " ####", "#    ", " ### ", "    #", "#### " } },
                { 'T', new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  " } },
                { 'U', new[] { "#   #", "#   #", "#   #", "#   #", " ### " } },
                { 'V', new[] { "#   #", "#   #", "#   #", " # # ", "  #  " } },
                { 'W', new[] { "#   #", "#   #", "# # #", "## ##", "#   #" } },
                { 'X', new[] { "#   #", " # # ", "  #  ", " # # ", "#   #" } },
                { 'Y', new[] { "#   #", " # # ", "  #  ", "  #  ", "  #  " } },
                { 'Z', new[] { "#####", "   # ", "  #  ", " #   ", "#####" } },
                { '0', new[] { " ### ", "#  ##", "# # #", "##  #", " ### " } },
                { '1', new[] { "  #  ", " ##  ", "  #  ", "  #  ", "#####" } },
                { '2', new[] { " ### ", "#   #", "  ## ", " #   ", "#####" } },
                { '3', new[] { "#### ", "    #", " ### ", "    #", "#### " } },
                { '4', new[] { "#  # ", "#  # ", "#####", "   # ", "   # " } },
                { '5', new[] { "#####", "#    ", "#### ", "    #", "#### " } },
                { '6', new[] { " ####", "#    ", "#### ", "#   #", " ### " } },
                { '7', new[] { "#####", "   # ", "  #  ", " #   ", " #   " } },
                { '8', new[] { " ### ", "#   #", " ### ", "#   #", " ### " } },
                { '9', new[] { " ### ", "#   #", " ####", "    #", "#### " } },
                { '!', new[] { "  #  ", "  #  ", "  #  ", "     ", "  #  " } },
                { '?', new[] { " ### ", "#   #", "  ## ", "     ", "  #  " } },
                { '.', new[] { "     ", "     ", "     ", "     ", "  #  " } },
                { ',', new[] { "     ", "     ", "     ", "  #  ", " #   " } },
                { '-', new[] { "     ", "     ", "#####", "     ", "     " } },
                { ':', new[] { "     ", "  #  ", "     ", "  #  ", "     " } }
            };

            return g;
        }
    }
}
