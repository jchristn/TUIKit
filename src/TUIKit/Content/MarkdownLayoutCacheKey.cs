namespace TUIKit.Content
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using TUIKit;

    // One MarkdownLayoutCache entry: the Markdown text (ordinal equality) and the options instance (reference
    // equality) identify it; Rows holds the cached layout and is not part of equality.
    internal sealed class MarkdownLayoutCacheKey : IEquatable<MarkdownLayoutCacheKey>
    {
        internal MarkdownLayoutCacheKey(string markdown, MarkdownRenderOptions options)
        {
            Markdown = markdown;
            Options = options;
        }

        internal string Markdown { get; }

        internal MarkdownRenderOptions Options { get; }

        internal IReadOnlyList<StyledText>? Rows { get; set; }

        public bool Equals(MarkdownLayoutCacheKey? other)
        {
            return other != null
                && ReferenceEquals(Options, other.Options)
                && string.Equals(Markdown, other.Markdown, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as MarkdownLayoutCacheKey);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(Markdown) * 397) ^ RuntimeHelpers.GetHashCode(Options);
            }
        }
    }
}
