namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.10 item C9: <see cref="CalendarHeatMap"/> and <see cref="WeekdayLabelMode"/>.
    /// </summary>
    public static class CalendarHeatMapSuite
    {
        private const string _Id = "CalendarHeatMap";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Calendar Heat Map",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "Year", "A 365-day range starting on a Wednesday: week count, blank leading days, and month labels",
                        _ =>
                        {
                            CalendarHeatMap calendar = new CalendarHeatMap();
                            DateTime from = new DateTime(2025, 1, 1);
                            Check.Equal(DayOfWeek.Wednesday, from.DayOfWeek, "starts on a Wednesday");
                            calendar.SetData(from, from.AddDays(364), new Dictionary<DateTime, double>());
                            Check.Equal(53, calendar.WeekCount, "53 week columns");
                            Check.Equal(new DateTime(2025, 12, 31), calendar.SelectedDate, "the last day is selected");
                            calendar.Select(from);
                            string[] lines = Snapshot.RenderWidget(calendar, 200, 11).Split('\n');
                            Check.True(lines[0].Contains("Jan") && lines[0].Contains("Feb") && lines[0].Contains("Dec"), "month labels: " + lines[0]);
                            Check.Equal("  ", lines[1].Substring(4, 2), "Sunday of the first week is blank");
                            Check.True(lines[4].Length > 4 && lines[4].Substring(4, 2).Trim().Length > 0, "Wednesday of the first week is drawn");
                            int jan = lines[0].IndexOf("Jan", StringComparison.Ordinal);
                            int feb = lines[0].IndexOf("Feb", StringComparison.Ordinal);
                            Check.Equal(jan + 4 * 2, feb, "February is labeled on the week of Feb 1, four weeks later");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "FirstDayAndLabels", "FirstDayOfWeek Monday shifts rows; weekday label modes",
                        _ =>
                        {
                            CalendarHeatMap calendar = Week();
                            string[] lines = Snapshot.RenderWidget(calendar, 40, 11).Split('\n');
                            Check.True(lines[2].StartsWith("Mon", StringComparison.Ordinal), "Alternate labels the second row (Mon with Sunday first)");
                            Check.False(lines[1].StartsWith("Sun", StringComparison.Ordinal), "the first row unlabeled");
                            calendar.FirstDayOfWeek = DayOfWeek.Monday;
                            lines = Snapshot.RenderWidget(calendar, 40, 11).Split('\n');
                            Check.True(lines[2].StartsWith("Tue", StringComparison.Ordinal), "rows shifted");
                            calendar.WeekdayLabels = WeekdayLabelMode.All;
                            Check.True(Snapshot.RenderWidget(calendar, 40, 11).Split('\n')[1].StartsWith("Mon", StringComparison.Ordinal), "All labels every row");
                            calendar.WeekdayLabels = WeekdayLabelMode.None;
                            Check.False(Snapshot.RenderWidget(calendar, 40, 11).Contains("Mon"), "None");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Levels", "Levels follow the values; the legend is on by default; a high-contrast glyph set renders",
                        _ =>
                        {
                            CalendarHeatMap calendar = Week();
                            Check.Equal(5, calendar.LevelCount, "five levels");
                            Check.True(calendar.ShowLegend, "legend on");
                            calendar.LevelGlyphs = new[] { ".", "-", "=", "+", "#" };
                            string screen = Snapshot.RenderWidget(calendar, 40, 11);
                            Check.True(screen.Contains("Less . - = + # More"), "legend with the glyphs: " + screen);
                            Check.True(screen.Contains("##"), "the busiest day is level 4");
                            Check.Throws<ArgumentOutOfRangeException>(() => calendar.LevelCount = 1, "level count guard");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Navigation", "Down on the last weekday wraps to the next week; Up wraps back; moves stay in range; blank days cannot be selected",
                        _ =>
                        {
                            CalendarHeatMap calendar = new CalendarHeatMap();
                            DateTime from = new DateTime(2026, 3, 4);
                            calendar.SetData(from, from.AddDays(20), new Dictionary<DateTime, double>());
                            List<DateTime> selected = new List<DateTime>();
                            calendar.DateSelected += (s, d) => selected.Add(d);
                            calendar.Select(new DateTime(2026, 3, 7));
                            Check.Equal(DayOfWeek.Saturday, calendar.SelectedDate.DayOfWeek, "a Saturday");
                            calendar.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal(new DateTime(2026, 3, 8), calendar.SelectedDate, "Down wraps to Sunday of the next week");
                            calendar.HandleKey(KeyEvent.Special(KeyCode.Up));
                            Check.Equal(new DateTime(2026, 3, 7), calendar.SelectedDate, "Up wraps back");
                            calendar.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Check.Equal(new DateTime(2026, 3, 14), calendar.SelectedDate, "Right is one week");
                            calendar.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Check.Equal(from, calendar.SelectedDate, "Home is the first day");
                            calendar.HandleKey(KeyEvent.Special(KeyCode.Up));
                            Check.Equal(from, calendar.SelectedDate, "never before the range");
                            calendar.HandleKey(KeyEvent.Special(KeyCode.End));
                            Check.Equal(from.AddDays(20), calendar.SelectedDate, "End is the last day");
                            calendar.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Check.Equal(from.AddDays(20), calendar.SelectedDate, "never past the range");
                            Check.True(selected.Count >= 5, "DateSelected raised");
                            calendar.Select(new DateTime(2020, 1, 1));
                            Check.Equal(from, calendar.SelectedDate, "Select clamps");
                            Snapshot.RenderWidget(calendar, 40, 11);
                            Check.False(calendar.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 4, 1, KeyModifiers.None, 1)), "a blank leading day refuses a click");
                            DateTime activated = DateTime.MinValue;
                            calendar.DateActivated += (s, d) => activated = d;
                            Check.True(calendar.HandleKey(KeyEvent.Special(KeyCode.Enter)), "Enter with a handler");
                            Check.Equal(from, activated, "DateActivated");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Footer", "The default description, a custom one, and the summary draw on the footer",
                        _ =>
                        {
                            CalendarHeatMap calendar = Week();
                            calendar.Select(new DateTime(2026, 3, 4));
                            string screen = Snapshot.RenderWidget(calendar, 60, 11);
                            Check.True(screen.Contains("03/04/2026: 9"), "invariant short date and value: " + screen);
                            calendar.Summary = "Total 21";
                            calendar.DayDescription = d => d.ToString("MMM d", CultureInfo.InvariantCulture);
                            screen = Snapshot.RenderWidget(calendar, 60, 11);
                            Check.True(screen.Contains("Total 21   Mar 4"), "summary then description");
                            Check.Equal("Mar 4", calendar.GetTooltip(4, 4), "tooltip uses the description");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "NarrowWidth", "A narrow calendar shows the newest weeks first and scrolls with the selection",
                        _ =>
                        {
                            CalendarHeatMap calendar = new CalendarHeatMap();
                            DateTime from = new DateTime(2025, 1, 1);
                            calendar.SetData(from, from.AddDays(364), new Dictionary<DateTime, double>());
                            Snapshot.RenderWidget(calendar, 24, 11);
                            Check.True(calendar.FirstVisibleWeek > 40, "the newest weeks are visible");
                            calendar.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Snapshot.RenderWidget(calendar, 24, 11);
                            Check.Equal(0, calendar.FirstVisibleWeek, "scrolled to the start");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Data", "SetData guards and null clearing; keys with a time part match their day and sum; cultures are deterministic by default",
                        _ =>
                        {
                            CalendarHeatMap calendar = new CalendarHeatMap();
                            Check.Throws<ArgumentException>(() => calendar.SetData(new DateTime(2026, 2, 1), new DateTime(2026, 1, 1), null), "to before from");
                            Dictionary<DateTime, double> values = new Dictionary<DateTime, double>
                            {
                                { new DateTime(2026, 3, 1, 9, 0, 0), 2 },
                                { new DateTime(2026, 3, 1, 17, 30, 0), 3 },
                            };
                            calendar.SetData(new DateTime(2026, 3, 1), new DateTime(2026, 3, 10), values);
                            Check.Equal(5.0, calendar.ValueOn(new DateTime(2026, 3, 1, 23, 59, 0)), "summed per day, time ignored");
                            Check.Equal(0.0, calendar.ValueOn(new DateTime(2026, 3, 2)), "missing days are zero");
                            string invariant = Snapshot.RenderWidget(calendar, 40, 11);
                            CultureInfo saved = CultureInfo.CurrentCulture;
                            try
                            {
                                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                                Check.Equal(invariant, Snapshot.RenderWidget(calendar, 40, 11), "identical under another current culture");
                            }
                            finally
                            {
                                CultureInfo.CurrentCulture = saved;
                            }

                            calendar.Culture = new CultureInfo("fr-FR");
                            Check.True(Snapshot.RenderWidget(calendar, 40, 11).Contains("mars"), "French month name");
                            Check.Throws<ArgumentNullException>(() => calendar.Culture = null!, "null culture");
                            calendar.SetData(new DateTime(2026, 3, 1), new DateTime(2026, 3, 10), null);
                            Check.Equal(0, calendar.WeekCount, "cleared");
                            Check.Equal(string.Empty, Snapshot.RenderWidget(calendar, 40, 11).Trim(), "renders nothing");
                            Check.False(calendar.HandleKey(KeyEvent.Special(KeyCode.Right)), "no keys without data");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "CellGap", "CellGap is forwarded: weeks, month labels, legend, footer, scrolling, and clicks stay aligned",
                        _ =>
                        {
                            CalendarHeatMap calendar = new CalendarHeatMap();
                            Check.Equal(0, calendar.CellGap, "CellGap defaults to 0");
                            Check.Equal(0, calendar.RowGap, "RowGap defaults to 0");
                            DateTime from = new DateTime(2025, 1, 1);
                            calendar.SetData(from, from.AddDays(364), new Dictionary<DateTime, double> { { new DateTime(2025, 1, 8), 3 } });
                            calendar.CellGap = 1;
                            calendar.Select(from);
                            Check.Equal(new Size(4 + 53 * 2 + 52, 10), calendar.Measure(new Size(400, 20)), "53 weeks of width 2 and 52 gaps");
                            string[] lines = Snapshot.RenderWidget(calendar, 200, 11).Split('\n');
                            int jan = lines[0].IndexOf("Jan", StringComparison.Ordinal);
                            int feb = lines[0].IndexOf("Feb", StringComparison.Ordinal);
                            Check.Equal(4, jan, "January over the first week");
                            Check.Equal(jan + 4 * 3, feb, "February four weeks of three columns later");
                            Check.Equal("  ", lines[1].Substring(4, 2), "Sunday of the first week is blank");
                            Check.True(lines[4].Substring(4, 2).Trim().Length > 0, "Wednesday of the first week is drawn");
                            Check.Equal(" ", lines[4].Substring(6, 1), "the gap after the first week is blank");
                            Check.Equal("\u2588\u2588", lines[4].Substring(7, 2), "the second week starts after the gap");
                            Check.True(lines[8].StartsWith("    Less", StringComparison.Ordinal), "legend on row 8: " + lines[8]);
                            Check.True(lines[9].Contains("01/01/2025"), "footer on row 9: " + lines[9]);
                            Check.False(calendar.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 6, 4, KeyModifiers.None, 1)), "a click in the gap");
                            Check.Equal(from, calendar.SelectedDate, "selection unchanged");
                            Check.True(calendar.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 8, 4, KeyModifiers.None, 1)), "a click on the second week");
                            Check.Equal(new DateTime(2025, 1, 8), calendar.SelectedDate, "Wednesday of the second week");
                            Check.True(calendar.GetTooltip(7, 4) != null && calendar.GetTooltip(7, 4)!.Contains("01/08/2025"), "tooltip on the second week");
                            Check.True(calendar.GetTooltip(6, 4) == null, "no tooltip in the gap");

                            calendar.HandleKey(KeyEvent.Special(KeyCode.End));
                            Snapshot.RenderWidget(calendar, 25, 11);
                            Check.Equal(52 - 7 + 1, calendar.FirstVisibleWeek, "seven weeks of three columns fit beside the labels");
                            calendar.HandleKey(KeyEvent.Special(KeyCode.Home));
                            Snapshot.RenderWidget(calendar, 25, 11);
                            Check.Equal(0, calendar.FirstVisibleWeek, "scrolled to the start");
                            Check.Throws<ArgumentOutOfRangeException>(() => calendar.CellGap = 3, "CellGap guard");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "RowGap", "RowGap is forwarded: weekday labels stay beside their rows and the legend and footer move down",
                        _ =>
                        {
                            CalendarHeatMap calendar = Week();
                            calendar.RowGap = 1;
                            calendar.Select(new DateTime(2026, 3, 4));
                            Check.Equal(new Size(6, 16), calendar.Measure(new Size(60, 30)), "header, 13 grid rows, legend, footer");
                            string[] lines = Snapshot.RenderWidget(calendar, 60, 17).Split('\n');
                            Check.True(lines[3].StartsWith("Mon", StringComparison.Ordinal), "Monday on row 3: " + lines[3]);
                            Check.True(lines[7].StartsWith("Wed", StringComparison.Ordinal), "Wednesday on row 7");
                            Check.True(lines[11].StartsWith("Fri", StringComparison.Ordinal), "Friday on row 11");
                            Check.Equal(string.Empty, lines[2].Trim(), "gap rows are blank");
                            Check.Equal("\u2588\u2588", lines[7].Substring(4, 2), "the busiest day beside its label");
                            Check.True(lines[14].StartsWith("    Less", StringComparison.Ordinal), "legend on row 14: " + lines[14]);
                            Check.True(lines[15].Contains("03/04/2026: 9"), "footer on row 15: " + lines[15]);
                            Check.False(calendar.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 4, 6, KeyModifiers.None, 1)), "a click on a gap row");
                            Check.True(calendar.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 4, 5, KeyModifiers.None, 1)), "a click on Tuesday");
                            Check.Equal(new DateTime(2026, 3, 3), calendar.SelectedDate, "Tuesday selected");
                            calendar.HandleKey(KeyEvent.Special(KeyCode.Down));
                            Check.Equal(new DateTime(2026, 3, 4), calendar.SelectedDate, "Down still moves one day");
                            Check.Throws<ArgumentOutOfRangeException>(() => calendar.RowGap = 2, "RowGap guard");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Palette", "Palette and PaletteMode are forwarded; empty days keep the dot and the empty color; the legend follows",
                        _ =>
                        {
                            CalendarHeatMap calendar = Week();
                            Check.True(calendar.Palette == null, "Palette defaults to null");
                            Check.Equal(HeatMapPaletteMode.Foreground, calendar.PaletteMode, "PaletteMode defaults to Foreground");
                            calendar.Palette = HeatMapPalette.Heat;
                            CellBuffer buffer = MarkdownThemeSuite.Draw(calendar, 40, 11);
                            Check.Equal(CellStyle.Default.WithForeground(HeatMapPalette.Heat.Colors[3]), buffer.Get(4, 4).Style, "the busiest day is the top color");
                            Check.Equal("\u00B7", buffer.Get(4, 1).Grapheme, "an empty day keeps the dot");
                            Check.Equal(CellStyle.Default.WithForeground(HeatMapPalette.Heat.EmptyColor), buffer.Get(4, 1).Style, "in the empty color");
                            for (int level = 0; level < 5; level++)
                                Check.Equal(CellStyle.Default.WithForeground(HeatMapPalette.Heat.ColorForLevel(level, 5)), buffer.Get(9 + level * 2, 8).Style, "legend level " + level);
                            calendar.PaletteMode = HeatMapPaletteMode.Background;
                            buffer = MarkdownThemeSuite.Draw(calendar, 40, 11);
                            Check.Equal(HeatMapPalette.Heat.Colors[3], buffer.Get(4, 4).Style.Background, "tiles forwarded");
                            Check.Equal(HeatMapPalette.Heat.Colors[3], buffer.Get(17, 8).Style.Background, "legend tiles");
                            Check.Equal(Color.Default, buffer.Get(4, 1).Style.Background, "empty days are not tiled");
                            calendar.LevelStyles = new[] { CellStyle.Default, CellStyle.Default, CellStyle.Default, CellStyle.Default, CellStyle.Default };
                            Check.Equal(CellStyle.Default, MarkdownThemeSuite.Draw(calendar, 40, 11).Get(4, 4).Style, "LevelStyles take precedence");
                            Check.Throws<ArgumentOutOfRangeException>(() => calendar.PaletteMode = (HeatMapPaletteMode)9, "PaletteMode guard");
                            return Task.CompletedTask;
                        }),
                });
        }

        private static CalendarHeatMap Week()
        {
            CalendarHeatMap calendar = new CalendarHeatMap();
            Dictionary<DateTime, double> values = new Dictionary<DateTime, double>();
            for (int i = 0; i < 7; i++)
                values[new DateTime(2026, 3, 1).AddDays(i)] = i * 1.5;
            values[new DateTime(2026, 3, 4)] = 9;
            calendar.SetData(new DateTime(2026, 3, 1), new DateTime(2026, 3, 7), values);
            return calendar;
        }
    }
}
