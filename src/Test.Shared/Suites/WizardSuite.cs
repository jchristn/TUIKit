namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.9 item C7: <see cref="Wizard"/>, <see cref="WizardStep"/>, the step events, navigation, layout,
    /// focus, keys, and theme.
    /// </summary>
    public static class WizardSuite
    {
        private const string _Id = "Wizard";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Wizard",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "Guards", "Constructor and step guards reject null and empty input",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new Wizard(null!), "null steps");
                            Check.Throws<ArgumentException>(() => new Wizard(new WizardStep[0]), "no steps");
                            Check.Throws<ArgumentException>(() => new Wizard(new WizardStep[] { null! }), "null step");
                            Check.Throws<ArgumentNullException>(() => new WizardStep(null!, new TextField()), "null title");
                            Check.Throws<ArgumentNullException>(() => new WizardStep("x", null!), "null content");
                            Wizard wizard = Three(out TextField _, out TextField _, out TextField _);
                            Check.Throws<ArgumentOutOfRangeException>(() => wizard.GoTo(9), "bad index");
                            Check.Throws<ArgumentException>(() => wizard.HeaderFormat = "{3}", "bad header format");
                            Check.Throws<ArgumentNullException>(() => wizard.NextLabel = null!, "null label");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "HeaderAndList", "The header and step list read right on the first, middle, and last steps",
                        _ =>
                        {
                            Wizard wizard = Three(out TextField first, out TextField _, out TextField _);
                            first.Value = "ada";
                            string[] lines = Snapshot.RenderWidget(wizard, 50, 12).Split('\n');
                            Check.Equal("Step 1 of 3: Account", lines[0], "first header");
                            Check.Equal("> Account    Repository    Review", lines[1], "first list");
                            Check.True(lines.Last(l => l.Length > 0).Contains("Back") && lines.Last(l => l.Length > 0).Contains("Next"), "buttons");
                            wizard.Next();
                            lines = Snapshot.RenderWidget(wizard, 50, 12).Split('\n');
                            Check.Equal("Step 2 of 3: Repository", lines[0], "middle header");
                            Check.True(lines[1].StartsWith("✓ Account  > Repository", StringComparison.Ordinal), "done and current: " + lines[1]);
                            wizard.Next();
                            Check.True(wizard.IsLast, "last");
                            Check.True(Snapshot.RenderWidget(wizard, 50, 12).Contains("Finish"), "Finish on the last step");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Validation", "Next blocked by validation shows the error and stays; passing moves and clears it; the last step raises Finished",
                        _ =>
                        {
                            Wizard wizard = Three(out TextField name, out TextField _, out TextField _);
                            Check.False(wizard.Next(), "blocked");
                            Check.Equal(0, wizard.CurrentIndex, "stayed");
                            Check.Equal("Name is required", wizard.Error, "error");
                            Check.True(Snapshot.RenderWidget(wizard, 50, 12).Contains("! Name is required"), "error shown");
                            name.Value = "ada";
                            Check.True(wizard.Next(), "moved");
                            Check.True(wizard.Error == null, "cleared");
                            int finished = 0;
                            wizard.Finished += (s, e) => finished++;
                            wizard.Next();
                            Check.True(wizard.Next(), "finish");
                            Check.Equal(1, finished, "Finished raised");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "BackSkipGoTo", "Back never validates; Skip only on optional steps; GoTo rules with and without AllowJumpAhead",
                        _ =>
                        {
                            Wizard wizard = Three(out TextField name, out TextField repo, out TextField _);
                            Check.False(wizard.Back(), "no Back on the first step");
                            Check.False(wizard.Skip(), "Account is required");
                            Check.False(wizard.GoTo(2), "Review not visited yet");
                            name.Value = "ada";
                            wizard.Next();
                            Check.True(wizard.Skip(), "Repository is optional");
                            Check.Equal(2, wizard.CurrentIndex, "skipped to Review");
                            Check.True(wizard.Back(), "Back");
                            Check.True(wizard.Back(), "Back without validating");
                            Check.Equal(0, wizard.CurrentIndex, "on Account");
                            name.Value = string.Empty;
                            Check.False(wizard.GoTo(2), "forward jump validates Account");
                            Check.Equal("Name is required", wizard.Error, "stopped with the error");
                            name.Value = "ada";
                            Check.True(wizard.GoTo(2), "visited and valid");
                            Wizard jump = Three(out TextField _, out TextField _, out TextField _);
                            jump.AllowJumpAhead = true;
                            Check.True(jump.GoTo(2), "AllowJumpAhead skips the rules");
                            Check.True(jump.GoTo(0), "backward always");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Applicability", "A step whose IsApplicable is false is skipped both ways and hidden from the list and count",
                        _ =>
                        {
                            Wizard wizard = Three(out TextField name, out TextField _, out TextField _);
                            bool importing = false;
                            wizard.Steps[1].IsApplicable = () => importing;
                            name.Value = "ada";
                            string[] lines = Snapshot.RenderWidget(wizard, 50, 12).Split('\n');
                            Check.Equal("Step 1 of 2: Account", lines[0], "count without the step");
                            Check.False(lines[1].Contains("Repository"), "hidden from the list");
                            wizard.Next();
                            Check.Equal(2, wizard.CurrentIndex, "skipped forward");
                            wizard.Back();
                            Check.Equal(0, wizard.CurrentIndex, "skipped backward");
                            importing = true;
                            wizard.Next();
                            Check.Equal(1, wizard.CurrentIndex, "applies again");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Events", "StepChanging can veto a move; StepChanged reports from and to",
                        _ =>
                        {
                            Wizard wizard = Three(out TextField name, out TextField _, out TextField _);
                            name.Value = "ada";
                            List<string> changed = new List<string>();
                            wizard.StepChanged += (s, e) => changed.Add(e.From + ">" + e.To);
                            bool veto = true;
                            wizard.StepChanging += (s, e) => e.Cancel = veto;
                            Check.False(wizard.Next(), "vetoed");
                            Check.Equal(0, wizard.CurrentIndex, "stayed");
                            veto = false;
                            wizard.Next();
                            Check.Equal("0>1", string.Join(",", changed), "StepChanged");
                            int cancelled = 0;
                            wizard.Cancelled += (s, e) => cancelled++;
                            wizard.Cancel();
                            Check.Equal(1, cancelled, "Cancelled");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Focus", "Focus lands in the step content; Tab reaches the buttons; Back is disabled on the first step",
                        _ =>
                        {
                            Wizard wizard = Three(out TextField name, out TextField _, out TextField _);
                            Check.True(ReferenceEquals(wizard.FocusedLeaf, name), "content focused on entry");
                            Check.False(wizard.Buttons.Buttons[0].IsEnabled, "Back disabled");
                            wizard.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.True(wizard.FocusedLeaf is Button, "Tab reaches the buttons");
                            Check.True(wizard.HandlePaste("x") == false, "a button refuses a paste");
                            wizard.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.Equal("Cancel", ((Button)wizard.FocusedLeaf!).Label, "Next, then Cancel");
                            wizard.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.True(ReferenceEquals(wizard.FocusedLeaf, name), "wraps to the content");
                            Check.True(wizard.HandlePaste("ada"), "paste to the field");
                            Check.Equal("ada", name.Value, "pasted");
                            Check.Throws<ArgumentNullException>(() => wizard.HandlePaste(null!), "null paste");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Keys", "Alt+Right and Alt+Left, and Alt+F and Alt+B, move steps; null keys disable them",
                        _ =>
                        {
                            Wizard wizard = Three(out TextField name, out TextField _, out TextField _);
                            name.Value = "ada";
                            Check.True(wizard.HandleKey(KeyEvent.Special(KeyCode.Right, KeyModifiers.Alt)), "Alt+Right");
                            Check.Equal(1, wizard.CurrentIndex, "moved");
                            Check.True(wizard.HandleKey(KeyEvent.Char('b', KeyModifiers.Alt)), "Alt+B (Option+Left on macOS)");
                            Check.Equal(0, wizard.CurrentIndex, "back");
                            Check.True(wizard.HandleKey(KeyEvent.Char('f', KeyModifiers.Alt)), "Alt+F");
                            Check.Equal("ada", name.Value, "Alt+F typed nothing into the field");
                            wizard.HandleKey(KeyEvent.Special(KeyCode.Left, KeyModifiers.Alt));
                            Check.Equal(0, wizard.CurrentIndex, "Alt+Left");
                            Check.True(wizard.GetKeyHints()!.Any(h => h.Description == "Next step"), "hint");
                            wizard.NextStepKey = null;
                            wizard.NextStepAltKey = null;
                            wizard.HandleKey(KeyEvent.Special(KeyCode.Right, KeyModifiers.Alt));
                            Check.Equal(0, wizard.CurrentIndex, "disabled");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Theme", "The theme styles the header and list, and an ASCII theme uses ASCII glyphs",
                        _ =>
                        {
                            Wizard wizard = Three(out TextField name, out TextField _, out TextField _);
                            name.Value = "ada";
                            wizard.Next();
                            Theme ascii = Theme.HighContrast;
                            wizard.ApplyTheme(ascii);
                            CellBuffer buffer = MarkdownThemeSuite.Draw(wizard, 50, 12);
                            Check.Equal(ascii.Accent.WithAttribute(CellAttributes.Bold, true), buffer.Get(0, 0).Style, "header from Accent");
                            Check.True(Snapshot.ToText(buffer).Split('\n')[1].StartsWith("x Account", StringComparison.Ordinal), "ASCII done glyph");
                            Check.Throws<ArgumentNullException>(() => wizard.ApplyTheme(null!), "null theme");
                            wizard.StepListPlacement = WizardStepListPlacement.Left;
                            Check.True(Snapshot.RenderWidget(wizard, 50, 12).Split('\n')[2].StartsWith("> Repository", StringComparison.Ordinal), "left list");
                            wizard.StepListPlacement = WizardStepListPlacement.None;
                            Check.False(Snapshot.RenderWidget(wizard, 50, 12).Contains("Account"), "no list");
                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(_Id, "MouseAndLabels", "Clicks on the step list, the content, and the buttons route correctly; labels are localizable",
                        _ =>
                        {
                            Wizard wizard = Three(out TextField name, out TextField _, out TextField _);
                            name.Value = "ada";
                            wizard.Next();
                            wizard.BackLabel = "Zurueck";
                            wizard.NextLabel = "Weiter";
                            wizard.CancelLabel = "Abbrechen";
                            wizard.SkipLabel = "Ueberspringen";
                            wizard.FinishLabel = "Fertig";
                            string[] lines = Snapshot.RenderWidget(wizard, 70, 12).Split('\n');
                            int buttonsRow = Array.FindIndex(lines, l => l.Contains("Weiter"));
                            Check.True(buttonsRow > 0 && lines[buttonsRow].Contains("Ueberspringen"), "localized buttons on an optional step");
                            Check.True(wizard.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 1, 1, KeyModifiers.None, 1)), "click on the list");
                            Check.Equal(0, wizard.CurrentIndex, "jumped back to Account");
                            Snapshot.RenderWidget(wizard, 70, 12);
                            Check.True(wizard.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 1, 3, KeyModifiers.None, 1)), "click in the content");
                            Check.True(ReferenceEquals(wizard.FocusedLeaf, name), "content focused");
                            lines = Snapshot.RenderWidget(wizard, 70, 12).Split('\n');
                            buttonsRow = Array.FindIndex(lines, l => l.Contains("Weiter"));
                            int x = lines[buttonsRow].IndexOf("Weiter", StringComparison.Ordinal);
                            wizard.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, x + 1, buttonsRow, KeyModifiers.None, 1));
                            wizard.HandleMouse(new MouseEvent(MouseEventKind.Release, MouseButton.Left, x + 1, buttonsRow, KeyModifiers.None, 1));
                            Check.Equal(1, wizard.CurrentIndex, "Next button clicked");
                            Check.False(wizard.HandleMouse(new MouseEvent(MouseEventKind.Press, MouseButton.Left, 60, 0, KeyModifiers.None, 1)), "header click ignored");
                            Check.Throws<ArgumentNullException>(() => wizard.HandleMouse(null!), "null mouse");
                            Check.Throws<ArgumentNullException>(() => wizard.DoneGlyph = null!, "null glyph");
                            wizard.CurrentGlyph = "*";
                            wizard.PendingGlyph = "-";
                            wizard.DoneGlyph = "+";
                            Check.True(Snapshot.RenderWidget(wizard, 70, 12).Split('\n')[1].StartsWith("+ Account  * Repository  - Review", StringComparison.Ordinal), "custom glyphs");
                            wizard.HeaderFormat = "{2} ({0}/{1})";
                            Check.True(Snapshot.RenderWidget(wizard, 70, 12).StartsWith("Repository (2/3)", StringComparison.Ordinal), "custom header");
                            wizard.OnFocusChanged(true);
                            Check.True(wizard.MoveFocus(true), "MoveFocus into the buttons");
                            wizard.FocusEdge(true);
                            Check.True(wizard.FocusedChild != null, "FocusedChild");
                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(_Id, "OptionArrowBytes", "The bytes macOS terminals send for Option+Left and Option+Right (ESC b, ESC f) and the CSI Alt arrows all move steps",
                        _ =>
                        {
                            Wizard wizard = Three(out TextField name, out TextField _, out TextField _);
                            name.Value = "ada";
                            foreach (string bytes in new[] { "\u001bf", "\u001bb", "\u001b[1;3C", "\u001b[1;3D" })
                            {
                                InputParser parser = new InputParser();
                                byte[] data = System.Text.Encoding.ASCII.GetBytes(bytes);
                                parser.Feed(data, data.Length);
                                List<InputEvent> events = parser.Drain().Concat(parser.Flush()).ToList();
                                Check.Equal(1, events.Count, "one key for " + bytes.Replace("\u001b", "ESC "));
                                int before = wizard.CurrentIndex;
                                Check.True(wizard.HandleKey(events[0].Key), "consumed: " + bytes.Replace("\u001b", "ESC "));
                                Check.True(wizard.CurrentIndex != before, "moved for " + bytes.Replace("\u001b", "ESC "));
                            }

                            return Task.CompletedTask;
                        }),
                });
        }

        internal static Wizard Three(out TextField name, out TextField repo, out TextField review)
        {
            TextField n = new TextField();
            TextField r = new TextField();
            TextField v = new TextField();
            name = n;
            repo = r;
            review = v;
            return new Wizard(new[]
            {
                new WizardStep("Account", n) { Validate = () => n.Value.Length == 0 ? "Name is required" : null },
                new WizardStep("Repository", r) { Optional = true },
                new WizardStep("Review", v),
            });
        }
    }
}
