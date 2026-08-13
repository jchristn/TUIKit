namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit.Hosting;
    using TUIKit.Input;
    using TUIKit.Terminal;
    using TUIKit.Widgets;

    /// <summary>
    /// Coverage for <see cref="Command"/> and <see cref="CommandRegistry"/>: one registration driving
    /// key bindings, a menu bar, a palette, and slash resolution, plus enabled filtering and guards.
    /// </summary>
    public static class CommandRegistrySuite
    {
        /// <summary>
        /// Builds the command-registry suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "CommandRegistry",
                displayName: "Command Registry",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("CommandRegistry", "OneRegistrationManySurfaces", "A command projects to host, menu, palette, and slash",
                        _ =>
                        {
                            int ran = 0;
                            CommandRegistry registry = new CommandRegistry();
                            registry.Add(new Command("open", "Open File", () => ran++, "File", KeyChord.Parse("ctrl+o"), new[] { "open" }));
                            registry.Add(new Command("quit", "Quit", () => ran++, "File", KeyChord.Parse("ctrl+q")));

                            using (HeadlessBackend backend = new HeadlessBackend(20, 5))
                            using (TuiApplication app = new TuiApplication(backend))
                            {
                                registry.ApplyTo(app);
                            }

                            MenuBar bar = registry.BuildMenuBar();
                            Check.True(bar != null, "menu bar built");

                            FuzzyList<Command> palette = registry.BuildPalette();
                            palette.Query = "Quit";
                            Check.Equal("quit", palette.SelectedItem!.Id, "palette matched title");

                            Command? resolved = registry.ResolveSlash("/open somefile.txt");
                            Check.True(resolved != null, "slash resolved");
                            Check.Equal("open", resolved!.Id, "slash matched alias/id");
                            resolved.Handler();
                            Check.Equal(1, ran, "handler ran once");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CommandRegistry", "SlashUnknown", "Unknown slash input resolves to null",
                        _ =>
                        {
                            CommandRegistry registry = new CommandRegistry();
                            registry.Add(new Command("open", "Open", () => { }, "File"));
                            Check.True(registry.ResolveSlash("/nope") == null, "unknown returns null");
                            Check.True(registry.ResolveSlash("/") == null, "bare slash returns null");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CommandRegistry", "DisabledFiltered", "Disabled commands are absent from the palette",
                        _ =>
                        {
                            CommandRegistry registry = new CommandRegistry();
                            registry.Add(new Command("a", "Alpha", () => { }, "G", null, null, () => true));
                            registry.Add(new Command("b", "Bravo", () => { }, "G", null, null, () => false));
                            FuzzyList<Command> palette = registry.BuildPalette();
                            Check.Equal(1, palette.MatchCount, "only the enabled command is in the palette");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CommandRegistry", "Guards", "Command and registry reject bad arguments",
                        _ =>
                        {
                            Check.Throws<ArgumentException>(() => new Command("", "T", () => { }), "empty id");
                            Check.Throws<ArgumentException>(() => new Command("i", "", () => { }), "empty title");
                            Check.Throws<ArgumentNullException>(() => new Command("i", "T", null!), "null handler");

                            CommandRegistry registry = new CommandRegistry();
                            registry.Add(new Command("dup", "One", () => { }));
                            Check.Throws<ArgumentException>(() => registry.Add(new Command("dup", "Two", () => { })), "duplicate id");
                            Check.Throws<ArgumentNullException>(() => registry.Add(null!), "null command");
                            Check.Throws<ArgumentNullException>(() => registry.ApplyTo(null!), "null app");
                            Check.Throws<ArgumentNullException>(() => registry.ResolveSlash(null!), "null slash input");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
