namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A simple <see cref="ISuggestionProvider"/> over a fixed candidate list. It returns candidates
    /// whose text starts with the input (prefix matches first), then candidates that merely contain it,
    /// each case-insensitively. An empty input returns every candidate.
    /// </summary>
    public sealed class PrefixSuggestionProvider : ISuggestionProvider
    {
        private readonly List<string> _Candidates = new List<string>();

        /// <summary>
        /// Initializes a new instance of the <see cref="PrefixSuggestionProvider"/> class.
        /// </summary>
        /// <param name="candidates">The candidate strings. Must not be null; null entries are ignored.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="candidates"/> is null.</exception>
        public PrefixSuggestionProvider(IEnumerable<string> candidates)
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            foreach (string candidate in candidates)
            {
                if (candidate != null)
                    _Candidates.Add(candidate);
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> Suggest(string input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            List<string> prefix = new List<string>();
            List<string> contains = new List<string>();
            for (int i = 0; i < _Candidates.Count; i++)
            {
                string candidate = _Candidates[i];
                if (input.Length == 0)
                {
                    prefix.Add(candidate);
                    continue;
                }

                if (candidate.StartsWith(input, StringComparison.OrdinalIgnoreCase))
                    prefix.Add(candidate);
                else if (candidate.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0)
                    contains.Add(candidate);
            }

            prefix.AddRange(contains);
            return prefix;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> SuggestAsync(string input, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Suggest(input));
        }
    }
}
