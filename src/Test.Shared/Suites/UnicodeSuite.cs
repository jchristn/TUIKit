namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit.Unicode;

    /// <summary>
    /// Touchstone suite covering Unicode column width (<see cref="TextWidth"/>) and grapheme
    /// cluster segmentation (<see cref="Graphemes"/>). Inputs use explicit code-point escapes so the
    /// suite is independent of source-file encoding.
    /// </summary>
    public static class UnicodeSuite
    {
        /// <summary>
        /// Builds the Unicode suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Unicode",
                displayName: "Unicode Width and Graphemes",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("Unicode", "ScalarWidths", "Scalar column widths",
                        _ =>
                        {
                            Check.Equal(1, TextWidth.CodePointWidth('A'), "ASCII 'A'");
                            Check.Equal(2, TextWidth.CodePointWidth(0x4E16), "CJK U+4E16");
                            Check.Equal(0, TextWidth.CodePointWidth(0x0301), "Combining acute");
                            Check.Equal(0, TextWidth.CodePointWidth(0x200D), "Zero width joiner");
                            Check.Equal(2, TextWidth.CodePointWidth(0x1F600), "Grinning face emoji");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Unicode", "CombiningCluster", "Base + combining mark is one grapheme",
                        _ =>
                        {
                            string basePlusMark = "é"; // e + combining acute (U+0301)
                            string input = basePlusMark + "llo";
                            IReadOnlyList<Grapheme> clusters = Graphemes.Split(input);
                            Check.Equal(4, clusters.Count, "Cluster count");
                            Check.Equal(basePlusMark, clusters[0].Text, "First cluster fuses the mark");
                            Check.Equal(1, clusters[0].Width, "First cluster width");
                            Check.Equal(4, Graphemes.MeasureWidth(input), "Total width");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Unicode", "WideMeasure", "Mixed narrow and wide measurement",
                        _ =>
                        {
                            string mixed = "A世B"; // A + CJK wide (U+4E16) + B
                            Check.Equal(4, Graphemes.MeasureWidth(mixed), "A + wide + B width");
                            Check.Equal(3, Graphemes.Split(mixed).Count, "Cluster count");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Unicode", "EmojiZwjSequence", "Emoji ZWJ family is one wide grapheme",
                        _ =>
                        {
                            string zwj = "‍";
                            string family = char.ConvertFromUtf32(0x1F468) + zwj
                                + char.ConvertFromUtf32(0x1F469) + zwj
                                + char.ConvertFromUtf32(0x1F467);
                            IReadOnlyList<Grapheme> clusters = Graphemes.Split(family);
                            Check.Equal(1, clusters.Count, "Family is a single cluster");
                            Check.Equal(2, clusters[0].Width, "Family occupies two columns");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Unicode", "RegionalIndicatorFlag", "Regional indicator pair is one flag",
                        _ =>
                        {
                            string flag = char.ConvertFromUtf32(0x1F1FA) + char.ConvertFromUtf32(0x1F1F8);
                            IReadOnlyList<Grapheme> clusters = Graphemes.Split(flag);
                            Check.Equal(1, clusters.Count, "Flag is a single cluster");
                            Check.Equal(2, clusters[0].Width, "Flag occupies two columns");

                            string threeRi = flag + char.ConvertFromUtf32(0x1F1FA);
                            Check.Equal(2, Graphemes.Split(threeRi).Count, "Third indicator starts a new cluster");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("Unicode", "NullThrows", "Null input is rejected",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => Graphemes.Split(null!), "Split(null)");
                            Check.Throws<ArgumentNullException>(() => Graphemes.MeasureWidth(null!), "MeasureWidth(null)");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
