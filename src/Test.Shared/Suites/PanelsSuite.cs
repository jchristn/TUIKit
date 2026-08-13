namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for status/streaming panel widgets: <see cref="DefinitionList"/> and
    /// <see cref="ActivityIndicator"/>.
    /// </summary>
    public static class PanelsSuite
    {
        /// <summary>
        /// Builds the panels suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Panels",
                displayName: "Panels (DefinitionList / ActivityIndicator)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Panels", "DefinitionSetUpdate", "Setting the same label updates in place",
                        _ =>
                        {
                            DefinitionList list = new DefinitionList();
                            list.Set("Status", "idle");
                            list.Set("Tasks", "0/0");
                            list.Set("Status", "running");
                            Check.Equal(2, list.Rows.Count, "no duplicate row");
                            Check.Equal("running", list.Rows[0].Value, "value updated in place");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Panels", "DefinitionSection", "Sections and removal work",
                        _ =>
                        {
                            DefinitionList list = new DefinitionList();
                            list.AddSection("Session");
                            list.Set("Turns", "3");
                            Check.True(list.Rows[0].IsSection, "first row is a section");
                            Check.True(list.Remove("Turns"), "row removed");
                            Check.False(list.Remove("Turns"), "second removal reports false");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Panels", "DefinitionRenders", "Label and value render on a row",
                        _ =>
                        {
                            DefinitionList list = new DefinitionList();
                            list.Set("Effort", "high");
                            string text = TUIKit.Testing.Snapshot.RenderWidget(list, 24, 2);
                            Check.True(text.Contains("Effort"), "label rendered");
                            Check.True(text.Contains("high"), "value rendered");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Panels", "DefinitionGuards", "DefinitionList rejects null arguments",
                        _ =>
                        {
                            DefinitionList list = new DefinitionList();
                            Check.Throws<ArgumentNullException>(() => list.Set(null!, "v"), "null label");
                            Check.Throws<ArgumentNullException>(() => list.AddSection(null!), "null section");
                            Check.Throws<ArgumentNullException>(() => list.Remove(null!), "null remove");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Panels", "ActivityAdvances", "Ticking advances the spinner and rotates phrases",
                        _ =>
                        {
                            ActivityIndicator indicator = new ActivityIndicator();
                            indicator.Phrases = new[] { "Thinking", "Working" };
                            indicator.PhraseIntervalTicks = 2;
                            string first = indicator.CurrentFrame;
                            indicator.Tick();
                            Check.False(first == indicator.CurrentFrame, "spinner advanced");
                            Check.Equal("Thinking", indicator.CurrentPhrase, "first phrase before interval");
                            indicator.Tick(); // tick = 2 -> phrase index 1
                            Check.Equal("Working", indicator.CurrentPhrase, "phrase rotated after interval");
                            Check.True(indicator.CurrentLine.Contains("Working"), "line includes phrase");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Panels", "ActivityGuards", "ActivityIndicator rejects bad configuration",
                        _ =>
                        {
                            ActivityIndicator indicator = new ActivityIndicator();
                            Check.Throws<ArgumentNullException>(() => indicator.Frames = null!, "null frames");
                            Check.Throws<ArgumentException>(() => indicator.Frames = Array.Empty<string>(), "empty frames");
                            Check.Throws<ArgumentNullException>(() => indicator.Phrases = null!, "null phrases");
                            Check.Throws<ArgumentOutOfRangeException>(() => indicator.PhraseIntervalTicks = 0, "zero interval");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
