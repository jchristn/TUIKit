namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Widgets;

    /// <summary>
    /// Validation coverage for widget constructor and mutator argument guards not exercised elsewhere.
    /// </summary>
    public static class WidgetGuardValidationSuite
    {
        /// <summary>
        /// Builds the widget-guard validation suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "WidgetGuardValidation",
                displayName: "Widget Guard Validation",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("WidgetGuardValidation", "Collections", "Collection-backed widgets reject null and empty inputs",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new Spinner(null!), "null spinner frames");
                            Check.Throws<ArgumentException>(() => new Spinner(Array.Empty<string>()), "empty spinner frames");
                            Check.Throws<ArgumentNullException>(() => new Table(null!), "null table headers");
                            Check.Throws<ArgumentException>(() => new Table(Array.Empty<string>()), "empty table headers");
                            Check.Throws<ArgumentNullException>(() => new Table(new[] { "H" }).AddRow(null!), "null table row");
                            Check.Throws<ArgumentNullException>(() => new ListView().SetItems(null!), "null list items");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetGuardValidation", "TextWidgets", "Text-bearing widgets reject null content",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new Label(null!), "null label content");
                            Label label = new Label(Text.From("x"));
                            Check.Throws<ArgumentNullException>(() => label.Content = null!, "null label content set");
                            Check.Throws<ArgumentNullException>(() => new BannerText(null!), "null banner text");
                            Check.Throws<ArgumentNullException>(() => new BannerText("A").SetText(null!), "null banner set text");
                            Check.Throws<ArgumentNullException>(() => new LineChart(null!), "null line chart values");
                            Check.Throws<ArgumentNullException>(() => new LineChart(new double[] { 1 }).SetValues(null!), "null line chart set values");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("WidgetGuardValidation", "MenusAndFocus", "Menus, collapsibles, and the focus manager reject null arguments",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new Menu(null!), "null menu title");
                            Check.Throws<ArgumentNullException>(() => new Menu("File").Add((MenuItem)null!), "null menu item");
                            Check.Throws<ArgumentNullException>(() => new Menu("File").Add((string)null!), "null menu item label");
                            Check.Throws<ArgumentNullException>(() => new MenuBar().Add(null!), "null menu");
                            Check.Throws<ArgumentNullException>(() => new MenuBar().AddMenu(null!), "null menu title via bar");
                            Check.Throws<ArgumentNullException>(() => new MenuItem(null!), "null menu item ctor label");

                            Check.Throws<ArgumentNullException>(() => new Collapsible(null!, new Label(Text.From("x"))), "null collapsible header");
                            Check.Throws<ArgumentNullException>(() => new Collapsible("h", null!), "null collapsible child");
                            Check.Throws<ArgumentNullException>(() => new StatusBar().Add(null!, "label"), "null status bar key");

                            Check.Throws<ArgumentNullException>(() => new FocusManager().Register((IFocusable[])null!), "null focus registration");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
