namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.8 item C4: <see cref="FuzzyList{T}.MultiSelect"/>, <see cref="FuzzyList{T}.CheckedItems"/>,
    /// <see cref="FuzzyList{T}.IsChecked"/>, <see cref="FuzzyList{T}.SetChecked"/>, <see cref="FuzzyList{T}.CheckedChanged"/>,
    /// the glyphs, and the comparer overload.
    /// </summary>
    public static class FuzzyListMultiSelectSuite
    {
        private const string _Id = "FuzzyListMultiSelect";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "FuzzyList Multi-select",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "SpaceTypesByDefault", "Without MultiSelect, Space types into the query and Ctrl+A is not consumed",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "a b" });
                            list.HandleKey(KeyEvent.Char(' '));
                            Check.Equal(" ", list.Query, "Space typed");
                            Check.False(list.HandleKey(KeyEvent.Char('a', KeyModifiers.Ctrl)), "Ctrl+A not consumed");
                            Check.False(Snapshot.RenderWidget(list, 10, 2).Contains("[ ]"), "no check column");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "SpaceToggles", "Space toggles the selected item's check and raises CheckedChanged once",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "one", "two" }) { MultiSelect = true };
                            int changes = 0;
                            list.CheckedChanged += (s, e) => changes++;
                            Check.True(list.HandleKey(KeyEvent.Char(' ')), "consumed");
                            Check.Equal(string.Empty, list.Query, "not typed");
                            Check.True(list.IsChecked("one"), "checked");
                            Check.Equal(1, changes, "one change");
                            string screen = Snapshot.RenderWidget(list, 12, 3);
                            Check.True(screen.Contains("[x] one") && screen.Contains("[ ] two"), "glyphs: " + screen);
                            list.HandleKey(KeyEvent.Char(' '));
                            Check.False(list.IsChecked("one"), "unchecked");
                            Check.Equal(2, changes, "second change");
                            list.CheckedGlyph = "*";
                            list.UncheckedGlyph = "-";
                            Check.True(Snapshot.RenderWidget(list, 12, 3).Contains("- one"), "custom glyphs");
                            Check.Throws<ArgumentNullException>(() => list.CheckedGlyph = null!, "null glyph");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "CtrlA", "Ctrl+A checks every visible enabled item, then unchecks them",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "ab", "ac", "zz", "ad" }) { MultiSelect = true };
                            list.IsEnabled = s => s != "ad";
                            list.Query = "a";
                            int changes = 0;
                            list.CheckedChanged += (s, e) => changes++;
                            Check.True(list.HandleKey(KeyEvent.Char('a', KeyModifiers.Ctrl)), "consumed");
                            Check.Equal("ab,ac", string.Join(",", list.CheckedItems), "visible enabled checked");
                            Check.Equal(1, changes, "one event for the batch");
                            list.HandleKey(KeyEvent.Char('a', KeyModifiers.Ctrl));
                            Check.Equal(0, list.CheckedItems.Count, "unchecked when all were checked");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "SurviveQuery", "Checks survive a query change and CheckedItems keeps original order",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "red", "green", "blue" }) { MultiSelect = true };
                            list.Query = "blu";
                            list.HandleKey(KeyEvent.Char(' '));
                            list.Query = "red";
                            list.HandleKey(KeyEvent.Char(' '));
                            list.Query = string.Empty;
                            Check.Equal("red,blue", string.Join(",", list.CheckedItems), "original order");
                            list.SetChecked("green", true);
                            Check.Equal("red,green,blue", string.Join(",", list.CheckedItems), "programmatic check");
                            int changes = 0;
                            list.CheckedChanged += (s, e) => changes++;
                            list.SetChecked("green", true);
                            Check.Equal(0, changes, "no event for no change");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "DisabledRefused", "Space does not check a disabled item",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "x" }) { MultiSelect = true, IsEnabled = s => false };
                            list.HandleKey(KeyEvent.Char(' '));
                            Check.Equal(0, list.CheckedItems.Count, "refused");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Comparer", "The comparer overload gives identity to items without value equality; null comparer is rejected",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new[] { "Alpha" }, null, StringComparer.OrdinalIgnoreCase) { MultiSelect = true };
                            list.SetChecked("ALPHA", true);
                            Check.True(list.IsChecked("alpha"), "case-insensitive identity");
                            Check.Throws<ArgumentNullException>(() => new FuzzyList<string>(new[] { "a" }, null, null!), "null comparer");
                            Check.Throws<ArgumentNullException>(() => new FuzzyList<int>(new[] { 1 }, null, EqualityComparer<int>.Default), "display required for non-string");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ProviderKeepsChecks", "Checks survive a provider refresh",
                        _ =>
                        {
                            FuzzyList<string> list = new FuzzyList<string>(new string[0]) { MultiSelect = true };
                            list.ItemsProvider = q => new[] { "keep", "other" };
                            list.HandleKey(KeyEvent.Char(' '));
                            list.Query = "o";
                            Check.True(list.IsChecked("keep"), "kept across refresh");
                            return Task.CompletedTask;
                        }),
                });
        }
    }
}
