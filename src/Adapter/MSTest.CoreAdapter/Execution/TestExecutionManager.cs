// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    /// <summary>
    /// Class responsible for execution of tests at assembly level and sending tests via framework handle
    /// </summary>
    public class TestExecutionManager
    {
        /// <summary>
        /// Specifies whether the test run is canceled or not
        /// </summary>
        private TestRunCancellationToken cancellationToken;

        public TestExecutionManager()
        {
            this.TestMethodFilter = new TestMethodFilter();
        }

        /// <summary>
        /// Gets or sets method filter for filtering tests
        /// </summary>
        private TestMethodFilter TestMethodFilter { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether any test executed has failed.
        /// </summary>
        private bool HasAnyTestFailed { get; set; }

        /// <summary>
        /// Runs the tests.
        /// </summary>
        /// <param name="tests">Tests to be run.</param>
        /// <param name="runContext">Context to use when executing the tests.</param>
        /// <param name="frameworkHandle">Handle to the framework to record results and to do framework operations.</param>
        /// <param name="runCancellationToken">Test run cancellation tokenn</param>
        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle, TestRunCancellationToken runCancellationToken)
        {
            Debug.Assert(tests != null, "tests");
            Debug.Assert(runContext != null, "runContext");
            Debug.Assert(frameworkHandle != null, "frameworkHandle");
            Debug.Assert(runCancellationToken != null, "runCancellationToken");

            this.cancellationToken = runCancellationToken;

            var isDeploymentDone = PlatformServiceProvider.Instance.TestDeployment.Deploy(tests, runContext, frameworkHandle);

            // Execute the tests
            this.ExecuteTests(tests, runContext, frameworkHandle, isDeploymentDone);

            if (!this.HasAnyTestFailed)
            {
                PlatformServiceProvider.Instance.TestDeployment.Cleanup();
            }
        }

        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle, TestRunCancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;

            var discoverySink = new TestCaseDiscoverySink();

            var tests = new List<TestCase>();

            // deploy everything first.
            foreach (var source in sources)
            {
                if (this.cancellationToken.Canceled)
                {
                    break;
                }

                var logger = (IMessageLogger)frameworkHandle;

                // discover the tests
                this.GetUnitTestDiscoverer().DiscoverTestsInSource(source, logger, discoverySink, runContext);
                tests.AddRange(discoverySink.Tests);

                // Clear discoverSinksTests so that it just stores test for one source at one point of time
                discoverySink.Tests.Clear();
            }

            bool isDeploymentDone = PlatformServiceProvider.Instance.TestDeployment.Deploy(tests, runContext, frameworkHandle);

            // Run tests.
            this.ExecuteTests(tests, runContext, frameworkHandle, isDeploymentDone);

            if (!this.HasAnyTestFailed)
            {
                PlatformServiceProvider.Instance.TestDeployment.Cleanup();
            }
        }

        /// <summary>
        /// Execute the parameter tests
        /// </summary>
        /// <param name="tests">Tests to execute.</param>
        /// <param name="runContext">The run context.</param>
        /// <param name="frameworkHandle">Handle to record test start/end/results.</param>
        /// <param name="isDeploymentDone">Indicates if deployment is done.</param>
        internal virtual void ExecuteTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle, bool isDeploymentDone)
        {
            var testsBySource = from test in tests
                                group test by test.Source into testGroup
                                select new { Source = testGroup.Key, Tests = testGroup };

            foreach (var group in testsBySource)
            {
                this.ExecuteTestsInSource(group.Tests, runContext, frameworkHandle, group.Source, isDeploymentDone);
            }
        }

        internal virtual UnitTestDiscoverer GetUnitTestDiscoverer()
        {
            return new UnitTestDiscoverer();
        }

        internal void SendTestResults(TestCase test, UnitTestResult[] unitTestResults, DateTimeOffset startTime, DateTimeOffset endTime, ITestExecutionRecorder testExecutionRecorder)
        {
            if (!(unitTestResults?.Length > 0))
            {
                return;
            }

            foreach (var unitTestResult in unitTestResults)
            {
                if (test == null)
                {
                    continue;
                }

                var testResult = unitTestResult.ToTestResult(test, startTime, endTime, MSTestSettings.CurrentSettings.MapInconclusiveToFailed);

                if (unitTestResult.DatarowIndex >= 0)
                {
                    testResult.DisplayName = string.Format(CultureInfo.CurrentCulture, Resource.DataDrivenResultDisplayName, test.DisplayName, unitTestResult.DatarowIndex);
                }

                testExecutionRecorder.RecordEnd(test, testResult.Outcome);

                if (testResult.Outcome == TestOutcome.Failed)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("MSTestExecutor:Test {0} failed. ErrorMessage:{1}, ErrorStackTrace:{2}.", testResult.TestCase.FullyQualifiedName, testResult.ErrorMessage, testResult.ErrorStackTrace);
                    this.HasAnyTestFailed = true;
                }

                try
                {
                    testExecutionRecorder.RecordResult(testResult);
                }
                catch (TestCanceledException)
                {
                    // Ignore this exception
                }
            }
        }

        /// <summary>
        /// Execute the parameter tests present in parameter source
        /// </summary>
        /// <param name="tests">Tests to execute.</param>
        /// <param name="runContext">The run context.</param>
        /// <param name="frameworkHandle">Handle to record test start/end/results.</param>
        /// <param name="source">The test container for the tests.</param>
        /// <param name="isDeploymentDone">Indicates if deployment is done.</param>
        private void ExecuteTestsInSource(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle, string source, bool isDeploymentDone)
        {
            Debug.Assert(!string.IsNullOrEmpty(source), "Source cannot be empty");

            source = isDeploymentDone
                         ? Path.Combine(
                             PlatformServiceProvider.Instance.TestDeployment.GetDeploymentDirectory(),
                             Path.GetFileName(source)) : source;

            using (var isolationHost = PlatformServiceProvider.Instance.CreateTestSourceHost(source, runContext?.RunSettings, frameworkHandle))
            {
                var testRunner = isolationHost.CreateInstanceForType(
                    typeof(UnitTestRunner),
                    new object[] { MSTestSettings.CurrentSettings }) as UnitTestRunner;
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Created unit-test runner {0}", source);

                this.ExecuteTestsWithTestRunner(tests, runContext, frameworkHandle, source, testRunner);

                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                    "Executed tests belonging to source {0}",
                    source);
            }
        }

        private void ExecuteTestsWithTestRunner(
            IEnumerable<TestCase> tests,
            IRunContext runContext,
            ITestExecutionRecorder testExecutionRecorder,
            string source,
            UnitTestRunner testRunner)
        {
            TestCase test = null;
            UnitTestResult[] unitTestResult = null;

            var startTime = DateTimeOffset.MinValue;
            var endTime = DateTimeOffset.MinValue;

            bool filterHasError;
            var filterExpression = this.TestMethodFilter.GetFilterExpression(
                runContext,
                testExecutionRecorder,
                out filterHasError);

            if (!filterHasError)
            {
                var sessionParameters = this.GetSessionParameters(runContext, testExecutionRecorder);

                foreach (var currentTest in tests)
                {
                    // Skip test if not fitting filter criteria.
                    if (filterExpression != null && filterExpression.MatchTestCase(
                            currentTest,
                            (p) => this.TestMethodFilter.PropertyValueProvider(currentTest, p)) == false)
                    {
                        continue;
                    }

                    // Send previous test result.
                    this.SendTestResults(test, unitTestResult, startTime, endTime, testExecutionRecorder);

                    if (this.cancellationToken != null && this.cancellationToken.Canceled)
                    {
                        break;
                    }

                    var unitTestElement = currentTest.ToUnitTestElement(source);
                    testExecutionRecorder.RecordStart(currentTest);

                    startTime = DateTimeOffset.Now;

                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                        "Executing test {0}",
                        unitTestElement.TestMethod.Name);

                    // this is done so that appropriate values of testcontext properties are set at source level
                    // and are merged with session level parameters
                    var sourceLevelParameters = PlatformServiceProvider.Instance.SettingsProvider.GetProperties(source);

                    if (sessionParameters != null && sessionParameters.Count > 0)
                    {
                        sourceLevelParameters = sourceLevelParameters.Concat(sessionParameters).ToDictionary(x => x.Key, x => x.Value);
                    }

                    unitTestResult = testRunner.RunSingleTest(unitTestElement.TestMethod, sourceLevelParameters);

                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                        "Executed test {0}",
                        unitTestElement.TestMethod.Name);

                    endTime = DateTimeOffset.Now;
                    test = currentTest;
                }
            }

            IList<string> warnings = null;
            try
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Executing cleanup methods.");
                var cleanupResult = testRunner.RunCleanup();
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Executed cleanup methods.");

                if (cleanupResult != null)
                {
                    warnings = cleanupResult.Warnings;

                    if (unitTestResult?.Length > 0)
                    {
                        var lastResult = unitTestResult[unitTestResult.Length - 1];
                        lastResult.StandardOut += cleanupResult.StandardOut;
                        lastResult.StandardError += cleanupResult.StandardError;
                        lastResult.DebugTrace += cleanupResult.DebugTrace;
                    }
                }
            }
            finally
            {
                // Send last test result
                this.SendTestResults(test, unitTestResult, startTime, endTime, testExecutionRecorder);
            }

            this.LogWarnings(testExecutionRecorder, warnings);
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle errors in user specified run parameters")]
        private IDictionary<string, object> GetSessionParameters(IRunContext runContext, ITestExecutionRecorder testExecutionRecorder)
        {
            if (!string.IsNullOrEmpty(runContext?.RunSettings?.SettingsXml))
            {
                try
                {
                    return RunSettingsUtilities.GetTestRunParameters(runContext.RunSettings.SettingsXml);
                }
                catch (Exception ex)
                {
                    testExecutionRecorder.SendMessage(TestMessageLevel.Error, ex.Message);
                }
            }

            return new Dictionary<string, object>();
        }

        /// <summary>
        /// Log the parameter warnings on the parameter logger
        /// </summary>
        /// <param name="testExecutionRecorder">Handle to record test start/end/results/messages.</param>
        /// <param name="warnings">Any warnings during run operation.</param>
        private void LogWarnings(ITestExecutionRecorder testExecutionRecorder, IEnumerable<string> warnings)
        {
            if (warnings == null)
            {
                return;
            }

            Debug.Assert(testExecutionRecorder != null, "Logger should not be null");

            // log the warnings
            foreach (string warning in warnings)
            {
                testExecutionRecorder.SendMessage(TestMessageLevel.Warning, warning);
            }
        }
    }
}
