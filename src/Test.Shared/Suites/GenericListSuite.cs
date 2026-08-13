namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for the generic list widgets <see cref="ListView{T}"/> and <see cref="FuzzyList{T}"/>:
    /// binding a non-string item type through a display selector, reading the selection back as the
    /// original object, and the selector guard.
    /// </summary>
    public static class GenericListSuite
    {
        /// <summary>
        /// Builds the generic-list suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "GenericList",
                displayName: "Generic Lists (ListView<T> / FuzzyList<T>)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("GenericList", "ListViewSelectsObject", "ListView<T> returns the selected object, not a string",
                        _ =>
                        {
                            ListView<int> list = new ListView<int>(i => "#" + i);
                            list.SetItems(new[] { 10, 20, 30 });
                            list.SelectNext();
                            Check.Equal(20, list.SelectedItem, "selected item is the int");
                            string text = Snapshot.RenderWidget(list, 10, 3);
                            Check.True(text.Contains("#20"), "selector label rendered");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("GenericList", "FuzzyMatchesLabel", "FuzzyList<T> filters on the selector label and returns the object",
                        _ =>
                        {
                            FuzzyList<int> list = new FuzzyList<int>(new[] { 11, 22, 123 }, i => "n" + i);
                            list.Query = "12";
                            Check.Equal(1, list.MatchCount, "only n123 contains '1' then '2' as a subsequence");
                            Check.Equal(123, list.SelectedItem, "selected object returned");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("GenericList", "StringConvenience", "String lists need no selector",
                        _ =>
                        {
                            ListView<string> list = new ListView<string>();
                            list.SetItems(new[] { "a", "b" });
                            Check.Equal("a", list.SelectedItem, "string identity selector");
                            FuzzyList<string> fuzzy = new FuzzyList<string>(new[] { "abc" });
                            Check.Equal("abc", fuzzy.SelectedItem, "string fuzzy identity");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("GenericList", "SelectorGuard", "A non-string type requires a selector",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new ListView<int>(), "ListView<int> without selector");
                            Check.Throws<ArgumentNullException>(() => new FuzzyList<int>(new[] { 1 }), "FuzzyList<int> without selector");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
