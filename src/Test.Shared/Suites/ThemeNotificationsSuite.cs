namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite covering theme-aware widgets, Markdown, and toasts (U5) and the notification
    /// center (U6): history, actions, dismiss, wider multi-line toasts, and the history modal.
    /// </summary>
    public static class ThemeNotificationsSuite
    {
        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ThemeNotifications",
                displayName: "Theme-Aware Widgets and Notification Center",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ThemeNotifications", "WidgetsTakeTheme", "ApplyTheme copies theme roles into widget styles and containers forward it",
                        _ =>
                        {
                            Theme theme = Theme.Light;
                            ListView<string> list = new ListView<string>();
                            TextField field = new TextField();
                            TabView tabs = new TabView();
                            tabs.Add("L", list).Add("F", field);
                            tabs.ApplyTheme(theme);
                            Check.True(list.NormalStyle == theme.Text, "list text");
                            Check.True(list.HighlightColor == theme.Accent.Foreground, "list highlight");
                            Check.True(field.NormalStyle == theme.Text, "field forwarded through the tab view");
                            Check.True(tabs.ActiveStyle == theme.Selection, "tab strip");
                            Check.True(ThemeApplier.Apply(new DataTable<string>(), theme), "data table is themeable");
                            Check.False(ThemeApplier.Apply(new Label(Text.From("x")), theme), "non-themeable left alone");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ThemeNotifications", "HostAppliesTheme", "ApplyThemeToWidgets themes bound widgets and follows theme changes",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(40, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                TextField field = new TextField();
                                app.Layout = Layout.Create().Add("f", r => r.LeftAnchored(0, 20).FillHeight()).Build();
                                app.Bind("f", field);
                                Check.True(field.NormalStyle == CellStyle.Default, "untouched by default");
                                app.ApplyThemeToWidgets = true;
                                Check.True(field.NormalStyle == Theme.Dark.Text, "themed on opt-in");
                                app.Theme = Theme.HighContrast;
                                Check.True(field.NormalStyle == Theme.HighContrast.Text, "follows theme changes");
                                Check.True(app.Notifications.InfoStyle == Theme.HighContrast.Info, "toasts themed");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ThemeNotifications", "MarkdownStyles", "Markdown renders with theme-derived styles; defaults are unchanged",
                        _ =>
                        {
                            IReadOnlyList<StyledText> plain = MarkdownRenderer.Render("# Title");
                            Check.True(plain[0].Spans[0].Style.Foreground == Color.FromPalette(6), "default heading color kept");
                            Theme theme = Theme.Light;
                            IReadOnlyList<StyledText> themed = MarkdownRenderer.Render("# Title\n`code`", MarkdownStyles.FromTheme(theme));
                            Check.True(themed[0].Spans[0].Style.Foreground == theme.Accent.Foreground, "themed heading");
                            Check.True(themed[1].Spans[0].Style.Foreground == theme.Warning.Foreground, "themed inline code");
                            theme.SetStyle(Theme.MarkdownCodeRole, CellStyle.Default.WithForeground(Color.FromPalette(5)));
                            IReadOnlyList<StyledText> overridden = MarkdownRenderer.Render("```\nx\n```", MarkdownStyles.FromTheme(theme));
                            Check.True(overridden[0].Spans[0].Style.Foreground == Color.FromPalette(5), "named role override");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ThemeNotifications", "History", "Notifications stay in history after expiry and dismissal",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter { MaxConcurrent = 1, HistoryLimit = 3 };
                            Notification first = center.Add("one", NotificationSeverity.Info, 0, 100);
                            center.Add("two", NotificationSeverity.Warning, 0, 100);
                            Check.Equal(1, center.Active(0).Count, "only one toast showing");
                            Check.Equal(2, center.History.Count, "both in history");
                            Check.Equal("two", center.History[0].Text, "newest first");
                            Check.Equal(2, center.UnreadCount, "unread");
                            center.Add("three", NotificationSeverity.Info, 0, 100);
                            center.Add("four", NotificationSeverity.Info, 0, 100);
                            Check.Equal(3, center.History.Count, "history capped");
                            Check.False(new List<Notification>(center.History).Contains(first), "oldest dropped");
                            Check.Equal(0, center.Active(500).Count, "expired");
                            Check.Equal(3, center.History.Count, "expiry keeps history");
                            center.MarkAllRead();
                            Check.Equal(0, center.UnreadCount, "read");
                            center.ClearHistory();
                            Check.Equal(0, center.History.Count, "cleared");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ThemeNotifications", "ActionsAndDismiss", "Toast actions and the dismiss marker work by click",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter { ShowDismissButton = true, ToastWidth = 30 };
                            int ran = 0;
                            Notification n = center.Add("Deploy finished", NotificationSeverity.Success, 0, 0, "Deploy", new[] { new NotificationAction("View", () => ran++) });
                            CellBuffer screen = new CellBuffer(40, 8);
                            center.Render(new BufferSurface(screen), 0);
                            string text = Snapshot.ToText(screen);
                            Check.True(text.Contains("Deploy") && text.Contains("[View]") && text.Contains("x"), "title, action, dismiss drawn");
                            int actionRow = -1;
                            int actionCol = -1;
                            for (int y = 0; y < 8 && actionRow < 0; y++)
                            {
                                for (int x = 0; x < 40; x++)
                                {
                                    if (screen.Get(x, y).Grapheme == "[" && screen.Get(x + 1, y).Grapheme == "V")
                                    {
                                        actionRow = y;
                                        actionCol = x;
                                        break;
                                    }
                                }
                            }

                            Check.True(center.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, actionCol + 1, actionRow, KeyModifiers.None, 1)), "action click handled");
                            Check.Equal(1, ran, "action ran");
                            Check.True(n.IsDismissed && n.IsRead, "acting dismissed the toast");
                            Check.Equal(0, center.Active(0).Count, "no toast left");

                            Notification other = center.Add("Second", NotificationSeverity.Info, 0, 0);
                            center.Render(new BufferSurface(new CellBuffer(40, 8)), 0);
                            Check.True(center.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 37, 0, KeyModifiers.None, 1)), "dismiss marker click");
                            Check.True(other.IsDismissed, "dismissed");
                            Check.False(center.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 0, 7, KeyModifiers.None, 1)), "clicks elsewhere pass through");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ThemeNotifications", "WideMultilineToasts", "Toasts wrap over MaxToastLines at ToastWidth; defaults stay single-line",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            center.Add("alpha beta gamma delta epsilon zeta eta theta iota kappa lambda", NotificationSeverity.Info, 0, 0);
                            CellBuffer narrow = new CellBuffer(80, 6);
                            center.Render(new BufferSurface(narrow), 0);
                            Check.True(Snapshot.ToText(narrow).Split('\n')[1].Trim().Length == 0, "default toast is one line");

                            center.MaxToastLines = 3;
                            center.ToastWidth = 24;
                            CellBuffer wide = new CellBuffer(80, 6);
                            center.Render(new BufferSurface(wide), 0);
                            string[] rows = Snapshot.ToText(wide).Split('\n');
                            Check.True(rows[1].Trim().Length > 0 && rows[2].Trim().Length > 0, "wrapped onto three lines");
                            Check.True(rows[0].TrimEnd().Length <= 80 && rows[0].IndexOf("alpha", StringComparison.Ordinal) >= 80 - 24, "placed at the right edge within the width");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ThemeNotifications", "HostToastClick", "The host routes clicks on toast actions before widgets",
                        _ =>
                        {
                            HeadlessBackend backend = new HeadlessBackend(60, 10);
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                RecordingMouseWidget under = new RecordingMouseWidget();
                                app.Layout = Layout.Create().Add("w", r => r.LeftAnchored(0, 40).FillHeight()).Build();
                                app.Bind("w", under);
                                int ran = 0;
                                app.Notifications.DismissOnClick = true;
                                app.Notify("hello", NotificationSeverity.Info, null, 0, new NotificationAction("Go", () => ran++));
                                app.Start();
                                app.RenderOnce();
                                backend.FeedInput("\u001b[<0;58;1M");
                                app.PumpInputOnce();
                                Check.True(app.Notifications.Active(app.NowMilliseconds).Count == 0, "click on the toast dismissed it");
                                Check.Equal(0, ran, "body click does not run the action");
                                bool pressReachedWidget = false;
                                for (int i = 0; i < under.Events.Count; i++)
                                    pressReachedWidget |= under.Events[i].Kind == MouseEventKind.Press;
                                Check.False(pressReachedWidget, "the widget below never saw the press");
                                app.Stop();
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ThemeNotifications", "HistoryModal", "The history modal lists, deletes, and runs actions",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            int ran = 0;
                            center.Add("first", NotificationSeverity.Info, 0, 0);
                            center.Add("second", NotificationSeverity.Error, 0, 0, null, new[] { new NotificationAction("Retry", () => ran++) });
                            NotificationHistoryModal modal = new NotificationHistoryModal(center, () => 5000);
                            CellBuffer screen = new CellBuffer(80, 20);
                            modal.Render(new BufferSurface(screen));
                            string text = Snapshot.ToText(screen);
                            Check.True(text.Contains("second") && text.Contains("first") && text.Contains("[error]"), "listed");
                            Check.Equal(0, center.UnreadCount, "opening marks read");
                            modal.HandleKey(KeyEvent.Special(KeyCode.Down));
                            modal.HandleKey(KeyEvent.Char('d'));
                            Check.Equal(1, center.History.Count, "deleted the selected entry");
                            modal.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.Equal(1, ran, "Enter ran the first action");
                            Check.True(modal.IsClosed, "closed after acting");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
