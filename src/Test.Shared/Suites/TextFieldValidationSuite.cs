namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.8 item C1: <see cref="TextField.Validator"/>, <see cref="TextField.Validate"/>,
    /// <see cref="TextField.Error"/>, <see cref="TextField.ErrorChanged"/>, <see cref="TextField.ValidateOnChange"/>,
    /// and <see cref="TextField.ErrorStyle"/>.
    /// </summary>
    public static class TextFieldValidationSuite
    {
        private const string _Id = "TextFieldValidation";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "TextField Validation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "NoValidator", "Without a validator Validate returns true and clears Error",
                        _ =>
                        {
                            TextField field = new TextField();
                            Check.True(field.Validate(), "valid");
                            Check.True(field.Error == null, "no error");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "PassAndFail", "A failing validator stores its message; a passing one clears it; ErrorChanged fires only on change",
                        _ =>
                        {
                            TextField field = new TextField { Validator = v => v.Length < 3 ? "Too short" : null };
                            int changes = 0;
                            field.ErrorChanged += (s, e) => changes++;
                            Check.False(field.Validate(), "empty fails");
                            Check.Equal("Too short", field.Error, "message stored");
                            Check.Equal(1, changes, "raised once");
                            Check.False(field.Validate(), "still fails");
                            Check.Equal(1, changes, "not raised when the error is the same");
                            field.Value = "long enough";
                            Check.True(field.Error != null, "Validate is not run on change by default");
                            Check.True(field.Validate(), "passes");
                            Check.True(field.Error == null, "cleared");
                            Check.Equal(2, changes, "raised on clearing");
                            field.Validator = null;
                            field.Value = string.Empty;
                            Check.True(field.Validate(), "null validator accepts anything");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ValidateOnChange", "ValidateOnChange re-validates on every edit",
                        _ =>
                        {
                            TextField field = new TextField { Validator = v => v.Contains("@") ? null : "Needs @", ValidateOnChange = true };
                            field.HandleKey(KeyEvent.Char('a'));
                            Check.Equal("Needs @", field.Error, "error after typing");
                            field.HandleKey(KeyEvent.Char('@'));
                            Check.True(field.Error == null, "cleared after a fix");
                            field.Value = "x";
                            Check.Equal("Needs @", field.Error, "programmatic set re-validates too");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ErrorStyle", "ErrorStyle applies only while an error is set and is not drawn by default",
                        _ =>
                        {
                            TextField field = new TextField { Value = "bad", Validator = v => "nope" };
                            field.Validate();
                            CellStyle plain = MarkdownThemeSuite.Draw(field, 6, 1).Get(0, 0).Style;
                            Check.Equal(CellStyle.Default, plain, "no ErrorStyle: drawn unchanged");
                            field.ErrorStyle = CellStyle.Default.WithForeground(Color.FromPalette(1));
                            Check.Equal(Color.FromPalette(1), MarkdownThemeSuite.Draw(field, 6, 1).Get(0, 0).Style.Foreground, "error style drawn");
                            field.Validator = null;
                            field.Validate();
                            Check.Equal(CellStyle.Default, MarkdownThemeSuite.Draw(field, 6, 1).Get(0, 0).Style, "no error: normal style");
                            return Task.CompletedTask;
                        }),
                });
        }
    }
}
