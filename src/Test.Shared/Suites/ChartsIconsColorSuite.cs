namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite for charts, icons, and color: the Braille canvas and charts (10.27), the icon catalog
    /// (10.26), and the color picker (10.37).
    /// </summary>
    public static class ChartsIconsColorSuite
    {
        /// <summary>
        /// Builds the charts, icons, and color suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ChartsIconsColor",
                displayName: "Charts, Icons & Color",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ChartsIconsColor", "BraillePixels", "Braille canvas sets, clears, and reports pixels",
                        _ =>
                        {
                            BrailleCanvas canvas = new BrailleCanvas(3, 2);
                            Check.Equal(6, canvas.PixelWidth, "pixel width is twice the cells");
                            Check.Equal(8, canvas.PixelHeight, "pixel height is four times the cells");

                            canvas.Set(0, 0);
                            Check.True(canvas.Get(0, 0), "pixel set");
                            Check.False(canvas.Get(1, 0), "neighbor unset");
                            canvas.Unset(0, 0);
                            Check.False(canvas.Get(0, 0), "pixel cleared");

                            canvas.Set(100, 100); // out of range: ignored, no throw
                            Check.False(canvas.Get(100, 100), "out-of-range pixel stays unset");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChartsIconsColor", "BrailleRender", "Braille canvas renders the correct glyph",
                        _ =>
                        {
                            BrailleCanvas canvas = new BrailleCanvas(1, 1);
                            canvas.Set(0, 0);
                            CellBuffer buffer = new CellBuffer(1, 1);
                            canvas.Render(new BufferSurface(buffer));
                            string expected = ((char)(0x2800 + 0x01)).ToString();
                            Check.Equal(expected, buffer.Get(0, 0).Grapheme, "top-left dot maps to U+2801");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChartsIconsColor", "BrailleGuards", "Braille canvas rejects bad dimensions",
                        _ =>
                        {
                            Check.Throws<ArgumentOutOfRangeException>(() => new BrailleCanvas(0, 2), "zero width");
                            Check.Throws<ArgumentOutOfRangeException>(() => new BrailleCanvas(2, -1), "negative height");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChartsIconsColor", "LineChart", "Line chart plots a series without error",
                        _ =>
                        {
                            LineChart chart = new LineChart(new double[] { 0, 2, 1, 3, 2 });
                            Check.Equal(5, chart.Count, "five samples");

                            CellBuffer buffer = new CellBuffer(12, 4);
                            chart.Render(new BufferSurface(buffer));
                            Check.True(HasInk(buffer), "chart drew at least one glyph");

                            LineChart empty = new LineChart(Array.Empty<double>());
                            CellBuffer blank = new CellBuffer(12, 4);
                            empty.Render(new BufferSurface(blank));
                            Check.False(HasInk(blank), "empty series draws nothing");

                            Check.Throws<ArgumentNullException>(() => new LineChart(null!), "null samples");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChartsIconsColor", "BarChart", "Bar chart renders labels, bars, and values",
                        _ =>
                        {
                            BarChart chart = new BarChart().Add("cpu", 80).Add("mem", 40);
                            Check.Equal(2, chart.Count, "two bars");

                            CellBuffer buffer = new CellBuffer(24, 2);
                            chart.Render(new BufferSurface(buffer));
                            Check.True(ReadRow(buffer, 0).Contains("cpu") && ReadRow(buffer, 0).Contains("80"), "first bar labeled and valued");
                            Check.True(ReadRow(buffer, 1).Contains("mem") && ReadRow(buffer, 1).Contains("40"), "second bar labeled and valued");
                            Check.True(ReadRow(buffer, 0).Contains("█"), "bar drawn with block glyphs");

                            chart.Clear();
                            Check.Equal(0, chart.Count, "cleared");
                            Check.Throws<ArgumentNullException>(() => new BarChart().Add(null!, 1), "null label");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChartsIconsColor", "IconModes", "Icons resolve per mode and by name",
                        _ =>
                        {
                            IconMode previous = Icons.Mode;
                            try
                            {
                                Icons.Mode = IconMode.Ascii;
                                Check.Equal("OK", Icons.Check.ToString(), "ascii check");
                                Icons.Mode = IconMode.Unicode;
                                Check.Equal("✓", Icons.Check.ToString(), "unicode check");
                                Check.True(Icons.Folder.Nerd.Length > 0, "nerd folder glyph present");

                                Check.Equal(Icons.Folder.Unicode, Icons.Get("folder").Unicode, "lookup by name");
                                Check.True(Icons.TryGet("git-branch", out Icon branch) && branch.Ascii == "Y", "try-get hit");
                                Check.False(Icons.TryGet("nope", out Icon _), "try-get miss");
                                Check.Throws<KeyNotFoundException>(() => Icons.Get("nope"), "unknown name throws");
                                Check.Throws<ArgumentNullException>(() => Icons.Get(null!), "null name throws");
                                Check.Throws<ArgumentNullException>(() => new Icon(null!, "u", "a"), "null glyph throws");
                            }
                            finally
                            {
                                Icons.Mode = previous;
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ChartsIconsColor", "ColorPicker", "Color picker edits channels and composes a color",
                        _ =>
                        {
                            ColorPicker picker = new ColorPicker(Color.FromRgb(10, 20, 30));
                            Check.Equal(10, picker.Value.R, "initial red");
                            Check.Equal(0, picker.ActiveChannel, "red active first");

                            picker.HandleKey(KeyEvent.Special(KeyCode.Down)); // -> green
                            Check.Equal(1, picker.ActiveChannel, "moved to green");
                            picker.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Check.Equal(21, picker.ActiveValue, "right nudged green up");
                            picker.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal(255, picker.ActiveValue, "end snaps to 255");
                            picker.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal(0, picker.ActiveValue, "home snaps to 0");
                            Check.Equal(0, picker.Value.G, "green now zero");

                            picker.HandleKey(KeyEvent.Special(KeyCode.Up)); // wrap green(1)->red(0)
                            Check.Equal(0, picker.ActiveChannel, "up wraps back to red");
                            Check.False(picker.HandleKey(KeyEvent.Char('z')), "unrelated key ignored");

                            CellBuffer buffer = new CellBuffer(20, 4);
                            picker.Render(new BufferSurface(buffer));
                            Check.True(ReadRow(buffer, 3).Contains("#"), "preview shows a hex string");
                            return Task.CompletedTask;
                        })
                });
        }

        private static bool HasInk(CellBuffer buffer)
        {
            for (int y = 0; y < buffer.Height; y++)
            {
                for (int x = 0; x < buffer.Width; x++)
                {
                    string g = buffer.Get(x, y).Grapheme;
                    if (!string.IsNullOrEmpty(g) && g != " ")
                        return true;
                }
            }

            return false;
        }

        private static string ReadRow(CellBuffer buffer, int row)
        {
            StringBuilder builder = new StringBuilder();
            for (int x = 0; x < buffer.Width; x++)
                builder.Append(buffer.Get(x, row).Grapheme);

            return builder.ToString().TrimEnd();
        }
    }
}
