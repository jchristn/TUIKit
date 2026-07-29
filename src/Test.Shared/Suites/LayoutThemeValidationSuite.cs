namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Layout;
    using TUIKit.Theming;

    /// <summary>
    /// Validation coverage for layout primitives (padding, constraints, regions, builders) and themes.
    /// </summary>
    public static class LayoutThemeValidationSuite
    {
        /// <summary>
        /// Builds the layout/theme validation suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "LayoutThemeValidation",
                displayName: "Layout & Theme Validation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("LayoutThemeValidation", "Padding", "Padding computes, deflates, and rejects negatives",
                        _ =>
                        {
                            Padding pad = new Padding(2, 1, 3, 4);
                            Check.Equal(5, pad.Horizontal, "left + right");
                            Check.Equal(5, pad.Vertical, "top + bottom");
                            Check.Equal(2, Padding.Uniform(2).Horizontal / 2, "uniform side");
                            Check.Equal(6, Padding.Symmetric(3, 1).Horizontal, "symmetric horizontal");

                            Rect deflated = new Padding(3, 0, 0, 0).Deflate(new Rect(0, 0, 2, 2));
                            Check.Equal(0, deflated.Width, "deflate never goes negative");

                            Check.Throws<ArgumentOutOfRangeException>(() => new Padding(-1, 0, 0, 0), "negative left");
                            Check.Throws<ArgumentOutOfRangeException>(() => new Padding(0, -1, 0, 0), "negative top");
                            Check.Throws<ArgumentOutOfRangeException>(() => new Padding(0, 0, -1, 0), "negative right");
                            Check.Throws<ArgumentOutOfRangeException>(() => new Padding(0, 0, 0, -1), "negative bottom");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LayoutThemeValidation", "Proportional", "Proportional constraint validates fractions and bounds",
                        _ =>
                        {
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Proportional(-0.1, 0.5), "negative start");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Proportional(1.5, 0.5), "start over 1");
                            Check.Throws<ArgumentOutOfRangeException>(() => AxisConstraint.Proportional(0.5, 0.5, min: 0), "non-positive min");
                            AxisConstraint ok = AxisConstraint.Proportional(0.25, 0.5);
                            Check.True(ok != null, "valid proportional built");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LayoutThemeValidation", "RegionsAndBuilders", "Regions and layout builders validate arguments",
                        _ =>
                        {
                            AxisConstraint h = AxisConstraint.Stretch();
                            AxisConstraint v = AxisConstraint.Stretch();
                            Check.Throws<ArgumentException>(() => new Region("", h, v), "empty region id");
                            Check.Throws<ArgumentNullException>(() => new Region("r", null!, v), "null horizontal constraint");
                            Check.Throws<ArgumentNullException>(() => new Region("r", h, null!), "null vertical constraint");

                            Check.Throws<ArgumentException>(() => new RegionBuilder(""), "empty builder id");
                            Check.Throws<InvalidOperationException>(() => new RegionBuilder("x").Build(), "build with no constraints");

                            Check.Throws<ArgumentNullException>(() => Layout.Create().Add((Region)null!), "null region added");
                            Check.Throws<ArgumentNullException>(() => Layout.Create().Add("id", null!), "null configure added");
                            Check.Throws<InvalidOperationException>(() => Layout.Create().Build(), "empty layout built");

                            Layout layout = Layout.Column(LayoutSlot.Fill("a"), LayoutSlot.Fill("b"));
                            Check.Throws<ArgumentNullException>(() => layout.Resolve(null!, new Size(10, 4)), "resolve null region");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("LayoutThemeValidation", "Theme", "Theme construction and style lookup validate arguments",
                        _ =>
                        {
                            CellStyle s = CellStyle.Default;
                            Check.Throws<ArgumentException>(() => new Theme(null!, s, s, s, s), "null theme name");
                            Check.Throws<ArgumentException>(() => new Theme("", s, s, s, s), "empty theme name");

                            Theme theme = new Theme("custom", s, s, s, s);
                            Check.Equal("custom", theme.Name, "theme name stored");
                            theme.SetStyle("accent2", s);
                            Check.True(theme.GetStyle("accent2").Equals(s), "registered style returned");
                            Check.True(theme.GetStyle("undefined").Equals(theme.Text), "unknown style falls back to Text");
                            Check.Throws<ArgumentNullException>(() => theme.GetStyle(null!), "null style name");
                            Check.Throws<ArgumentException>(() => theme.SetStyle("", s), "empty set-style name");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
