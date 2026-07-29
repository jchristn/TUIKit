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

    /// <summary>
    /// Touchstone suite for modal editing, links, and lifecycle: modal (vi-style) key dispatch (10.32), link hints and clipboard
    /// read (10.34), and the app lifecycle / signal surface (10.31).
    /// </summary>
    public static class ModalLinkLifecycleSuite
    {
        /// <summary>
        /// Builds the modal editing, links, and lifecycle suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ModalLinkLifecycle",
                displayName: "Modal Editing, Links & Lifecycle",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ModalLinkLifecycle", "ModalDispatch", "Modal dispatcher routes keys per mode",
                        _ =>
                        {
                            ModalDispatcher dispatcher = new ModalDispatcher();
                            int deletes = 0;
                            int modeChanges = 0;
                            dispatcher.ModeChanged += _2 => modeChanges++;
                            dispatcher.Bind(EditMode.Normal, "d", () => deletes++);
                            dispatcher.Transition(EditMode.Normal, "i", EditMode.Insert);
                            dispatcher.Transition(EditMode.Insert, "escape", EditMode.Normal);

                            Check.Equal(EditMode.Normal, dispatcher.Mode, "starts in normal");
                            Check.True(dispatcher.Handle(KeyEvent.Char('d')), "d handled in normal");
                            Check.Equal(1, deletes, "delete action ran");

                            Check.True(dispatcher.Handle(KeyEvent.Char('i')), "i handled");
                            Check.Equal(EditMode.Insert, dispatcher.Mode, "switched to insert");
                            Check.False(dispatcher.Handle(KeyEvent.Char('d')), "d not bound in insert");
                            Check.Equal(1, deletes, "insert did not delete");

                            Check.True(dispatcher.Handle(KeyEvent.Special(KeyCode.Escape)), "escape handled");
                            Check.Equal(EditMode.Normal, dispatcher.Mode, "back to normal");
                            Check.Equal(2, modeChanges, "two mode changes");

                            Check.Throws<ArgumentNullException>(() => dispatcher.Bind(EditMode.Normal, null!, () => { }), "null chord");
                            Check.Throws<ArgumentNullException>(() => dispatcher.Bind(EditMode.Normal, "x", null!), "null action");
                            Check.Throws<ArgumentNullException>(() => dispatcher.Transition(EditMode.Normal, null!, EditMode.Visual), "null transition chord");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ModalLinkLifecycle", "LinkHints", "Link hints label and resolve links",
                        _ =>
                        {
                            Check.Equal("a", LinkHints.Label(0), "first label");
                            Check.Equal("z", LinkHints.Label(25), "last single label");
                            Check.Equal("aa", LinkHints.Label(26), "first double label");
                            Check.Equal("ab", LinkHints.Label(27), "second double label");

                            List<Link> links = new List<Link>
                            {
                                new Link(1, "one", "https://a"),
                                new Link(2, "two", "https://b")
                            };
                            LinkHints hints = LinkHints.ForLinks(links);
                            Check.Equal(2, hints.Count, "two hints");
                            Check.Equal(1, hints.Hints["a"].Id, "a maps to first link");
                            Check.Equal("b", hints.HintFor(2), "second link labeled b");
                            Check.True(hints.TryResolve("a", out Link? resolved) && resolved!.Id == 1, "resolve a");
                            Check.False(hints.TryResolve("zz", out Link? _), "resolve miss");

                            LinkRegistry registry = new LinkRegistry();
                            registry.Add("x", new Rect(0, 0, 1, 1), "https://x");
                            Check.Equal(1, LinkHints.ForRegistry(registry).Count, "registry hints");

                            Check.Throws<ArgumentNullException>(() => LinkHints.ForLinks(null!), "null links");
                            Check.Throws<ArgumentOutOfRangeException>(() => LinkHints.Label(-1), "negative index");
                            Check.Throws<ArgumentNullException>(() => hints.TryResolve(null!, out Link? _), "null hint");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ModalLinkLifecycle", "Clipboard", "Clipboard builds a write sequence and reads without throwing",
                        _ =>
                        {
                            string sequence = SystemClipboard.BuildWriteSequence("hello");
                            Check.True(sequence.Length > 0, "sequence produced");
                            Check.True(sequence.Contains("52"), "OSC 52 introducer present");

                            bool read = SystemClipboard.TryReadText(out string text);
                            Check.True(text != null, "text is never null");
                            if (!read)
                                Check.Equal(string.Empty, text, "failed read yields empty text");

                            Check.Throws<ArgumentNullException>(() => SystemClipboard.BuildWriteSequence(null!), "null text");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ModalLinkLifecycle", "Lifecycle", "App lifecycle raises suspend/resume/resize events",
                        _ =>
                        {
                            AppLifecycle life = new AppLifecycle();
                            int suspends = 0;
                            int resumes = 0;
                            int interrupts = 0;
                            Size? resized = null;
                            life.Suspending += () => suspends++;
                            life.Resumed += () => resumes++;
                            life.Interrupted += () => interrupts++;
                            life.Resized += s => resized = s;

                            Check.False(life.SignalsHooked, "no signals hooked yet");
                            life.RaiseSuspending();
                            life.RaiseResumed();
                            life.RaiseResized(new Size(80, 24));
                            life.RaiseInterrupted();

                            Check.Equal(1, suspends, "suspend raised");
                            Check.Equal(1, resumes, "resume raised");
                            Check.Equal(1, interrupts, "interrupt raised");
                            Check.True(resized.HasValue && resized.Value.Width == 80, "resize carried the size");

                            life.HookPosixSignals(); // no-op on Windows, wires signals on Unix; must not throw
                            life.Dispose();
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
