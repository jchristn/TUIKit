namespace TUIKit.Widgets
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Supplies ranked completion suggestions for a piece of input, used by
    /// <see cref="AutocompleteOverlay"/>. Both a synchronous and an asynchronous method are provided so
    /// a provider can either compute suggestions in memory or fetch them from a slower source.
    /// </summary>
    public interface ISuggestionProvider
    {
        /// <summary>
        /// Returns suggestions for the supplied input, best match first.
        /// </summary>
        /// <param name="input">The current input. Never null.</param>
        /// <returns>The suggestions, best first. Never null; empty when there are none.</returns>
        IReadOnlyList<string> Suggest(string input);

        /// <summary>
        /// Returns suggestions for the supplied input asynchronously, best match first.
        /// </summary>
        /// <param name="input">The current input. Never null.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A task yielding the suggestions, best first. Never null; empty when there are none.</returns>
        Task<IReadOnlyList<string>> SuggestAsync(string input, CancellationToken cancellationToken);
    }
}
