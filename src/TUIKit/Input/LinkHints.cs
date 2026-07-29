namespace TUIKit.Input
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Assigns short keyboard labels ("hints") to links so they can be followed without a mouse, the
    /// way browser hint extensions work. Hints are drawn next to each link; the user types a label to
    /// resolve the corresponding <see cref="Link"/>. Labels are generated a–z, then aa, ab, … for
    /// large sets. This complements OSC 8 hyperlinks for terminals or sessions without mouse support.
    /// </summary>
    public sealed class LinkHints
    {
        private readonly Dictionary<string, Link> _ByHint = new Dictionary<string, Link>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> _ByLinkId = new Dictionary<int, string>();

        private LinkHints()
        {
        }

        /// <summary>
        /// Gets the assigned hint labels mapped to their links.
        /// </summary>
        public IReadOnlyDictionary<string, Link> Hints
        {
            get { return _ByHint; }
        }

        /// <summary>
        /// Gets the number of assigned hints.
        /// </summary>
        public int Count
        {
            get { return _ByHint.Count; }
        }

        /// <summary>
        /// Assigns hints to a list of links, in order.
        /// </summary>
        /// <param name="links">The links to label. Must not be null.</param>
        /// <returns>A new hint set.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="links"/> is null.</exception>
        public static LinkHints ForLinks(IReadOnlyList<Link> links)
        {
            if (links == null)
                throw new ArgumentNullException(nameof(links));

            LinkHints hints = new LinkHints();
            for (int i = 0; i < links.Count; i++)
            {
                string label = Label(i);
                hints._ByHint[label] = links[i];
                hints._ByLinkId[links[i].Id] = label;
            }

            return hints;
        }

        /// <summary>
        /// Assigns hints to every link in a registry, in registration order.
        /// </summary>
        /// <param name="registry">The link registry. Must not be null.</param>
        /// <returns>A new hint set.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="registry"/> is null.</exception>
        public static LinkHints ForRegistry(LinkRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            return ForLinks(registry.Links);
        }

        /// <summary>
        /// Generates the label for the zero-based index (a–z, then aa, ab, …).
        /// </summary>
        /// <param name="index">The zero-based index. Must not be negative.</param>
        /// <returns>The label.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative.</exception>
        public static string Label(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            const string alphabet = "abcdefghijklmnopqrstuvwxyz";
            char[] buffer = new char[8];
            int position = buffer.Length;
            int value = index;
            do
            {
                position--;
                buffer[position] = alphabet[value % 26];
                value = value / 26 - 1;
            }
            while (value >= 0);

            return new string(buffer, position, buffer.Length - position);
        }

        /// <summary>
        /// Resolves a typed hint to its link.
        /// </summary>
        /// <param name="hint">The typed hint label. Must not be null.</param>
        /// <param name="link">When this method returns, the matching link or null.</param>
        /// <returns><c>true</c> when the hint matched; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="hint"/> is null.</exception>
        public bool TryResolve(string hint, out Link? link)
        {
            if (hint == null)
                throw new ArgumentNullException(nameof(hint));

            return _ByHint.TryGetValue(hint, out link);
        }

        /// <summary>
        /// Gets the hint label for a link id, or null when the link has no hint.
        /// </summary>
        /// <param name="linkId">The link id.</param>
        /// <returns>The hint label or null.</returns>
        public string? HintFor(int linkId)
        {
            return _ByLinkId.TryGetValue(linkId, out string? label) ? label : null;
        }
    }
}
