namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for the typeahead surfaces: <see cref="PrefixSuggestionProvider"/> and
    /// <see cref="AutocompleteOverlay"/>.
    /// </summary>
    public static class AutocompleteSuite
    {
        /// <summary>
        /// Builds the autocomplete suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Autocomplete",
                displayName: "Autocomplete (provider / overlay)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Autocomplete", "PrefixRanks", "Prefix matches come before contains matches",
                        _ =>
                        {
                            PrefixSuggestionProvider provider = new PrefixSuggestionProvider(new[] { "open", "reopen", "close" });
                            IReadOnlyList<string> results = provider.Suggest("open");
                            Check.Equal(2, results.Count, "open and reopen match");
                            Check.Equal("open", results[0], "prefix match first");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Autocomplete", "AcceptRaises", "Enter accepts the highlighted suggestion",
                        _ =>
                        {
                            AutocompleteOverlay overlay = new AutocompleteOverlay(new PrefixSuggestionProvider(new[] { "alpha", "alnum", "beta" }));
                            overlay.SetInput("al");
                            Check.True(overlay.IsVisible, "overlay visible with matches");

                            string? accepted = null;
                            overlay.Accepted += a => accepted = a;
                            overlay.HandleKey(KeyEvent.Special(KeyCode.Down)); // move to second
                            overlay.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.Equal("alnum", accepted, "second suggestion accepted");
                            Check.False(overlay.IsVisible, "overlay hidden after accept");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Autocomplete", "EscapeDismisses", "Escape dismisses the overlay",
                        _ =>
                        {
                            AutocompleteOverlay overlay = new AutocompleteOverlay(new PrefixSuggestionProvider(new[] { "one", "two" }));
                            overlay.SetInput("");
                            bool dismissed = false;
                            overlay.Dismissed += () => dismissed = true;
                            overlay.HandleKey(KeyEvent.Special(KeyCode.Escape));
                            Check.True(dismissed, "dismissed raised");
                            Check.False(overlay.IsVisible, "hidden after escape");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Autocomplete", "EmptyHides", "No matches means the overlay is not visible",
                        _ =>
                        {
                            AutocompleteOverlay overlay = new AutocompleteOverlay(new PrefixSuggestionProvider(new[] { "apple" }));
                            overlay.SetInput("zzz");
                            Check.False(overlay.IsVisible, "no matches -> hidden");
                            Check.False(overlay.HandleKey(KeyEvent.Special(KeyCode.Down)), "keys not consumed when hidden");

                            CellBuffer buffer = new CellBuffer(20, 5);
                            overlay.RenderAt(new BufferSurface(buffer), 0, 0);
                            Check.Equal(string.Empty, Snapshot.ToText(buffer).Trim(), "nothing drawn when hidden");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Autocomplete", "RendersBelowOrAbove", "The overlay renders suggestions near the caret",
                        _ =>
                        {
                            AutocompleteOverlay overlay = new AutocompleteOverlay(new PrefixSuggestionProvider(new[] { "open", "close" }));
                            overlay.SetInput("");
                            CellBuffer buffer = new CellBuffer(20, 6);
                            overlay.RenderAt(new BufferSurface(buffer), 0, 0);
                            string text = Snapshot.ToText(buffer);
                            Check.True(text.Contains("open"), "suggestion rendered");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Autocomplete", "AsyncMatches", "The async provider returns the same results",
                        async ct =>
                        {
                            PrefixSuggestionProvider provider = new PrefixSuggestionProvider(new[] { "go", "gone" });
                            IReadOnlyList<string> results = await provider.SuggestAsync("go", CancellationToken.None).ConfigureAwait(false);
                            Check.Equal(2, results.Count, "async matches");
                        }),

                    new TestCaseDescriptor("Autocomplete", "Guards", "Provider and overlay reject bad arguments",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new PrefixSuggestionProvider(null!), "null candidates");
                            Check.Throws<ArgumentNullException>(() => new AutocompleteOverlay(null!), "null provider");
                            AutocompleteOverlay overlay = new AutocompleteOverlay(new PrefixSuggestionProvider(new[] { "x" }));
                            Check.Throws<ArgumentNullException>(() => overlay.SetInput(null!), "null input");
                            Check.Throws<ArgumentOutOfRangeException>(() => overlay.MaxRows = 0, "zero max rows");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
