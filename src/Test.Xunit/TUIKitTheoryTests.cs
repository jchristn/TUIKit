namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using global::Xunit;
    using global::Xunit.Abstractions;

    /// <summary>
    /// Runs each non-skipped TUIKit descriptor as an independent xUnit theory row, giving
    /// per-test visibility in the IDE Test Explorer.
    /// </summary>
    public sealed class TUIKitTheoryTests
    {
        private readonly ITestOutputHelper _Output;

        /// <summary>
        /// Initializes a new instance of the <see cref="TUIKitTheoryTests"/> class.
        /// </summary>
        /// <param name="output">The xUnit output helper used to log the running case name. Must not be null.</param>
        public TUIKitTheoryTests(ITestOutputHelper output)
        {
            _Output = output;
        }

        /// <summary>
        /// Enumerates every non-skipped descriptor across all suites as theory data.
        /// </summary>
        /// <returns>The set of descriptors to execute, one per theory row.</returns>
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();

            foreach (TestSuiteDescriptor suite in TUIKitSuites.All)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    if (!testCase.Skip)
                        data.Add(testCase);
                }
            }

            return data;
        }

        /// <summary>
        /// Executes a single descriptor.
        /// </summary>
        /// <param name="testCase">The descriptor to execute. Must not be null.</param>
        /// <returns>A task that completes when the descriptor has run.</returns>
        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            _Output.WriteLine("Running: " + testCase.DisplayName);
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
