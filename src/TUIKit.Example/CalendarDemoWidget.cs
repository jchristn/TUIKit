namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    // The "Activity calendar" tour page (1.10, C8 and C9): a CalendarHeatMap over a year of generated activity, with
    // day and week movement, a legend, a summary, and a description of the selected day. 'm' starts weeks on
    // Monday, 'w' cycles the weekday labels, 'g' switches to distinct ASCII glyphs that read without color, and 't'
    // follows the theme.
    internal sealed class CalendarDemoWidget : IWidget, IFocusable, IFocusAware, IMouseAware, IKeyHintSource
    {
        private static readonly string[] _AsciiGlyphs = { " ", ".", ":", "+", "#" };

        private readonly TuiApplication? _App;
        private readonly CalendarHeatMap _Calendar = new CalendarHeatMap();
        private Theme? _AppliedTheme;
        private string _Status = "Arrows move by day and week; Enter opens a day.";

        internal CalendarDemoWidget(TuiApplication? app)
        {
            _App = app;
            Dictionary<DateTime, double> values = Activity(out DateTime from, out DateTime to);
            _Calendar.SetData(from, to, values);
            _Calendar.Summary = values.Values.Sum().ToString("0") + " commits";
            _Calendar.DayDescription = d => d.ToString("ddd yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ": " + _Calendar.ValueOn(d).ToString("0") + " commits";
            _Calendar.DateActivated += (s, d) => _Status = "Opened " + d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ".";
        }

        internal CalendarHeatMap Calendar
        {
            get { return _Calendar; }
        }

        // A deterministic year of activity: busier on weekdays, quieter in August and late December.
        internal static Dictionary<DateTime, double> Activity(out DateTime from, out DateTime to)
        {
            to = new DateTime(2026, 10, 14);
            from = to.AddDays(-364);
            Dictionary<DateTime, double> values = new Dictionary<DateTime, double>();
            for (DateTime day = from; day <= to; day = day.AddDays(1))
            {
                int seed = (day.DayOfYear * 37 + day.Year) % 11;
                double weekday = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday ? 0.3 : 1.0;
                double season = day.Month == 8 || (day.Month == 12 && day.Day > 20) ? 0.2 : 1.0;
                values[day] = Math.Round(seed * weekday * season);
            }

            return values;
        }

        public bool HandleKey(KeyEvent key)
        {
            if (_Calendar.HandleKey(key))
                return true;

            if (key.Code != KeyCode.Character || key.Modifiers != KeyModifiers.None)
                return false;

            switch (key.Rune)
            {
                case 'm':
                    _Calendar.FirstDayOfWeek = _Calendar.FirstDayOfWeek == DayOfWeek.Sunday ? DayOfWeek.Monday : DayOfWeek.Sunday;
                    _Status = "Weeks start on " + _Calendar.FirstDayOfWeek + ".";
                    return true;
                case 'w':
                    _Calendar.WeekdayLabels = (WeekdayLabelMode)(((int)_Calendar.WeekdayLabels + 1) % 3);
                    _Status = "Weekday labels: " + _Calendar.WeekdayLabels + ".";
                    return true;
                case 'g':
                    _Calendar.LevelGlyphs = _Calendar.LevelGlyphs == null ? _AsciiGlyphs : null;
                    _Status = _Calendar.LevelGlyphs != null ? "ASCII levels: readable without color." : "Shade levels.";
                    return true;
                case 't':
                    _Calendar.UseThemeStyles = !_Calendar.UseThemeStyles;
                    _Status = "UseThemeStyles " + (_Calendar.UseThemeStyles ? "on" : "off") + ".";
                    return true;
                default:
                    return false;
            }
        }

        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint> hints = new List<KeyHint>();
            IReadOnlyList<KeyHint>? own = _Calendar.GetKeyHints();
            if (own != null)
                hints.AddRange(own);
            hints.Add(new KeyHint("M", "Monday"));
            hints.Add(new KeyHint("G", "Glyphs"));
            return hints;
        }

        public void OnFocusChanged(bool focused)
        {
            _Calendar.OnFocusChanged(focused);
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            return _Calendar.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X, mouse.Y - 1, mouse.Modifiers, mouse.ClickCount));
        }

        public Size Measure(Size available)
        {
            return available;
        }

        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 2)
                return;

            Theme theme = _App?.Theme ?? Theme.Dark;
            if (!ReferenceEquals(theme, _AppliedTheme))
            {
                _AppliedTheme = theme;
                _Calendar.ApplyTheme(theme);
            }

            surface.DrawText(0, 0, TUIKit.Unicode.TextFit.Ellipsize(_Status + "  m w g t", width), CellStyle.Default.WithForeground(Color.FromPalette(2)));
            _Calendar.Render(new SurfaceView(surface, new Rect(0, 1, width, height - 1)));
        }
    }
}
