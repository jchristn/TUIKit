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
