namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Modals;
    using TUIKit.Terminal;
    using TUIKit.Testing;
    using N = TUIKit.Diagnostics.TuiKitTelemetryNames;

    /// <summary>
    /// Coverage for toast coalescing by key and content, severity labels, the newest action, and the top
    /// offset (1.5.0, B5): <see cref="NotificationCenter.CoalesceBy"/>, <see cref="NotificationAction.Key"/>,
    /// <see cref="NotificationOptions"/>, <see cref="NotificationCenter.ShowSeverityLabels"/>,
    /// <see cref="NotificationCenter.SeverityLabels"/>, <see cref="NotificationCenter.InvokeLatestAction"/>,
    /// and <see cref="NotificationCenter.TopOffset"/>.
    /// </summary>
    public static class ToastKeySuite
    {
        /// <summary>
        /// Builds the toast key suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ToastKey",
                displayName: "Toasts: Coalesce by Key, Severity Text, Newest Action, Top Offset",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ToastKey", "FreshLambdasCoalesce", "Three raises with new action instances of the same label coalesce, and the action runs the newest callback",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            List<int> opened = new List<int>();
                            Notification? last = null;
                            for (int i = 1; i <= 3; i++)
                            {
                                int id = i;
                                last = center.Add("Saved", NotificationSeverity.Success, i * 10, null, null, new[] { new NotificationAction("Open", () => opened.Add(id)) });
                            }

                            Check.Equal(1, center.Active(30).Count, "one toast");
                            Check.Equal(3, last!.RepeatCount, "RepeatCount 3");
                            center.InvokeAction(last, 0);
                            Check.Equal(1, opened.Count, "one callback ran");
                            Check.Equal(3, opened[0], "the newest callback");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ToastKey", "ActionKeysSeparate", "Same text and label but different action keys stay two toasts; without keys they merge to the newest target",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            center.Add("Mission done", NotificationSeverity.Info, 0, null, null, new[] { new NotificationAction("Open", () => { }, "msn_1") });
                            center.Add("Mission done", NotificationSeverity.Info, 1, null, null, new[] { new NotificationAction("Open", () => { }, "msn_2") });
                            Check.Equal(2, center.Active(1).Count, "distinct keys stay separate");
                            center.Add("Mission done", NotificationSeverity.Info, 2, null, null, new[] { new NotificationAction("Open", () => { }, "msn_1") });
                            Check.Equal(2, center.Active(2).Count, "a matching key merges");

                            NotificationCenter unkeyed = new NotificationCenter();
                            string target = string.Empty;
                            unkeyed.Add("Mission done", NotificationSeverity.Info, 0, null, null, new[] { new NotificationAction("Open", () => target = "msn_1") });
                            Notification merged = unkeyed.Add("Mission done", NotificationSeverity.Info, 1, null, null, new[] { new NotificationAction("Open", () => target = "msn_2") });
                            Check.Equal(1, unkeyed.Active(1).Count, "without keys they merge");
                            unkeyed.InvokeAction(merged, 0);
                            Check.Equal("msn_2", target, "the newest target survives");
                            Check.True(new NotificationAction("Open", () => { }, string.Empty).Key == null, "an empty key is null");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ToastKey", "CoalesceKeyGroups", "The same coalesce key with different text, severity, and title coalesces and shows the newest content",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            Notification first = center.Add("Indexing 1 of 3", NotificationSeverity.Info, 0, new NotificationOptions { CoalesceKey = "index" });
                            center.Add("Unrelated", NotificationSeverity.Info, 5);
                            Notification second = center.Add("Indexing 3 of 3", NotificationSeverity.Success, 10, new NotificationOptions { CoalesceKey = "index", Title = "Done" });
                            Check.True(ReferenceEquals(first, second), "same notification");
                            Check.Equal("Indexing 3 of 3", second.Text, "newest text");
                            Check.Equal(NotificationSeverity.Success, second.Severity, "newest severity");
                            Check.Equal("Done", second.Title, "newest title");
                            Check.Equal(0L, second.CreatedAtMilliseconds, "creation time kept");
                            Check.Equal("index", second.CoalesceKey, "key recorded");
                            Check.Equal(2, center.Active(10).Count, "the unrelated toast is separate");

                            center.Add("Indexing 3 of 3", NotificationSeverity.Success, 11, null, "Done", null);
                            Check.Equal(3, center.Active(11).Count, "an unkeyed raise never merges into a keyed toast");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ToastKey", "InstanceRuleReproduces14", "CoalesceBy ContentAndActionInstances reproduces 1.4.0, keeping the original callback",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter { CoalesceBy = CoalesceMatch.ContentAndActionInstances };
                            int ran = 0;
                            NotificationAction shared = new NotificationAction("Open", () => ran = 1);
                            center.Add("Saved", NotificationSeverity.Info, 0, null, null, new[] { shared });
                            center.Add("Saved", NotificationSeverity.Info, 1, null, null, new[] { new NotificationAction("Open", () => ran = 2) });
                            Check.Equal(2, center.Active(1).Count, "fresh instances do not merge");
                            Notification repeat = center.Add("Saved", NotificationSeverity.Info, 2, null, null, new[] { shared });
                            Check.Equal(2, repeat.RepeatCount, "the same instance merges");
                            center.InvokeAction(repeat, 0);
                            Check.Equal(1, ran, "the original callback");
                            Check.Equal(CoalesceMatch.Content, new NotificationCenter().CoalesceBy, "Content is the default");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ToastKey", "SeverityLabels", "With labels on, toasts differ by text and not only color; with them off, output matches 1.4.0",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter { ShowSeverityLabels = true };
                            center.Add("Saved", NotificationSeverity.Success, 0);
                            center.Add("Disk full", NotificationSeverity.Error, 0);
                            CellBuffer buffer = new CellBuffer(50, 4);
                            center.Render(new BufferSurface(buffer), 0);
                            string text = Snapshot.ToText(buffer);
                            Check.True(text.Contains("[ok] Saved"), "success label");
                            Check.True(text.Contains("[x] Disk full"), "error label");

                            Dictionary<NotificationSeverity, string> custom = new Dictionary<NotificationSeverity, string>
                            {
                                { NotificationSeverity.Info, "INFO" },
                                { NotificationSeverity.Success, "OK" },
                                { NotificationSeverity.Warning, "WARN" },
                                { NotificationSeverity.Error, string.Empty }
                            };
                            center.SeverityLabels = custom;
                            custom[NotificationSeverity.Success] = "changed";
                            buffer = new CellBuffer(50, 4);
                            center.Render(new BufferSurface(buffer), 0);
                            text = Snapshot.ToText(buffer);
                            Check.True(text.Contains("OK Saved"), "custom label, copied when set");
                            Check.True(text.Contains(" Disk full") && !text.Contains("[x]"), "an empty label shows no prefix");

                            NotificationCenter plain = new NotificationCenter();
                            plain.Add("Saved", NotificationSeverity.Success, 0);
                            CellBuffer plainBuffer = new CellBuffer(50, 2);
                            plain.Render(new BufferSurface(plainBuffer), 0);
                            Check.Equal(" Saved", Snapshot.ToText(plainBuffer).Split('\n')[0].Substring(9), "labels off: the 1.4.0 text");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ToastKey", "InvokeLatestAction", "InvokeLatestAction runs the newest toast's first action, and returns false when none are active",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            Check.False(center.InvokeLatestAction(0), "nothing active");
                            string ran = string.Empty;
                            center.Add("First", NotificationSeverity.Info, 0, null, null, new[] { new NotificationAction("Open", () => ran = "first") });
                            center.Add("Second", NotificationSeverity.Info, 1, null, null, new[] { new NotificationAction("Open", () => ran = "second"), new NotificationAction("Skip", () => ran = "skip") });
                            center.Add("No actions", NotificationSeverity.Info, 2);
                            Check.True(center.InvokeLatestAction(2), "ran");
                            Check.Equal("second", ran, "the newest toast with an action, first action");
                            Check.True(center.InvokeLatestAction(2), "ran again");
                            Check.Equal("first", ran, "the second toast was dismissed, so the first is next");
                            Check.False(center.InvokeLatestAction(2), "only the action-less toast is left");

                            using (TuiApplication app = new TuiApplication(new HeadlessBackend()))
                            {
                                int hits = 0;
                                app.Notify("Build failed", NotificationSeverity.Error, new NotificationOptions { Actions = new[] { new NotificationAction("Logs", () => hits++) } });
                                Check.True(app.InvokeLatestNotificationAction(), "host helper ran");
                                Check.Equal(1, hits, "callback ran once");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ToastKey", "TopOffset", "TopOffset 2 leaves rows 0 and 1 untouched",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter { TopOffset = 2 };
                            center.Add("Hello", NotificationSeverity.Info, 0);
                            CellBuffer buffer = new CellBuffer(50, 5);
                            center.Render(new BufferSurface(buffer), 0);
                            string[] rows = Snapshot.ToText(buffer).Split('\n');
                            Check.Equal(string.Empty, rows[0], "row 0 untouched");
                            Check.Equal(string.Empty, rows[1], "row 1 untouched");
                            Check.True(rows[2].Contains("Hello"), "toast starts at row 2");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ToastKey", "CoalescedTelemetry", "tuikit.notifications.coalesced counts key- and label-based merges",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                NotificationCenter center = new NotificationCenter();
                                center.Add("p", NotificationSeverity.Info, 0, new NotificationOptions { CoalesceKey = "k" });
                                center.Add("q", NotificationSeverity.Info, 1, new NotificationOptions { CoalesceKey = "k" });
                                center.Add("Saved", NotificationSeverity.Info, 0, null, null, new[] { new NotificationAction("Open", () => { }) });
                                center.Add("Saved", NotificationSeverity.Info, 1, null, null, new[] { new NotificationAction("Open", () => { }) });
                                Check.Equal(2.0, capture.Sum(N.NotificationsCoalesced), "two merges counted");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ToastKey", "Guards", "Out-of-range offsets, incomplete label maps, and null options are rejected",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            Check.Throws<ArgumentOutOfRangeException>(() => center.TopOffset = -1, "offset -1");
                            Check.Throws<ArgumentOutOfRangeException>(() => center.TopOffset = 11, "offset 11");
                            center.TopOffset = 10;
                            Check.Throws<ArgumentException>(() => center.SeverityLabels = new Dictionary<NotificationSeverity, string> { { NotificationSeverity.Info, "i" } }, "missing severities");
                            Check.Throws<ArgumentNullException>(() => center.SeverityLabels = null!, "null map");
                            Dictionary<NotificationSeverity, string> withNull = new Dictionary<NotificationSeverity, string>
                            {
                                { NotificationSeverity.Info, "i" },
                                { NotificationSeverity.Success, null! },
                                { NotificationSeverity.Warning, "w" },
                                { NotificationSeverity.Error, "e" }
                            };
                            Check.Throws<ArgumentNullException>(() => center.SeverityLabels = withNull, "null label");
                            Check.Throws<ArgumentNullException>(() => center.Add("x", NotificationSeverity.Info, 0, (NotificationOptions)null!), "null options");
                            Check.Throws<ArgumentNullException>(() => center.Add(null!, NotificationSeverity.Info, 0, new NotificationOptions()), "null text");
                            Check.Throws<ArgumentOutOfRangeException>(() => new NotificationOptions { TimeoutMilliseconds = -1 }, "negative timeout");
                            Check.Throws<ArgumentNullException>(() => center.Add("x", NotificationSeverity.Info, 0, new NotificationOptions { Actions = new NotificationAction[] { null! } }), "null action");
                            Check.False(center.ShowSeverityLabels, "labels off by default");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
