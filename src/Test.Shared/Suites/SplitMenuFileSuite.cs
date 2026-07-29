namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite for split, menu, and file browser: the surface view and resizable/nestable split (10.22, 10.28),
    /// the menu bar (10.25), and the file browser (10.35).
    /// </summary>
    public static class SplitMenuFileSuite
    {
        /// <summary>
        /// Builds the split, menu, and file browser suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "SplitMenuFile",
                displayName: "Split, Menu & File Browser",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("SplitMenuFile", "SurfaceView", "Surface view offsets and clips writes",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(10, 5);
                            SurfaceView view = new SurfaceView(new BufferSurface(buffer), new Rect(2, 1, 4, 2));
                            Check.Equal(4, view.Size.Width, "view width");
                            Check.Equal(2, view.Size.Height, "view height");

                            view.Set(0, 0, Cell.Glyph("X", CellStyle.Default, 1));
                            Check.Equal("X", buffer.Get(2, 1).Grapheme, "write mapped to parent offset");

                            view.Set(9, 9, Cell.Glyph("Y", CellStyle.Default, 1)); // out of view: clipped
                            Check.Equal(" ", buffer.Get(9, 4).Grapheme, "out-of-view write clipped");

                            Check.Throws<ArgumentNullException>(() => new SurfaceView(null!, new Rect(0, 0, 1, 1)), "null parent");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SplitMenuFile", "SplitRender", "Split view renders both children with a divider",
                        _ =>
                        {
                            SplitView split = new SplitView(SplitOrientation.Horizontal,
                                new Label(Text.From("LEFT")), new Label(Text.From("RIGHT")), 0.5);
                            WidgetTester tester = WidgetTester.For(split, 21, 3).Render();
                            tester.AssertContains("LEFT").AssertContains("RIGHT").AssertContains("│");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SplitMenuFile", "SplitResize", "Split view resizes and clamps with arrows",
                        _ =>
                        {
                            SplitView split = new SplitView(SplitOrientation.Horizontal,
                                new Label(Text.From("a")), new Label(Text.From("b")), 0.5);
                            split.ResizeStep = 0.1;

                            Check.True(split.HandleKey(KeyEvent.Special(KeyCode.Right)), "right consumed");
                            Check.True(Math.Abs(split.Ratio - 0.6) < 1e-9, "ratio grew");
                            split.HandleKey(KeyEvent.Special(KeyCode.Left));
                            split.HandleKey(KeyEvent.Special(KeyCode.Left));
                            Check.True(Math.Abs(split.Ratio - 0.4) < 1e-9, "ratio shrank");
                            Check.False(split.HandleKey(KeyEvent.Special(KeyCode.Up)), "cross-axis key ignored for horizontal");

                            for (int i = 0; i < 20; i++)
                                split.HandleKey(KeyEvent.Special(KeyCode.Right));
                            Check.True(split.Ratio <= split.MaxRatio + 1e-9, "ratio clamped to max");

                            Check.Throws<ArgumentNullException>(() => new SplitView(SplitOrientation.Vertical, null!, new Label(Text.From("x"))), "null first child");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SplitMenuFile", "MenuNavigate", "Menu bar opens, navigates, and activates",
                        _ =>
                        {
                            bool quit = false;
                            MenuBar bar = new MenuBar();
                            bar.AddMenu("File").Add("Open").Add("Quit", () => quit = true);
                            bar.AddMenu("Edit").Add("Copy");
                            Check.Equal(2, bar.Count, "two menus");

                            WidgetTester tester = WidgetTester.For(bar, 30, 8).Render();
                            tester.AssertContains("File").AssertContains("Edit");

                            Check.True(bar.HandleKey(KeyEvent.Special(KeyCode.Right)), "right consumed");
                            Check.Equal(1, bar.ActiveMenu, "moved to Edit");
                            bar.HandleKey(KeyEvent.Special(KeyCode.Left));
                            Check.Equal(0, bar.ActiveMenu, "back to File");

                            bar.HandleKey(KeyEvent.Special(KeyCode.Down)); // open
                            Check.True(bar.IsOpen, "menu open");
                            tester.Render().AssertContains("Open").AssertContains("Quit");

                            bar.HandleKey(KeyEvent.Special(KeyCode.Down)); // highlight Quit
                            Check.Equal(1, bar.HighlightedItem, "highlight moved to Quit");
                            bar.HandleKey(KeyEvent.Special(KeyCode.Enter));
                            Check.True(quit, "activated Quit action");
                            Check.False(bar.IsOpen, "closed after activation");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SplitMenuFile", "MenuGuards", "Menu bar handles empty state and null inputs",
                        _ =>
                        {
                            MenuBar empty = new MenuBar();
                            Check.False(empty.HandleKey(KeyEvent.Special(KeyCode.Down)), "empty bar ignores keys");
                            Check.Throws<ArgumentNullException>(() => empty.Add(null!), "null menu");
                            Check.Throws<ArgumentNullException>(() => new Menu(null!), "null title");
                            Check.Throws<ArgumentNullException>(() => new MenuItem(null!), "null item label");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("SplitMenuFile", "FileBrowser", "File browser lists, descends, and activates files",
                        _ =>
                        {
                            string root = Path.Combine(Path.GetTempPath(), "tuikit-fb-" + Guid.NewGuid().ToString("N"));
                            Directory.CreateDirectory(root);
                            try
                            {
                                Directory.CreateDirectory(Path.Combine(root, "sub"));
                                File.WriteAllText(Path.Combine(root, "a.txt"), "x");

                                FileBrowser browser = new FileBrowser(root);
                                Check.Equal(root, browser.CurrentDirectory, "current directory set");
                                Check.Equal("..", browser.SelectedName, "parent link first");
                                Check.Equal(3, browser.Count, "parent + sub + a.txt");

                                browser.HandleKey(KeyEvent.Special(KeyCode.Down)); // sub
                                Check.Equal("sub", browser.SelectedName, "directories before files");
                                browser.HandleKey(KeyEvent.Special(KeyCode.Enter)); // descend
                                Check.Equal(Path.Combine(root, "sub"), browser.CurrentDirectory, "descended into sub");

                                browser.HandleKey(KeyEvent.Special(KeyCode.Backspace)); // back up
                                Check.Equal(root, browser.CurrentDirectory, "back to root");

                                string? activated = null;
                                browser.FileActivated += p => activated = p;
                                browser.HandleKey(KeyEvent.Special(KeyCode.Down)); // sub
                                browser.HandleKey(KeyEvent.Special(KeyCode.Down)); // a.txt
                                Check.Equal("a.txt", browser.SelectedName, "file selected");
                                browser.HandleKey(KeyEvent.Special(KeyCode.Enter));
                                Check.Equal(Path.Combine(root, "a.txt"), activated, "file activation raised event");
                                Check.Equal(Path.Combine(root, "a.txt"), browser.SelectedPath, "selected path recorded");

                                Check.Throws<ArgumentNullException>(() => new FileBrowser(null!), "null start dir");
                                Check.Throws<DirectoryNotFoundException>(() => new FileBrowser(Path.Combine(root, "nope")), "missing dir");
                            }
                            finally
                            {
                                try
                                {
                                    Directory.Delete(root, true);
                                }
                                catch (IOException)
                                {
                                }
                            }

                            return Task.CompletedTask;
                        })
                });
        }
    }
}
