namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Modals;
    using TUIKit.Testing;
    using N = TUIKit.Diagnostics.TuiKitTelemetryNames;

    /// <summary>
    /// Coverage for notification coalescing: identical raises refresh one toast with a repeat count
    /// instead of stacking duplicates, distinct raises stay separate, and the format and opt-out work.
    /// All timing uses explicit clock values.
    /// </summary>
    public static class NotificationCoalescingSuite
    {
        /// <summary>
        /// Builds the notification coalescing suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "NotificationCoalescing",
                displayName: "Notification Coalescing",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("NotificationCoalescing", "IdenticalRaisesMerge", "Three identical raises make one toast with a repeat count and a refreshed timeout",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            Notification first = center.Add("Saved", NotificationSeverity.Info, 0, 1000);
                            Notification second = center.Add("Saved", NotificationSeverity.Info, 400, 1000);
                            Notification third = center.Add("Saved", NotificationSeverity.Info, 800, 1000);

                            Check.True(ReferenceEquals(first, second) && ReferenceEquals(first, third), "the same instance is returned");
                            Check.Equal(3, first.RepeatCount, "repeat count");
                            Check.Equal(800L, first.LastRaisedAtMilliseconds, "last raise time");
                            Check.Equal(1, center.Active(800).Count, "one active toast");
                            Check.Equal(1, center.History.Count, "one history entry");
                            Check.False(first.IsExpired(1700), "still showing 900 ms after the last raise");
                            Check.True(first.IsExpired(1800), "expires a full timeout after the last raise");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NotificationCoalescing", "SuffixRenders", "A repeated toast shows the repeat suffix",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            center.Add("Saved", NotificationSeverity.Info, 0);
                            center.Add("Saved", NotificationSeverity.Info, 10);
                            center.Add("Saved", NotificationSeverity.Info, 20);

                            CellBuffer buffer = new CellBuffer(50, 4);
                            center.Render(new BufferSurface(buffer), 20);
                            Check.True(Snapshot.ToText(buffer).Contains("Saved (x3)"), "suffix rendered");

                            center.RepeatSuffixFormat = " [{0}]";
                            buffer = new CellBuffer(50, 4);
                            center.Render(new BufferSurface(buffer), 20);
                            Check.True(Snapshot.ToText(buffer).Contains("Saved [3]"), "custom suffix rendered");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NotificationCoalescing", "DistinctRaisesStaySeparate", "A different severity, text, title, or action set makes a separate toast",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter { CoalesceBy = CoalesceMatch.ContentAndActionInstances };
                            center.MaxConcurrent = 10;
                            NotificationAction undo = new NotificationAction("Undo", () => { });
                            NotificationAction undoAgain = new NotificationAction("Undo", () => { });

                            center.Add("Saved", NotificationSeverity.Info, 0);
                            center.Add("Saved", NotificationSeverity.Warning, 0);
                            center.Add("Saved!", NotificationSeverity.Info, 0);
                            center.Add("Saved", NotificationSeverity.Info, 0, null, "File", null);
                            center.Add("Saved", NotificationSeverity.Info, 0, null, null, new[] { undo });
                            center.Add("Saved", NotificationSeverity.Info, 0, null, null, new[] { undoAgain });
                            Check.Equal(6, center.Active(0).Count, "six distinct toasts under the 1.4.0 instance rule");

                            NotificationCenter byContent = new NotificationCenter { MaxConcurrent = 10 };
                            byContent.Add("Saved", NotificationSeverity.Info, 0, null, null, new[] { undo });
                            byContent.Add("Saved", NotificationSeverity.Info, 0, null, null, new[] { undoAgain });
                            Check.Equal(1, byContent.Active(0).Count, "the 1.5.0 default merges actions with the same label");

                            Notification merged = center.Add("Saved", NotificationSeverity.Info, 5, null, null, new[] { undo });
                            Check.Equal(2, merged.RepeatCount, "the same action instance merges");
                            Check.Equal(6, center.Active(5).Count, "still six toasts");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NotificationCoalescing", "MovesToNewestAndUnread", "A coalesced toast moves to the newest position and becomes unread",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            Notification saved = center.Add("Saved", NotificationSeverity.Info, 0);
                            center.Add("Loaded", NotificationSeverity.Info, 1);
                            center.MarkAllRead();
                            Check.True(saved.IsRead, "read before the repeat");

                            center.Add("Saved", NotificationSeverity.Info, 2);
                            IReadOnlyList<Notification> active = center.Active(2);
                            Check.True(ReferenceEquals(saved, active[0]), "repeat is now the newest toast");
                            Check.False(saved.IsRead, "repeat marks it unread");
                            Check.Equal(1, center.UnreadCount, "one unread");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NotificationCoalescing", "ExpiredDoesNotMerge", "An identical raise after the first expired creates a new toast",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            Notification first = center.Add("Saved", NotificationSeverity.Info, 0, 100);
                            Notification second = center.Add("Saved", NotificationSeverity.Info, 500, 100);
                            Check.False(ReferenceEquals(first, second), "new instance after expiry");
                            Check.Equal(1, second.RepeatCount, "new toast starts at 1");

                            Notification dismissed = center.Add("Done", NotificationSeverity.Info, 0);
                            center.Dismiss(dismissed);
                            Notification fresh = center.Add("Done", NotificationSeverity.Info, 1);
                            Check.False(ReferenceEquals(dismissed, fresh), "a dismissed toast is not revived");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NotificationCoalescing", "OptOutStacks", "With CoalesceRepeats off, identical raises stack as before",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            center.CoalesceRepeats = false;
                            center.Add("Saved", NotificationSeverity.Info, 0);
                            center.Add("Saved", NotificationSeverity.Info, 1);
                            Check.Equal(2, center.Active(1).Count, "two toasts");
                            Check.Equal(1, center.Active(1)[0].RepeatCount, "no repeat count");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NotificationCoalescing", "AdjacentRowsNoBlankLines", "Stacked toasts occupy adjacent rows and stray newlines add no empty row",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            center.MaxToastLines = 3;
                            center.Add("\nfirst\n", NotificationSeverity.Info, 0);
                            center.Add("second\n\n", NotificationSeverity.Info, 0);

                            CellBuffer buffer = new CellBuffer(50, 6);
                            center.Render(new BufferSurface(buffer), 0);
                            string[] rows = Snapshot.ToText(buffer).Split('\n');
                            Check.True(rows[0].Contains("second"), "newest toast on row 0");
                            Check.True(rows[1].Contains("first"), "older toast directly below on row 1");
                            Check.Equal(string.Empty, rows[2].Trim(), "nothing after the two toasts");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NotificationCoalescing", "HistoryShowsRepeatCount", "The history modal lists the repeat count",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            center.Add("Saved", NotificationSeverity.Info, 0);
                            center.Add("Saved", NotificationSeverity.Info, 1);
                            NotificationHistoryModal modal = new NotificationHistoryModal(center, () => 1);
                            CellBuffer buffer = new CellBuffer(70, 12);
                            modal.Render(new BufferSurface(buffer));
                            Check.True(Snapshot.ToText(buffer).Contains("Saved (x2)"), "history row shows the count");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NotificationCoalescing", "FormatGuards", "RepeatSuffixFormat rejects null, a missing {0}, and a bad format",
                        _ =>
                        {
                            NotificationCenter center = new NotificationCenter();
                            Check.Throws<ArgumentNullException>(() => center.RepeatSuffixFormat = null!, "null format");
                            Check.Throws<ArgumentException>(() => center.RepeatSuffixFormat = " (x)", "no placeholder");
                            Check.Throws<ArgumentException>(() => center.RepeatSuffixFormat = " {0} {", "malformed format");
                            Check.Equal(" (x{0})", center.RepeatSuffixFormat, "default unchanged after rejected sets");
                            Check.Throws<ArgumentNullException>(() => center.Add("x", NotificationSeverity.Info, 0, null, null, new NotificationAction[] { null! }), "null action");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("NotificationCoalescing", "CoalescedCounter", "Coalesced raises are counted by severity",
                        _ =>
                        {
                            using (TelemetryCapture capture = new TelemetryCapture())
                            {
                                NotificationCenter center = new NotificationCenter();
                                center.Add("Saved", NotificationSeverity.Success, 0);
                                center.Add("Saved", NotificationSeverity.Success, 1);
                                center.Add("Saved", NotificationSeverity.Success, 2);
                                Check.Equal(2.0, capture.Sum(N.NotificationsCoalesced, N.AttrSeverity, "success"), "two coalesced raises");
                                Check.Equal(3.0, capture.Sum(N.Notifications, N.AttrSeverity, "success"), "every raise still counted");
                            }

                            return Task.CompletedTask;
                        })
                });
        }
    }
}
