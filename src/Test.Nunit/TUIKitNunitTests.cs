namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs each TUIKit descriptor as an independent NUnit test case via a data source, giving
    /// per-test visibility in the IDE Test Explorer.
    /// </summary>
    [TestFixture]
    public sealed class TUIKitNunitTests
    {
        private static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(TUIKitSuites.All);
        }

        /// <summary>
        /// Executes a single descriptor.
        /// </summary>
        /// <param name="testCase">The descriptor to execute. Must not be null.</param>
        /// <returns>A task that completes when the descriptor has run.</returns>
        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
