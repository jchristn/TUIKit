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
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for 1.9 item C6 validation: <see cref="Form.ValidateAll"/>, <see cref="FormFieldError"/>,
    /// <see cref="Form.GetFieldError"/>, <see cref="Form.ClearErrors"/>, <see cref="Form.ShowFieldErrors"/>,
    /// <see cref="Form.ErrorPrefix"/>, and <see cref="Form.ValidationMode"/>.
    /// </summary>
    public static class FormValidationSuite
    {
        private const string _Id = "FormValidation";

        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: _Id,
                displayName: "Form Validation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(_Id, "ValidateAll", "ValidateAll returns errors in field order, includes TextField.Validator, and skips hidden fields",
                        _ =>
                        {
                            Form form = Build(out TextField name, out TextField email, out TextField port);
                            IReadOnlyList<FormFieldError> errors = form.ValidateAll();
                            Check.Equal("Name,Email,Port", string.Join(",", errors.Select(e => e.Label)), "in order");
                            Check.Equal("Required", errors[0].Message, "form validator");
                            Check.Equal("Needs @", errors[1].Message, "TextField.Validator");
                            Check.Equal("Email: Needs @", errors[1].ToString(), "ToString");
                            Check.Equal("Needs @", form.GetFieldError(1), "stored");
                            form.SetFieldVisible(2, false);
                            Check.Equal(2, form.ValidateAll().Count, "hidden field skipped");
                            Check.True(form.GetFieldError(2) == null, "hidden error cleared");
                            Check.Equal("Required", form.Validate(), "Validate unchanged: first form-validator error");
                            name.Value = "ada";
                            email.Value = "a@b";
                            Check.Equal(0, form.ValidateAll().Count, "valid");
                            form.ClearErrors();
                            Check.Throws<ArgumentOutOfRangeException>(() => form.GetFieldError(9), "bad index");
                            Check.Throws<ArgumentException>(() => new FormFieldError(0, "x", ""), "empty message");
                            Check.Throws<ArgumentOutOfRangeException>(() => new FormFieldError(-1, "x", "m"), "negative index");
                            Check.Throws<ArgumentNullException>(() => new FormFieldError(0, null!, "m"), "null label");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ShowFieldErrors", "ShowFieldErrors false draws nothing; true draws each error with its prefix above the hint",
                        _ =>
                        {
                            Form form = Build(out TextField name, out TextField email, out TextField port);
                            form.SetFieldHint(0, "Your full name");
                            form.ValidateAll();
                            string hidden = Snapshot.RenderWidget(form, 30, 14);
                            Check.False(hidden.Contains("Required"), "not drawn by default");
                            form.ShowFieldErrors = true;
                            CellBuffer buffer = MarkdownThemeSuite.Draw(form, 30, 14);
                            string[] lines = Snapshot.ToText(buffer).Split('\n');
                            Check.Equal("  ! Required", lines[2], "error under the field");
                            Check.Equal("  Your full name", lines[3], "hint after the error");
                            Check.Equal(Color.FromPalette(1), buffer.Get(2, 2).Style.Foreground, "error style");
                            form.ErrorPrefix = "x ";
                            Check.True(Snapshot.RenderWidget(form, 30, 14).Contains("x Required"), "custom prefix");
                            Check.Throws<ArgumentNullException>(() => form.ErrorPrefix = null!, "null prefix");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "ErrorClearsAfterFix", "With OnSubmit, a stored error re-checks when its field changes and clears after a fix",
                        _ =>
                        {
                            Form form = Build(out TextField name, out TextField email, out TextField port);
                            form.ValidateAll();
                            Check.Equal("Required", form.GetFieldError(0), "error stored");
                            email.Value = "still bad";
                            Check.Equal("Needs @", form.GetFieldError(1), "a change re-checks and the error stays");
                            name.Value = "ada";
                            Check.True(form.GetFieldError(0) == null, "cleared after a fix");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(_Id, "Modes", "OnSubmit validates nothing on its own; OnChange validates every change; OnLeave validates when focus leaves",
                        _ =>
                        {
                            Form onSubmit = Build(out TextField n1, out TextField e1, out TextField p1);
                            e1.Value = "x";
                            Check.True(onSubmit.GetFieldError(1) == null, "OnSubmit: nothing until submit");
                            Check.Equal(FormValidationMode.OnSubmit, onSubmit.ValidationMode, "default");

                            Form onChange = Build(out TextField n2, out TextField e2, out TextField p2);
                            onChange.ValidationMode = FormValidationMode.OnChange;
                            e2.Value = "x";
                            Check.Equal("Needs @", onChange.GetFieldError(1), "OnChange: validated on change");

                            Form onLeave = Build(out TextField n3, out TextField e3, out TextField p3);
                            onLeave.ValidationMode = FormValidationMode.OnLeave;
                            Check.True(onLeave.GetFieldError(0) == null, "not yet");
                            onLeave.HandleKey(KeyEvent.Special(KeyCode.Tab));
                            Check.Equal("Required", onLeave.GetFieldError(0), "validated when focus left Name");
                            Check.True(onLeave.GetFieldError(1) == null, "Email not validated yet");
                            return Task.CompletedTask;
                        }),
                });
        }

        internal static Form Build(out TextField name, out TextField email, out TextField port)
        {
            Form form = new Form();
            TextField n = new TextField();
            TextField e = new TextField { Validator = v => v.Contains("@") ? null : "Needs @" };
            TextField p = new TextField();
            form.Add("Name", n, () => n.Value.Length == 0 ? "Required" : null);
            form.Add("Email", e);
            form.Add("Port", p, () => int.TryParse(p.Value, out int value) && value > 0 ? null : "A positive number");
            name = n;
            email = e;
            port = p;
            return form;
        }
    }
}
