namespace TUIKit.Widgets
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A contribution calendar: one column per week, one row per weekday, month names above the weeks, weekday names
    /// at the side, a legend, and a footer describing the selected day. For activity, commits, usage, incidents, or
    /// spending over a date range. Built on a selectable <see cref="HeatMap"/>.
    /// </summary>
    /// <remarks>
    /// Dates are calendar days: every <see cref="DateTime"/> is reduced to <see cref="DateTime.Date"/> and its
    /// <see cref="DateTime.Kind"/> is ignored, so nothing is converted between time zones. Up and Down move one day
    /// (wrapping into the next or previous week), Left and Right one week, and Home and End go to the first and last
    /// day of the range; days outside the range in the first and last week are blank and cannot be selected. When the
    /// range has more weeks than fit, the visible weeks follow the selection, and the newest week shows first. Not
    /// thread-safe: use it from the UI thread.
    /// </remarks>
    public sealed class CalendarHeatMap : IWidget, IFocusable, IFocusStop, IFocusAware, IMouseAware, IKeyHintSource, IThemeable, ITooltipProvider
    {
        #region Private-Members

        private readonly HeatMap _Map = new HeatMap();
        private readonly Dictionary<DateTime, double> _Values = new Dictionary<DateTime, double>();
        private DateTime _From = DateTime.MinValue.Date;
        private DateTime _To = DateTime.MinValue.Date;
        private DateTime _Start = DateTime.MinValue.Date;
        private int _Weeks;
        private bool _HasData;
        private DayOfWeek _FirstDayOfWeek = DayOfWeek.Sunday;
        private WeekdayLabelMode _WeekdayLabels = WeekdayLabelMode.Alternate;
        private CultureInfo _Culture = CultureInfo.InvariantCulture;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="CalendarHeatMap"/> class with no data.
        /// </summary>
        public CalendarHeatMap()
        {
            _Map.IsSelectable = true;
            _Map.CellWidth = 2;
            _Map.LevelCount = 5;
            _Map.LevelGlyphs = new[] { "\u00B7", "\u2591", "\u2592", "\u2593", "\u2588" };
            _Map.ShowLegend = true;
            _Map.SpanColumnLabels = true;
            _Map.IsCellSelectable = (row, column) => InRange(DateAt(row, column));
            _Map.CellBlank = (row, column) => !InRange(DateAt(row, column));
            _Map.NavigateOverride = Navigate;
            _Map.SelectionChanged += (s, e) => DateSelected?.Invoke(this, SelectedDate);
            _Map.CellActivated += (s, e) => DateActivated?.Invoke(this, SelectedDate);
        }

        #endregion

        #region Public-Members

        /// <summary>
        /// Raised with the new date when the selected day changes.
        /// </summary>
        public event EventHandler<DateTime>? DateSelected;

        /// <summary>
        /// Raised with the selected date on Enter or a double click. Enter is consumed only while a handler is attached.
        /// </summary>
        public event EventHandler<DateTime>? DateActivated;

        /// <summary>
        /// Gets the first day of the range (a date). <see cref="DateTime.MinValue"/> before <see cref="SetData"/>.
        /// </summary>
        public DateTime From
        {
            get { return _From; }
        }

        /// <summary>
        /// Gets the last day of the range (a date). <see cref="DateTime.MinValue"/> before <see cref="SetData"/>.
        /// </summary>
        public DateTime To
        {
            get { return _To; }
        }

        /// <summary>
        /// Gets the number of week columns the range spans. 0 before <see cref="SetData"/>.
        /// </summary>
        public int WeekCount
        {
            get { return _Weeks; }
        }

        /// <summary>
        /// Gets the selected day. <see cref="DateTime.MinValue"/> before <see cref="SetData"/>.
        /// </summary>
        public DateTime SelectedDate
        {
            get { return _HasData ? DateAt(_Map.SelectedRow, _Map.SelectedColumn) : DateTime.MinValue.Date; }
        }

        /// <summary>
        /// Gets or sets the weekday of the first row. Defaults to <see cref="DayOfWeek.Sunday"/>. Changing it lays the
        /// calendar out again and keeps the selected day.
        /// </summary>
        public DayOfWeek FirstDayOfWeek
        {
            get { return _FirstDayOfWeek; }
            set
            {
                DateTime selected = SelectedDate;
                _FirstDayOfWeek = value;
                Layout();
                if (_HasData)
                    Select(selected);
            }
        }

        /// <summary>
        /// Gets or sets which weekday rows carry a label. Defaults to <see cref="WeekdayLabelMode.Alternate"/>.
        /// </summary>
        public WeekdayLabelMode WeekdayLabels
        {
            get { return _WeekdayLabels; }
            set
            {
                _WeekdayLabels = value;
                Layout();
            }
        }

        /// <summary>
        /// Gets or sets the culture for month and weekday names and the default day description. Defaults to
        /// <see cref="CultureInfo.InvariantCulture"/>, for deterministic output. Must not be null.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null.</exception>
        public CultureInfo Culture
        {
            get { return _Culture; }
            set
            {
                _Culture = value ?? throw new ArgumentNullException(nameof(value));
                Layout();
            }
        }

        /// <summary>
        /// Gets or sets the number of levels. Defaults to 5 (zero plus four); 2 to 16. See <see cref="HeatMap.LevelCount"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 2 to 16.</exception>
        public int LevelCount
        {
            get { return _Map.LevelCount; }
            set
            {
                if (value < 2 || value > 16)
                    throw new ArgumentOutOfRangeException(nameof(value), "LevelCount must be 2 to 16.");
                _Map.LevelCount = value;
            }
        }

        /// <summary>
        /// Gets or sets one glyph per level, or null for the shade glyphs (see <see cref="HeatMap.LevelGlyphs"/>). Defaults
        /// to a middle dot for zero followed by the four shades, so all five default levels differ; changing
        /// <see cref="LevelCount"/> clears it.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the list does not hold exactly <see cref="LevelCount"/> entries.</exception>
        public IReadOnlyList<string>? LevelGlyphs
        {
            get { return _Map.LevelGlyphs; }
            set { _Map.LevelGlyphs = value; }
        }

        /// <summary>
        /// Gets or sets one style per level, or null for <see cref="CellColor"/>. See <see cref="HeatMap.LevelStyles"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the list does not hold exactly <see cref="LevelCount"/> entries.</exception>
        public IReadOnlyList<CellStyle>? LevelStyles
        {
            get { return _Map.LevelStyles; }
            set { _Map.LevelStyles = value; }
        }

        /// <summary>
        /// Gets or sets the color the day glyphs are drawn in when there are no level styles. Defaults to palette green.
        /// </summary>
        public Color CellColor
        {
            get { return _Map.CellColor; }
            set { _Map.CellColor = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the legend is drawn. Defaults to true.
        /// </summary>
        public bool ShowLegend
        {
            get { return _Map.ShowLegend; }
            set { _Map.ShowLegend = value; }
        }

        /// <summary>
        /// Gets or sets the width of each day cell in columns. Defaults to 2; 1 to 4.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when set outside 1 to 4.</exception>
        public int CellWidth
        {
            get { return _Map.CellWidth; }
            set { _Map.CellWidth = value; }
        }

        /// <summary>
        /// Gets or sets the description of a day drawn on the footer row, or null for the default: the date in
        /// <see cref="Culture"/>'s short date format and its value.
        /// </summary>
        public Func<DateTime, string>? DayDescription { get; set; }

        /// <summary>
        /// Gets or sets text drawn at the start of the footer row, such as a total, or null for none (the default).
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the calendar follows the theme passed to <see cref="ApplyTheme"/>.
        /// Defaults to false. See <see cref="HeatMap.UseThemeStyles"/>.
        /// </summary>
        public bool UseThemeStyles
        {
            get { return _Map.UseThemeStyles; }
            set { _Map.UseThemeStyles = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether Tab stops on the calendar. Defaults to true.
        /// </summary>
        public bool IsFocusStop { get; set; } = true;

        /// <summary>
        /// Gets the first week column shown. See <see cref="HeatMap.FirstVisibleColumn"/>.
        /// </summary>
        public int FirstVisibleWeek
        {
            get { return _Map.FirstVisibleColumn; }
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Sets the range and the values. Every date is reduced to its day; a key with a time part counts for its day,
        /// and two keys on the same day are summed (so an hourly feed totals per day). Days without a value are zero.
        /// Null values clear the data. The last day of the range is selected, so the newest week shows first.
        /// </summary>
        /// <param name="from">The first day.</param>
        /// <param name="to">The last day. Must not be before <paramref name="from"/>.</param>
        /// <param name="values">The values by day, or null to clear.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="to"/> is before <paramref name="from"/>.</exception>
        public void SetData(DateTime from, DateTime to, IReadOnlyDictionary<DateTime, double>? values)
        {
            if (to.Date < from.Date)
                throw new ArgumentException("The end of the range must not be before its start.", nameof(to));

            _Values.Clear();
            if (values == null)
            {
                _HasData = false;
                _Weeks = 0;
                _From = _To = _Start = DateTime.MinValue.Date;
                _Map.SetCells(new double[0, 0]);
                return;
            }

            foreach (KeyValuePair<DateTime, double> pair in values)
            {
                DateTime day = pair.Key.Date;
                _Values[day] = (_Values.TryGetValue(day, out double existing) ? existing : 0) + pair.Value;
            }

            _From = from.Date;
            _To = to.Date;
            _HasData = true;
            Layout();
            Select(_To);
        }

        /// <summary>
        /// Gets the value of a day (the time part is ignored), or 0 when it has none.
        /// </summary>
        /// <param name="day">The day.</param>
        /// <returns>The value.</returns>
        public double ValueOn(DateTime day)
        {
            return _Values.TryGetValue(day.Date, out double value) ? value : 0;
        }

        /// <summary>
        /// Selects a day, clamped to the range. Does nothing before <see cref="SetData"/>.
        /// </summary>
        /// <param name="day">The day.</param>
        public void Select(DateTime day)
        {
            if (!_HasData)
                return;

            DateTime date = day.Date < _From ? _From : (day.Date > _To ? _To : day.Date);
            int offset = (date - _Start).Days;
            _Map.Select(offset % 7, offset / 7);
        }

        /// <summary>
        /// Routes a key to the calendar's day navigation. Part of <see cref="IFocusable"/>.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns><c>true</c> when consumed.</returns>
        public bool HandleKey(KeyEvent key)
        {
            return _HasData && _Map.HandleKey(key);
        }

        /// <summary>
        /// Selects the day under a left press and activates it on a double click. Coordinates are widget-local.
        /// </summary>
        /// <param name="mouse">The mouse event. Must not be null.</param>
        /// <returns><c>true</c> when a day was selected.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mouse"/> is null.</exception>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null)
                throw new ArgumentNullException(nameof(mouse));

            return _HasData && _Map.HandleMouse(mouse);
        }

        /// <summary>
        /// Gets the description of the day under the pointer. Part of <see cref="ITooltipProvider"/>.
        /// </summary>
        /// <param name="x">The column, widget-local.</param>
        /// <param name="y">The row, widget-local.</param>
        /// <returns>The description, or null.</returns>
        public string? GetTooltip(int x, int y)
        {
            return _HasData ? _Map.GetTooltip(x, y) : null;
        }

        /// <summary>
        /// Records the focused state. Part of <see cref="IFocusAware"/>.
        /// </summary>
        /// <param name="focused"><c>true</c> when the calendar gained focus.</param>
        public void OnFocusChanged(bool focused)
        {
            _Map.OnFocusChanged(focused);
        }

        /// <summary>
        /// Gets the calendar's keys while focused. Part of <see cref="IKeyHintSource"/>.
        /// </summary>
        /// <returns>The hints, or null while not focused.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            if (!_Map.IsFocused)
                return null;

            List<KeyHint> hints = new List<KeyHint>();
            if (DateActivated != null)
                hints.Add(new KeyHint(new KeyChord(KeyCode.Enter, 0, KeyModifiers.None), "Open", 10));
            hints.Add(new KeyHint("Up/Down", "Day"));
            hints.Add(new KeyHint("Left/Right", "Week"));
            return hints;
        }

        /// <summary>
        /// Remembers the theme for <see cref="UseThemeStyles"/>. Part of <see cref="IThemeable"/>.
        /// </summary>
        /// <param name="theme">The theme. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="theme"/> is null.</exception>
        public void ApplyTheme(Theme theme)
        {
            _Map.ApplyTheme(theme ?? throw new ArgumentNullException(nameof(theme)));
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            if (!_HasData)
                return new Size(0, 0);

            Size map = _Map.Measure(new Size(available.Width, Math.Max(0, available.Height - 1)));
            return new Size(map.Width, Math.Min(available.Height, map.Height + 1));
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null)
                throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (!_HasData || width <= 0 || height <= 0)
                return;

            int mapHeight = Math.Max(0, height - 1);
            _Map.Render(new SurfaceView(surface, new Rect(0, 0, width, mapHeight)));
            int footer = Math.Min(height - 1, 1 + 7 + (_Map.ShowLegend ? 1 : 0));
            if (footer < 0 || footer >= height)
                return;

            string description = Describe(SelectedDate);
            string text = string.IsNullOrEmpty(Summary) ? description : Summary + "   " + description;
            surface.DrawText(0, footer, TextFit.Ellipsize(text, width), CellStyle.Default.WithForeground(Color.FromPalette(8)));
        }

        #endregion

        #region Private-Methods

        private void Layout()
        {
            if (!_HasData)
                return;

            int lead = ((int)_From.DayOfWeek - (int)_FirstDayOfWeek + 7) % 7;
            _Start = _From.AddDays(-lead);
            int days = (_To - _Start).Days + 1;
            _Weeks = (days + 6) / 7;

            double[,] cells = new double[7, _Weeks];
            for (int week = 0; week < _Weeks; week++)
            {
                for (int row = 0; row < 7; row++)
                {
                    DateTime date = _Start.AddDays(week * 7 + row);
                    cells[row, week] = InRange(date) ? ValueOn(date) : 0;
                }
            }

            _Map.SetCells(cells);
            if (_Values.Count > 0)
            {
                double max = _Values.Where(p => InRange(p.Key)).Select(p => p.Value).DefaultIfEmpty(0).Max();
                if (max > 0)
                    _Map.SetRange(0, max);
            }

            List<string> rowLabels = new List<string>();
            for (int row = 0; row < 7; row++)
            {
                bool labeled = _WeekdayLabels == WeekdayLabelMode.All || (_WeekdayLabels == WeekdayLabelMode.Alternate && row % 2 == 1);
                rowLabels.Add(labeled ? _Culture.DateTimeFormat.AbbreviatedDayNames[((int)_FirstDayOfWeek + row) % 7] : string.Empty);
            }

            _Map.SetRowLabels(_WeekdayLabels == WeekdayLabelMode.None ? new string[0] : rowLabels.ToArray());

            List<string> columnLabels = new List<string>();
            int lastMonth = -1;
            for (int week = 0; week < _Weeks; week++)
            {
                DateTime first = _Start.AddDays(week * 7);
                string label = string.Empty;
                for (int d = 0; d < 7; d++)
                {
                    DateTime date = first.AddDays(d);
                    if (!InRange(date))
                        continue;
                    if (date.Month != lastMonth && (date.Day == 1 || week == 0))
                    {
                        label = _Culture.DateTimeFormat.GetAbbreviatedMonthName(date.Month);
                        lastMonth = date.Month;
                        break;
                    }
                }

                columnLabels.Add(label);
            }

            _Map.SetColumnLabels(columnLabels);
            if (_Map.CellDescription == null)
                _Map.CellDescription = (row, column) => InRange(DateAt(row, column)) ? Describe(DateAt(row, column)) : null;
        }

        private string Describe(DateTime day)
        {
            return DayDescription != null
                ? DayDescription(day)
                : day.ToString(_Culture.DateTimeFormat.ShortDatePattern, _Culture) + ": " + ValueOn(day).ToString("0.##", _Culture);
        }

        private Point? Navigate(int row, int column, KeyEvent key)
        {
            DateTime current = DateAt(row, column);
            DateTime target;
            switch (key.Code)
            {
                case KeyCode.Up:
                    target = current.AddDays(-1);
                    break;
                case KeyCode.Down:
                    target = current.AddDays(1);
                    break;
                case KeyCode.Left:
                    target = current.AddDays(-7);
                    break;
                case KeyCode.Right:
                    target = current.AddDays(7);
                    break;
                case KeyCode.Home:
                    target = _From;
                    break;
                case KeyCode.End:
                    target = _To;
                    break;
                default:
                    return null;
            }

            if (target < _From)
                target = _From;
            if (target > _To)
                target = _To;

            int offset = (target - _Start).Days;
            return new Point(offset / 7, offset % 7);
        }

        private DateTime DateAt(int row, int column)
        {
            return _Start.AddDays(column * 7 + row);
        }

        private bool InRange(DateTime date)
        {
            return _HasData && date >= _From && date <= _To;
        }

        #endregion
    }
}
