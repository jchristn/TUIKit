namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for the developer-configurable widget style properties added in v0.10.2: base
    /// <c>NormalStyle</c> backgrounds and the per-role styles (header, selection, border, scrollbar
    /// track/thumb, diff add/remove, menu bar chrome). Each widget is rendered into an off-screen
    /// buffer and the resulting cell styles are asserted. Every configurable property is exercised
    /// with a positive case (a custom style is applied) and, where the default is transparent, a
    /// negative case (leaving the default does not paint a background, preserving the ambient region
    /// color the host paints behind the widget).
    /// </summary>
    public static class WidgetStyleSuite
    {
        private static readonly Color Grey = Color.FromRgb(0x2A, 0x2A, 0x2A);
        private static readonly Color Sentinel = Color.FromRgb(0x12, 0x34, 0x56);
        private static readonly Color CustomFg = Color.FromRgb(0xAB, 0xCD, 0xEF);
        private static readonly Color CustomFg2 = Color.FromRgb(0x21, 0x43, 0x65);

        /// <summary>
        /// Builds the widget-style suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "WidgetStyle",
                displayName: "Widget Styles",
                cases: new List<TestCaseDescriptor>
                {
                    // ---- TextEditor ----
                    new TestCaseDescriptor("WidgetStyle", "TextEditorBackground", "TextEditor paints its NormalStyle background",
                        _ =>
                        {
                            TextEditor editor = new TextEditor { Text = "hi" };
                            editor.NormalStyle = CellStyle.Default.WithBackground(Grey);
                            CellBuffer buffer = Paint(editor, 10, 3);
                            Check.Equal(Grey, buffer.Get(6, 2).Style.Background, "blank cell background");
                            Check.Equal(Grey, buffer.Get(0, 0).Style.Background, "text cell background");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "TextEditorDefault", "TextEditor default NormalStyle leaves the terminal-default background",
                        _ =>
                        {
                            TextEditor editor = new TextEditor { Text = "hi" };
                            CellBuffer buffer = PaintOver(editor, 10, 3, CellStyle.Default.WithBackground(Sentinel));
                            Check.Equal(Color.Default, buffer.Get(6, 2).Style.Background, "default fill background");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "TextEditorCaret", "TextEditor caret inverts against the configured background",
                        _ =>
                        {
                            TextEditor editor = new TextEditor { IsFocused = true };
                            editor.NormalStyle = CellStyle.Default.WithBackground(Grey);
                            CellBuffer buffer = Paint(editor, 10, 3);
                            Check.True(buffer.Get(0, 0).Style.HasAttribute(CellAttributes.Reverse), "caret is reverse video");
                            Check.Equal(Grey, buffer.Get(0, 0).Style.Background, "caret keeps the configured background");
                            return Task.CompletedTask;
                        }),

                    // ---- TextField ----
                    new TestCaseDescriptor("WidgetStyle", "TextFieldBackground", "TextField paints its NormalStyle background",
                        _ =>
                        {
                            TextField field = new TextField { Value = "x" };
                            field.NormalStyle = CellStyle.Default.WithBackground(Grey);
                            CellBuffer buffer = Paint(field, 10, 1);
                            Check.Equal(Grey, buffer.Get(5, 0).Style.Background, "blank cell background");
                            Check.Equal(Grey, buffer.Get(0, 0).Style.Background, "value cell background");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "TextFieldDefault", "TextField default NormalStyle leaves the terminal-default background",
                        _ =>
                        {
                            TextField field = new TextField { Value = "x" };
                            CellBuffer buffer = PaintOver(field, 10, 1, CellStyle.Default.WithBackground(Sentinel));
                            Check.Equal(Color.Default, buffer.Get(5, 0).Style.Background, "default fill background");
                            return Task.CompletedTask;
                        }),

                    // ---- ListView ----
                    new TestCaseDescriptor("WidgetStyle", "ListViewBackground", "ListView paints its NormalStyle background under every row",
                        _ =>
                        {
                            ListView<string> list = new ListView<string> { IsFocused = false };
                            list.NormalStyle = CellStyle.Default.WithBackground(Grey);
                            list.SetItems(new[] { "a", "b" });
                            CellBuffer buffer = Paint(list, 10, 4);
                            Check.Equal(Grey, buffer.Get(5, 3).Style.Background, "empty area background");
                            Check.Equal(Grey, buffer.Get(0, 1).Style.Background, "unselected row background");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "ListViewDefault", "ListView default NormalStyle leaves the terminal-default background",
                        _ =>
                        {
                            ListView<string> list = new ListView<string> { IsFocused = false };
                            list.SetItems(new[] { "a", "b" });
                            CellBuffer buffer = PaintOver(list, 10, 4, CellStyle.Default.WithBackground(Sentinel));
                            Check.Equal(Color.Default, buffer.Get(5, 3).Style.Background, "default fill background");
                            return Task.CompletedTask;
                        }),

                    // ---- Table ----
                    new TestCaseDescriptor("WidgetStyle", "TableHeaderStyle", "Table applies a custom HeaderStyle",
                        _ =>
                        {
                            Table table = new Table(new[] { "H" });
                            table.HeaderStyle = CellStyle.Default.WithForeground(CustomFg);
                            table.AddRow(new[] { "r" });
                            CellBuffer buffer = Paint(table, 10, 4);
                            Check.Equal(CustomFg, buffer.Get(0, 0).Style.Foreground, "header cell foreground");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "TableRowBackground", "Table paints its RowStyle background",
                        _ =>
                        {
                            Table table = new Table(new[] { "H" });
                            table.RowStyle = CellStyle.Default.WithBackground(Grey);
                            table.AddRow(new[] { "r" });
                            CellBuffer buffer = Paint(table, 10, 4);
                            Check.Equal(Grey, buffer.Get(8, 3).Style.Background, "empty area background");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "TableRowDefault", "Table default RowStyle does not paint a background",
                        _ =>
                        {
                            Table table = new Table(new[] { "H" });
                            table.AddRow(new[] { "r" });
                            CellBuffer buffer = PaintOver(table, 10, 4, CellStyle.Default.WithBackground(Sentinel));
                            Check.Equal(Sentinel, buffer.Get(8, 3).Style.Background, "ambient background preserved");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "TableBorderStyle", "Table applies a custom BorderStyle to its border glyphs",
                        _ =>
                        {
                            Table table = new Table(new[] { "H" }, TableBorder.Rounded);
                            table.BorderStyle = CellStyle.Default.WithForeground(CustomFg);
                            table.AddRow(new[] { "r" });
                            CellBuffer buffer = Paint(table, 12, 6);
                            Check.Equal(CustomFg, buffer.Get(0, 0).Style.Foreground, "top-left border glyph foreground");
                            return Task.CompletedTask;
                        }),

                    // ---- RadioGroup ----
                    new TestCaseDescriptor("WidgetStyle", "RadioSelectedStyle", "RadioGroup applies a custom SelectedStyle",
                        _ =>
                        {
                            RadioGroup group = new RadioGroup(new[] { "a", "b" });
                            group.SelectedStyle = CellStyle.Default.WithForeground(CustomFg);
                            CellBuffer buffer = Paint(group, 10, 3);
                            Check.Equal(CustomFg, buffer.Get(0, 0).Style.Foreground, "selected option foreground");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "RadioBackground", "RadioGroup paints its NormalStyle background",
                        _ =>
                        {
                            RadioGroup group = new RadioGroup(new[] { "a", "b" });
                            group.NormalStyle = CellStyle.Default.WithBackground(Grey);
                            CellBuffer buffer = Paint(group, 10, 3);
                            Check.Equal(Grey, buffer.Get(8, 2).Style.Background, "empty area background");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "RadioDefault", "RadioGroup default NormalStyle does not paint a background",
                        _ =>
                        {
                            RadioGroup group = new RadioGroup(new[] { "a", "b" });
                            CellBuffer buffer = PaintOver(group, 10, 3, CellStyle.Default.WithBackground(Sentinel));
                            Check.Equal(Sentinel, buffer.Get(8, 2).Style.Background, "ambient background preserved");
                            return Task.CompletedTask;
                        }),

                    // ---- ScrollView ----
                    new TestCaseDescriptor("WidgetStyle", "ScrollBarStyles", "ScrollView applies custom TrackStyle and ThumbStyle",
                        _ =>
                        {
                            TextEditor child = new TextEditor { Text = ManyLines(50) };
                            ScrollView view = new ScrollView(child, 10, 50);
                            view.ThumbStyle = CellStyle.Default.WithForeground(CustomFg);
                            view.TrackStyle = CellStyle.Default.WithForeground(CustomFg2);
                            CellBuffer buffer = Paint(view, 12, 8);
                            // Content (10 wide) fits the 12-wide viewport, so only the vertical bar is drawn
                            // at the last inner column (11). The thumb sits at the top with the view at y=0.
                            Check.Equal(CustomFg, buffer.Get(11, 0).Style.Foreground, "thumb foreground at top");
                            Check.Equal(CustomFg2, buffer.Get(11, 5).Style.Foreground, "track foreground below the thumb");
                            return Task.CompletedTask;
                        }),

                    // ---- DiffView ----
                    new TestCaseDescriptor("WidgetStyle", "DiffAddRemoveStyles", "DiffView applies custom AddedStyle and RemovedStyle",
                        _ =>
                        {
                            DiffView diff = new DiffView("a\nb", "a\nc");
                            diff.AddedStyle = CellStyle.Default.WithForeground(CustomFg);
                            diff.RemovedStyle = CellStyle.Default.WithForeground(CustomFg2);
                            CellBuffer buffer = Paint(diff, 10, 5);
                            // Rows: 0 = " a" (context), 1 = "-b" (removed), 2 = "+c" (added).
                            Check.Equal(CustomFg2, buffer.Get(0, 1).Style.Foreground, "removed gutter foreground");
                            Check.Equal(CustomFg, buffer.Get(0, 2).Style.Foreground, "added gutter foreground");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "DiffContextDefault", "DiffView context lines keep the default (unstyled) foreground",
                        _ =>
                        {
                            DiffView diff = new DiffView("a\nb", "a\nc");
                            diff.AddedStyle = CellStyle.Default.WithForeground(CustomFg);
                            CellBuffer buffer = Paint(diff, 10, 5);
                            Check.Equal(Color.Default, buffer.Get(1, 0).Style.Foreground, "context text foreground unchanged");
                            return Task.CompletedTask;
                        }),

                    // ---- FileBrowser ----
                    new TestCaseDescriptor("WidgetStyle", "FileBrowserHeader", "FileBrowser applies a custom HeaderStyle",
                        _ =>
                        {
                            FileBrowser browser = new FileBrowser(Path.GetTempPath());
                            browser.HeaderStyle = CellStyle.Default.WithForeground(CustomFg);
                            CellBuffer buffer = Paint(browser, 20, 6);
                            Check.Equal(CustomFg, buffer.Get(0, 0).Style.Foreground, "header cell foreground");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "FileBrowserBackground", "FileBrowser paints its NormalStyle background",
                        _ =>
                        {
                            FileBrowser browser = new FileBrowser(Path.GetTempPath());
                            browser.NormalStyle = CellStyle.Default.WithBackground(Grey);
                            CellBuffer buffer = Paint(browser, 20, 6);
                            Check.Equal(Grey, buffer.Get(18, 5).Style.Background, "empty area background");
                            return Task.CompletedTask;
                        }),

                    // ---- Tree ----
                    new TestCaseDescriptor("WidgetStyle", "TreeBackground", "Tree paints its NormalStyle background",
                        _ =>
                        {
                            Tree<string> tree = new Tree<string>("r", _ => Array.Empty<string>(), s => s);
                            tree.NormalStyle = CellStyle.Default.WithBackground(Grey);
                            CellBuffer buffer = Paint(tree, 10, 4);
                            Check.Equal(Grey, buffer.Get(8, 3).Style.Background, "empty area background");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "TreeDefault", "Tree default NormalStyle does not paint a background",
                        _ =>
                        {
                            Tree<string> tree = new Tree<string>("r", _ => Array.Empty<string>(), s => s);
                            CellBuffer buffer = PaintOver(tree, 10, 4, CellStyle.Default.WithBackground(Sentinel));
                            Check.Equal(Sentinel, buffer.Get(8, 3).Style.Background, "ambient background preserved");
                            return Task.CompletedTask;
                        }),

                    // ---- DataTable ----
                    new TestCaseDescriptor("WidgetStyle", "DataTableHeader", "DataTable applies a custom HeaderStyle",
                        _ =>
                        {
                            DataTable<string> table = new DataTable<string>();
                            table.Column("C", s => s);
                            table.HeaderStyle = CellStyle.Default.WithForeground(CustomFg);
                            table.Bind(new[] { "a" });
                            CellBuffer buffer = Paint(table, 10, 4);
                            Check.Equal(CustomFg, buffer.Get(0, 0).Style.Foreground, "header cell foreground");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "DataTableBackground", "DataTable paints its NormalStyle background",
                        _ =>
                        {
                            DataTable<string> table = new DataTable<string>();
                            table.Column("C", s => s);
                            table.NormalStyle = CellStyle.Default.WithBackground(Grey);
                            table.Bind(new[] { "a" });
                            CellBuffer buffer = Paint(table, 10, 4);
                            Check.Equal(Grey, buffer.Get(8, 3).Style.Background, "empty area background");
                            return Task.CompletedTask;
                        }),

                    // ---- CheckList ----
                    new TestCaseDescriptor("WidgetStyle", "CheckListBackground", "CheckList paints its NormalStyle background",
                        _ =>
                        {
                            CheckList<string> list = new CheckList<string>(new[] { "a", "b" }) { IsFocused = false };
                            list.NormalStyle = CellStyle.Default.WithBackground(Grey);
                            CellBuffer buffer = Paint(list, 10, 4);
                            Check.Equal(Grey, buffer.Get(8, 3).Style.Background, "empty area background");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "CheckListDefault", "CheckList default NormalStyle does not paint a background",
                        _ =>
                        {
                            CheckList<string> list = new CheckList<string>(new[] { "a", "b" }) { IsFocused = false };
                            CellBuffer buffer = PaintOver(list, 10, 4, CellStyle.Default.WithBackground(Sentinel));
                            Check.Equal(Sentinel, buffer.Get(8, 3).Style.Background, "ambient background preserved");
                            return Task.CompletedTask;
                        }),

                    // ---- CheckTree ----
                    new TestCaseDescriptor("WidgetStyle", "CheckTreeBackground", "CheckTree paints its NormalStyle background",
                        _ =>
                        {
                            CheckTree<string> tree = new CheckTree<string>(new[] { "r" }, _ => Array.Empty<string>(), s => s);
                            tree.NormalStyle = CellStyle.Default.WithBackground(Grey);
                            CellBuffer buffer = Paint(tree, 10, 4);
                            Check.Equal(Grey, buffer.Get(8, 3).Style.Background, "empty area background");
                            return Task.CompletedTask;
                        }),

                    // ---- MenuBar ----
                    new TestCaseDescriptor("WidgetStyle", "MenuBarBarStyle", "MenuBar paints its BarStyle across the strip",
                        _ =>
                        {
                            MenuBar bar = new MenuBar();
                            bar.AddMenu("File");
                            bar.BarStyle = CellStyle.Default.WithBackground(Grey);
                            CellBuffer buffer = Paint(bar, 20, 1);
                            Check.Equal(Grey, buffer.Get(15, 0).Style.Background, "bar strip background beyond the titles");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetStyle", "MenuBarActiveStyle", "MenuBar applies a custom ActiveStyle to the active title",
                        _ =>
                        {
                            MenuBar bar = new MenuBar();
                            bar.AddMenu("File");
                            bar.ActiveStyle = CellStyle.Default.WithForeground(CustomFg).WithBackground(Grey);
                            CellBuffer buffer = Paint(bar, 20, 1);
                            Check.Equal(Grey, buffer.Get(1, 0).Style.Background, "active title background");
                            Check.Equal(CustomFg, buffer.Get(1, 0).Style.Foreground, "active title foreground");
                            return Task.CompletedTask;
                        })
                });
        }

        private static CellBuffer Paint(IWidget widget, int width, int height)
        {
            return PaintOver(widget, width, height, CellStyle.Default);
        }

        private static CellBuffer PaintOver(IWidget widget, int width, int height, CellStyle ambient)
        {
            CellBuffer buffer = new CellBuffer(width, height);
            buffer.Clear(ambient);
            widget.Render(new BufferSurface(buffer));
            return buffer;
        }

        private static string ManyLines(int count)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    builder.Append('\n');
                builder.Append("line" + i);
            }

            return builder.ToString();
        }
    }
}
