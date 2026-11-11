namespace TUIKit.Example
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    // The "Heat map spacing" tour page (1.13): a week of activity by hour of day (7 rows by 24 columns) in a selectable
    // HeatMap. 'g' cycles CellGap (0, 1, 2), 'c' cycles CellWidth (1 to 4), and 'r' toggles RowGap; arrows move, and the
    // footer and tooltip describe the selected hour.
    internal sealed class HeatMapSpacingDemoWidget : IWidget, IFocusable, IFocusAware, IMouseAware, IKeyHintSource, ITooltipProvider
    {
        internal static readonly string[] Days = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

        private readonly TuiApplication? _App;
        private readonly HeatMap _Map;
        private readonly double[,] _Values;
        private Theme? _AppliedTheme;

        internal HeatMapSpacingDemoWidget(TuiApplication? app)
        {
            _App = app;
            _Values = HourlyActivity();
            _Map = Build(_Values);
            _Map.IsSelectable = true;
            _Map.ShowSelectionDescription = true;
            _Map.CellGap = 1;
            _Map.Select(2, 14);
        }

        internal HeatMap Map
        {
            get { return _Map; }
        }

        // A deterministic week of activity by hour: quiet at night, a morning and an afternoon peak on weekdays, a late
        // morning bump at the weekend.
        internal static double[,] HourlyActivity()
        {
            double[,] values = new double[7, 24];
            for (int day = 0; day < 7; day++)
            {
                bool weekend = day >= 5;
                for (int hour = 0; hour < 24; hour++)
                {
                    double morning = Math.Exp(-Math.Pow((hour - (weekend ? 11 : 10)) / 2.0, 2));
                    double afternoon = weekend ? 0.3 * Math.Exp(-Math.Pow((hour - 16) / 3.0, 2)) : Math.Exp(-Math.Pow((hour - 15) / 2.0, 2));
                    double jitter = ((day * 31 + hour * 17) % 7) / 20.0;
                    double value = (morning + afternoon) * (weekend ? 6 : 14) + jitter * 3;
                    values[day, hour] = hour < 7 || hour > 21 ? Math.Floor(jitter * 2) : Math.Round(value);
                }
            }

            return values;
        }

        // The column labels: the hour every three hours, spanning the two unlabeled hours after it.
        internal static string[] HourLabels()
        {
            string[] labels = new string[24];
            for (int hour = 0; hour < 24; hour++)
                labels[hour] = hour % 3 == 0 ? hour.ToString("00", CultureInfo.InvariantCulture) : string.Empty;
            return labels;
        }

        // A levelled map over the hourly data, shared by the spacing and palette demos and --heatmap-once.
        internal static HeatMap Build(double[,] values)
        {
            HeatMap map = new HeatMap();
            map.SetCells(values);
            map.SetRowLabels(Days);
            map.SetColumnLabels(HourLabels());
            map.SpanColumnLabels = true;
            map.LevelCount = 5;
            map.LevelGlyphs = new[] { "·", "░", "▒", "▓", "█" };
            map.ShowLegend = true;
            map.CellDescription = (r, c) => Days[r] + " " + c.ToString("00", CultureInfo.InvariantCulture) + ":00-"
                + ((c + 1) % 24).ToString("00", CultureInfo.InvariantCulture) + ":00, " + values[r, c].ToString("0", CultureInfo.InvariantCulture) + " events";
            return map;
        }

        public bool HandleKey(KeyEvent key)
        {
            if (_Map.HandleKey(key))
                return true;

            if (key.Code != KeyCode.Character || key.Modifiers != KeyModifiers.None)
                return false;

            switch (key.Rune)
            {
                case 'g':
                    _Map.CellGap = (_Map.CellGap + 1) % 3;
                    return true;
                case 'c':
                    _Map.CellWidth = _Map.CellWidth % 4 + 1;
                    return true;
                case 'r':
                    _Map.RowGap = 1 - _Map.RowGap;
                    return true;
                default:
                    return false;
            }
        }

        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            List<KeyHint> hints = new List<KeyHint>();
            IReadOnlyList<KeyHint>? own = _Map.GetKeyHints();
            if (own != null)
                hints.AddRange(own);
            hints.Add(new KeyHint("G", "Gap"));
            hints.Add(new KeyHint("C", "Width"));
            hints.Add(new KeyHint("R", "Row gap"));
            return hints;
        }

        public void OnFocusChanged(bool focused)
        {
            _Map.OnFocusChanged(focused);
        }

        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            return _Map.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X, mouse.Y - 1, mouse.Modifiers, mouse.ClickCount));
        }

        public string? GetTooltip(int x, int y)
        {
            return _Map.GetTooltip(x, y - 1);
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
                _Map.ApplyTheme(theme);
            }

            string settings = "g gap " + _Map.CellGap + "  c width " + _Map.CellWidth + "  r row gap " + _Map.RowGap;
            surface.DrawText(0, 0, TUIKit.Unicode.TextFit.Ellipsize(settings, width), CellStyle.Default.WithForeground(Color.FromPalette(2)));
            _Map.Render(new SurfaceView(surface, new Rect(0, 1, width, height - 1)));
        }
    }
}
