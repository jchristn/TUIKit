namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using Test.Shared.Suites;
    using Touchstone.Core;
    using TUIKit;

    /// <summary>
    /// Central registry of all TUIKit Touchstone test suites. Every runner (Automated, xUnit,
    /// NUnit) consumes <see cref="All"/> so the same descriptors execute everywhere.
    /// </summary>
    public static class TUIKitSuites
    {
        /// <summary>
        /// Gets every test suite descriptor defined for TUIKit. Never null. Suites are added here
        /// as each feature phase lands in <c>TUIKIT_PLAN.md</c>.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    LibrarySuite(),
                    GeometrySuite.Suite(),
                    ColorStyleSuite.Suite(),
                    UnicodeSuite.Suite(),
                    BufferSurfaceSuite.Suite(),
                    StyledTextSuite.Suite(),
                    TerminalSuite.Suite(),
                    RenderSuite.Suite(),
                    LayoutSuite.Suite(),
                    RegionBackgroundSuite.Suite(),
                    ContentSuite.Suite(),
                    MarkdownSuite.Suite(),
                    InputSuite.Suite(),
                    MouseLinkSuite.Suite(),
                    SelectionSuite.Suite(),
                    ModalSuite.Suite(),
                    DialogModalSuite.Suite(),
                    MultiSelectSuite.Suite(),
                    WidgetSuite.Suite(),
                    HostingSuite.Suite(),
                    DiagnosticsSuite.Suite(),
                    CoverageSuite.Suite(),
                    MarkupThemeSuite.Suite(),
                    HostErgonomicsSuite.Suite(),
                    LayoutDslSuite.Suite(),
                    NewWidgetsSuite.Suite(),
                    FocusFormsPromptsSuite.Suite(),
                    RichContentSuite.Suite(),
                    ChartsIconsColorSuite.Suite(),
                    ReactiveAnimationSuite.Suite(),
                    SplitMenuFileSuite.Suite(),
                    VisualEffectsSuite.Suite(),
                    ModalLinkLifecycleSuite.Suite(),
                    KeyBindingSuite.Suite(),
                    ValueValidationSuite.Suite(),
                    LayoutConstraintSuite.Suite(),
                    InputWidgetSuite.Suite(),
                    ImageProtocolSuite.Suite(),
                    HostValidationSuite.Suite(),
                    CoreValidationSuite.Suite(),
                    LayoutThemeValidationSuite.Suite(),
                    InputRoutingValidationSuite.Suite(),
                    BackendModalValidationSuite.Suite(),
                    ContentValidationSuite.Suite(),
                    DiagnosticsValidationSuite.Suite(),
                    WidgetGuardValidationSuite.Suite(),
                    StyledOutputSuite.Suite(),
                    UsabilitySuite.Suite(),
                    UtilitiesSuite.Suite(),
                    PanelsSuite.Suite(),
                    StreamingTranscriptSuite.Suite(),
                    GenericListSuite.Suite(),
                    ListEditingSuite.Suite(),
                    CommandRegistrySuite.Suite(),
                    AutocompleteSuite.Suite(),
                    ScrollFormSuite.Suite()
                };
            }
        }

        /// <summary>
        /// Builds the suite covering library-level metadata. Serves as the end-to-end smoke test
        /// that the Touchstone pipeline is wired correctly across all runners.
        /// </summary>
        /// <returns>The library metadata suite descriptor.</returns>
        public static TestSuiteDescriptor LibrarySuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Library",
                displayName: "Library Metadata",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "Library",
                        caseId: "NameIsTUIKit",
                        displayName: "Library name is TUIKit",
                        executeAsync: _ =>
                        {
                            if (TuiKitLibrary.Name != "TUIKit")
                                throw new InvalidOperationException(
                                    "Expected library name 'TUIKit' but got '" + TuiKitLibrary.Name + "'.");

                            return System.Threading.Tasks.Task.CompletedTask;
                        }),

                    new TestCaseDescriptor(
                        suiteId: "Library",
                        caseId: "VersionNotEmpty",
                        displayName: "Library reports a non-empty version",
                        executeAsync: _ =>
                        {
                            if (string.IsNullOrEmpty(TuiKitLibrary.Version))
                                throw new InvalidOperationException("Library version was null or empty.");

                            return System.Threading.Tasks.Task.CompletedTask;
                        })
                });
        }
    }
}
