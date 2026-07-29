namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Widgets;

    /// <summary>
    /// Validation coverage for content types: panes and line handles, the text editor, and the
    /// stateless text utilities (markdown, syntax highlighting, ANSI stripping, wrapping, selection,
    /// clipboard).
    /// </summary>
    public static class ContentValidationSuite
    {
        /// <summary>
        /// Builds the content validation suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ContentValidation",
                displayName: "Content Validation (panes / editor / text)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ContentValidation", "Pane", "Pane construction, bounds, and writes validate arguments",
                        _ =>
                        {
                            Check.Throws<ArgumentException>(() => new Pane(""), "empty pane id");
                            Check.Throws<ArgumentException>(() => new Pane(null!), "null pane id");

                            Pane pane = new Pane("p");
                            Check.Throws<ArgumentOutOfRangeException>(() => pane.MaxLineLength = 0, "non-positive max line length");
                            Check.Throws<ArgumentOutOfRangeException>(() => pane.ScrollbackCapacityLines = -1, "negative scrollback capacity");
                            pane.ScrollbackCapacityLines = 0; // 0 means unbounded — allowed
                            Check.Equal(0, pane.ScrollbackCapacityLines, "zero scrollback allowed (unbounded)");

                            Check.Throws<ArgumentNullException>(() => pane.Write((string)null!), "write null string");
                            Check.Throws<ArgumentNullException>(() => pane.Write((StyledText)null!), "write null styled text");
                            Check.Throws<ArgumentNullException>(() => pane.WriteLine((string)null!), "writeline null string");
                            Check.Throws<ArgumentNullException>(() => pane.WriteMarkup(null!), "writemarkup null");
                            Check.Throws<ArgumentNullException>(() => pane.Render(null!), "render null surface");

                            PaneLineHandle handle = pane.WriteLine("first");
                            Check.Throws<ArgumentNullException>(() => handle.Update((StyledText)null!), "update handle with null");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ContentValidation", "TextEditor", "Text editor rejects null assignments and inserts",
                        _ =>
                        {
                            TextEditor editor = new TextEditor();
                            Check.Throws<ArgumentNullException>(() => editor.Text = null!, "null text");
                            Check.Throws<ArgumentNullException>(() => editor.InsertText(null!), "null insert");
                            editor.Text = "line one";
                            editor.InsertText("!");
                            Check.True(editor.Text.Contains("!"), "insert applied");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ContentValidation", "TextUtilities", "Stateless text utilities null-guard their inputs",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => MarkdownRenderer.Render(null!), "markdown null");
                            Check.Throws<ArgumentNullException>(() => MarkdownRenderer.RenderInline(null!, CellStyle.Default), "markdown inline null");
                            Check.Throws<ArgumentNullException>(() => SyntaxHighlighter.Highlight(null!, "csharp"), "highlight null code");
                            Check.Throws<ArgumentNullException>(() => SyntaxHighlighter.Highlight("x", null!), "highlight null language");
                            Check.Throws<ArgumentNullException>(() => SyntaxHighlighter.HighlightLine(null!, "csharp"), "highlight line null");
                            Check.Throws<ArgumentNullException>(() => AnsiStripper.Strip(null!), "strip null");
                            Check.Throws<ArgumentNullException>(() => TextWrapper.Wrap(null!, 10), "wrap null");
                            Check.Throws<ArgumentNullException>(() => ClipboardWriter.BuildSequence(null!), "clipboard null");
                            Check.Throws<ArgumentNullException>(() => new Selection().GetText(null!), "selection null lines");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
