namespace TUIKit.Input
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Finds URLs in text for optional auto-linkification. For safety in an agent harness — where the
    /// text may be model-generated — only an allowlisted set of schemes is matched, so a streamed
    /// response cannot turn <c>file://</c> or custom-scheme URLs into one-click actions. Auto-linkify
    /// is opt-in; application code can always create links explicitly regardless of scheme.
    /// </summary>
    public sealed class LinkScanner
    {
        private readonly List<string> _AllowedSchemes;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkScanner"/> class allowing <c>http</c>,
        /// <c>https</c>, and <c>mailto</c>.
        /// </summary>
        public LinkScanner()
            : this(new[] { "http://", "https://", "mailto:" })
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkScanner"/> class with explicit scheme
        /// prefixes.
        /// </summary>
        /// <param name="allowedSchemePrefixes">
        /// The scheme prefixes to match, such as <c>"https://"</c>. Must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when the collection is null.</exception>
        public LinkScanner(IEnumerable<string> allowedSchemePrefixes)
        {
            if (allowedSchemePrefixes == null)
                throw new ArgumentNullException(nameof(allowedSchemePrefixes));

            _AllowedSchemes = new List<string>(allowedSchemePrefixes);
        }

        /// <summary>
        /// Scans text and returns the allowlisted URLs found, in order.
        /// </summary>
        /// <param name="text">The text to scan. Must not be null.</param>
        /// <returns>The matches. Never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        public IReadOnlyList<LinkMatch> Scan(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            List<LinkMatch> matches = new List<LinkMatch>();
            int i = 0;
            while (i < text.Length)
            {
                string? scheme = MatchScheme(text, i);
                if (scheme == null)
                {
                    i++;
                    continue;
                }

                int end = i + scheme.Length;
                while (end < text.Length && !IsTerminator(text[end]))
                    end++;

                while (end > i + scheme.Length && IsTrailingPunctuation(text[end - 1]))
                    end--;

                matches.Add(new LinkMatch(i, end - i, text.Substring(i, end - i)));
                i = end;
            }

            return matches;
        }

        private string? MatchScheme(string text, int index)
        {
            for (int s = 0; s < _AllowedSchemes.Count; s++)
            {
                string scheme = _AllowedSchemes[s];
                if (index + scheme.Length <= text.Length
                    && string.Compare(text, index, scheme, 0, scheme.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return scheme;
                }
            }

            return null;
        }

        private static bool IsTerminator(char c)
        {
            return char.IsWhiteSpace(c) || c == '<' || c == '>' || c == '"' || c == '`';
        }

        private static bool IsTrailingPunctuation(char c)
        {
            return c == '.' || c == ',' || c == ';' || c == ':' || c == ')' || c == ']' || c == '!' || c == '?';
        }
    }
}
