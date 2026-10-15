namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Terminal;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.8 item C1 paste routing: <see cref="IPasteTarget"/>, <see cref="TuiApplication.RoutePasteToFocus"/>,
    /// <see cref="DialogModal.HandlePaste"/>, and the targets on <see cref="TextField"/>, <see cref="TextEditor"/>,
    /// and <see cref="Form"/>.
    /// </summary>
    public static class PasteRoutingSuite
    {
        private const string _Id = "PasteRouting";
        private const string _Esc = "\u001b";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Paste Routing",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "OffByDefault", "With RoutePasteToFocus false a paste raises PasteReceived and leaves the focused field alone",
                        _ =>
                        {
                            Run(false, new TextField(), (app, widget, pasted) =>
                            {
                                Check.Equal("hello", pasted.ToString(), "PasteReceived raised");
                                Check.Equal(string.Empty, ((TextField)widget).Value, "field unchanged (1.7)");
                            });
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "RoutesToField", "With RoutePasteToFocus true the focused field takes the paste and PasteReceived is not raised",
                        _ =>
                        {
                            Run(true, new TextField(), (app, widget, pasted) =>
                            {
                                Check.Equal("hello", ((TextField)widget).Value, "inserted into the field");
                                Check.Equal(string.Empty, pasted.ToString(), "not raised");
                            });
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "NonTargetFallsBack", "A focused widget that is not a paste target, or a read-only field, still raises PasteReceived",
                        _ =>
                        {
                            Run(true, new ListView<string>(), (app, widget, pasted) =>
                                Check.Equal("hello", pasted.ToString(), "raised for a non-target"));
                            Run(true, new TextField { IsReadOnly = true }, (app, widget, pasted) =>
                                Check.Equal("hello", pasted.ToString(), "raised when the field refuses"));
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "FormForwards", "A paste into a Form reaches its focused field, directly and through the host",
                        _ =>
                        {
                            Form form = new Form();
                            TextField name = form.Add("Name", new TextField());
                            Check.True(form.HandlePaste("Ada"), "consumed");
                            Check.Equal("Ada", name.Value, "focused field got it");
                            Check.Throws<ArgumentNullException>(() => form.HandlePaste(null!), "null rejected");
                            Form host = new Form();
                            TextField inner = host.Add("Name", new TextField());
                            Run(true, host, (app, widget, pasted) =>
                            {
                                Check.Equal("hello", inner.Value, "routed to the form's leaf");
                                Check.Equal(string.Empty, pasted.ToString(), "not raised");
                            });
                            Form empty = new Form();
                            Check.False(empty.HandlePaste("x"), "a form without fields refuses");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "TextEditorTarget", "TextEditor accepts pastes unless read-only",
                        _ =>
                        {
                            TextEditor editor = new TextEditor();
                            Check.True(editor.HandlePaste("a\nb"), "consumed");
                            Check.Equal("a\nb", editor.Text, "inserted with the newline");
                            editor.IsReadOnly = true;
                            Check.False(editor.HandlePaste("c"), "read-only refuses");
                            Check.Throws<ArgumentNullException>(() => editor.HandlePaste(null!), "null rejected");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "DialogModal", "DialogModal offers pastes to its PasteTarget, and refuses them without one",
                        _ =>
                        {
                            PasteDialogModal without = new PasteDialogModal(false);
                            Check.False(without.HandlePaste("x"), "no target: unconsumed, as in 1.7");
                            Check.Equal(string.Empty, without.Field.Value, "field unchanged");
                            PasteDialogModal with = new PasteDialogModal(true);
                            Check.True(with.HandlePaste("token"), "consumed");
                            Check.Equal("token", with.Field.Value, "inserted");
                            Check.Throws<ArgumentNullException>(() => with.HandlePaste(null!), "null rejected");
                            ModalStack stack = new ModalStack();
                            PasteDialogModal top = new PasteDialogModal(true);
                            stack.Push(top);
                            Check.True(stack.HandlePaste("via stack"), "stack routes to it");
                            Check.Equal("via stack", top.Field.Value, "through the modal stack");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "TextFieldTarget", "TextField.HandlePaste refuses while disabled and rejects null",
                        _ =>
                        {
                            TextField field = new TextField { IsEnabled = false };
                            Check.False(field.HandlePaste("x"), "disabled refuses");
                            Check.Throws<ArgumentNullException>(() => field.HandlePaste(null!), "null rejected");
                            return Task.CompletedTask;
                        }),
                });
        }

        private static void Run(bool route, IWidget widget, Action<TuiApplication, IWidget, StringBuilder> verify)
        {
            HeadlessBackend backend = new HeadlessBackend(60, 6);
            using (TuiApplication app = new TuiApplication(backend))
            {
                StringBuilder pasted = new StringBuilder();
                app.PasteReceived += text => pasted.Append(text);
                app.RoutePasteToFocus = route;
                app.Layout = TUIKit.Layout.Layout.Create().Add("main", r => r.FillWidth().FillHeight().WithPadding(0)).Build();
                app.Bind("main", widget);
                app.Start();
                app.Focus("main");
                backend.FeedInput(_Esc + "[200~hello" + _Esc + "[201~");
                app.PumpInputOnce();
                verify(app, widget, pasted);
                app.Stop();
            }
        }
    }
}
