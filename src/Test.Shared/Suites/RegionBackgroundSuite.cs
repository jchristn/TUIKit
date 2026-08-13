namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Hosting;
    using TUIKit.Layout;
    using TUIKit.Terminal;
    using TUIKit.Theming;

    /// <summary>
    /// Coverage for per-region background colors: the <see cref="Region"/> and
    /// <see cref="RegionBuilder"/> inputs, their guards, and the host painting an explicit color and a
    /// theme-role background into the region's rectangle.
    /// </summary>
    public static class RegionBackgroundSuite
    {
        /// <summary>
        /// Builds the region-background suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "RegionBackground",
                displayName: "Region Backgrounds",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("RegionBackground", "ExplicitColor", "Builder records an explicit background color",
                        _ =>
                        {
                            Region region = Region.Define("panel").FillWidth().FillHeight()
                                .Background(Color.FromRgb(40, 41, 42)).Build();
                            Check.True(region.HasBackground, "region reports a background");
                            Check.True(region.Background.HasValue, "explicit color present");
                            Check.Equal(Color.FromRgb(40, 41, 42), region.Background!.Value, "background color");
                            Check.True(region.BackgroundRole == null, "no role when color is set");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("RegionBackground", "Role", "Builder records a theme background role",
                        _ =>
                        {
                            Region region = Region.Define("side").RightAnchored(0, 20).FillHeight()
                                .BackgroundRole(Theme.SidebarRole).Build();
                            Check.True(region.HasBackground, "region reports a background");
                            Check.Equal(Theme.SidebarRole, region.BackgroundRole, "role name");
                            Check.False(region.Background.HasValue, "no explicit color when a role is set");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("RegionBackground", "LastWins", "Setting color then role (and vice versa) keeps only the last",
                        _ =>
                        {
                            Region colorLast = Region.Define("a").FillWidth().FillHeight()
                                .BackgroundRole("x").Background(Color.FromRgb(1, 2, 3)).Build();
                            Check.True(colorLast.Background.HasValue, "explicit color wins after role");
                            Check.True(colorLast.BackgroundRole == null, "role cleared by later color");

                            Region roleLast = Region.Define("b").FillWidth().FillHeight()
                                .Background(Color.FromRgb(1, 2, 3)).BackgroundRole("x").Build();
                            Check.Equal("x", roleLast.BackgroundRole, "role wins after color");
                            Check.False(roleLast.Background.HasValue, "color cleared by later role");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("RegionBackground", "Transparent", "A region with no background stays transparent",
                        _ =>
                        {
                            Region region = Region.Define("plain").FillWidth().FillHeight().Build();
                            Check.False(region.HasBackground, "no background by default");
                            Check.False(region.Background.HasValue, "no explicit color by default");
                            Check.True(region.BackgroundRole == null, "no role by default");

                            Region cleared = Region.Define("plain2").FillWidth().FillHeight()
                                .Background(Color.FromRgb(9, 9, 9)).NoBackground().Build();
                            Check.False(cleared.HasBackground, "NoBackground clears the background");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("RegionBackground", "PaintsExplicit", "Host paints the explicit background into the region rectangle",
                        _ =>
                        {
                            using (HeadlessBackend backend = new HeadlessBackend(20, 6))
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Theme = Theme.Dark;
                                app.Layout = Layout.Create()
                                    .Add("main", r => r.FillWidth().FillHeight().Background(Color.FromRgb(40, 41, 42)))
                                    .Build();
                                app.Start();
                                app.RenderOnce();
                                string output = backend.PeekOutput();
                                app.Stop();

                                Check.True(output.Contains("48;2;40;41;42"), "explicit background SGR emitted");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("RegionBackground", "PaintsRole", "Host resolves a theme role to its background color",
                        _ =>
                        {
                            using (HeadlessBackend backend = new HeadlessBackend(20, 6))
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Theme = Theme.Dark;
                                app.Layout = Layout.Create()
                                    .Add("side", r => r.FillWidth().FillHeight().BackgroundRole(Theme.SidebarRole))
                                    .Build();
                                app.Start();
                                app.RenderOnce();
                                string output = backend.PeekOutput();
                                app.Stop();

                                // Dark theme registers the sidebar role at RGB(0x18,0x18,0x1B) = 24,24,27.
                                Check.True(output.Contains("48;2;24;24;27"), "sidebar role background SGR emitted");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("RegionBackground", "RoleFollowsTheme", "Switching the theme restyles a role-based background",
                        _ =>
                        {
                            using (HeadlessBackend backend = new HeadlessBackend(20, 6))
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                app.Theme = Theme.Light;
                                app.Layout = Layout.Create()
                                    .Add("side", r => r.FillWidth().FillHeight().BackgroundRole(Theme.SidebarRole))
                                    .Build();
                                app.Start();
                                app.RenderOnce();
                                string output = backend.PeekOutput();
                                app.Stop();

                                // Light theme registers the sidebar role at RGB(0xD8,0xD8,0xD8) = 216,216,216.
                                Check.True(output.Contains("48;2;216;216;216"), "light sidebar role background SGR emitted");
                            }

                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("RegionBackground", "EmptyRoleThrows", "An empty or whitespace role is rejected",
                        _ =>
                        {
                            Check.Throws<ArgumentException>(() => Region.Define("x").FillWidth().FillHeight().BackgroundRole(""), "empty role via builder");
                            Check.Throws<ArgumentException>(() => Region.Define("x").FillWidth().FillHeight().BackgroundRole("   "), "whitespace role via builder");
                            Check.Throws<ArgumentException>(() => Region.Define("x").FillWidth().FillHeight().BackgroundRole(null!), "null role via builder");
                            Check.Throws<ArgumentException>(
                                () => new Region("x", AxisConstraint.Stretch(0, 0), AxisConstraint.Stretch(0, 0), default, BorderStyle.None, null, null, ""),
                                "empty role via constructor");
                            Check.Throws<ArgumentException>(
                                () => new Region("x", AxisConstraint.Stretch(0, 0), AxisConstraint.Stretch(0, 0), default, BorderStyle.None, null, null, "  "),
                                "whitespace role via constructor");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
