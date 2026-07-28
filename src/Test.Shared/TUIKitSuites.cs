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
                    ContentSuite.Suite(),
                    MarkdownSuite.Suite(),
                    InputSuite.Suite(),
                    MouseLinkSuite.Suite(),
                    SelectionSuite.Suite(),
                    ModalSuite.Suite(),
                    WidgetSuite.Suite(),
                    HostingSuite.Suite(),
                    DiagnosticsSuite.Suite(),
                    CoverageSuite.Suite()
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
