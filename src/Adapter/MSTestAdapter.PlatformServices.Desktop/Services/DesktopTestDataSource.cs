
// Copyright (c) Microsoft. All rights reserved.


namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using Data;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using UTF = TestTools.UnitTesting;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;
    using ObjectModel;
    public class TestDataSource : ITestDataSource
    {

        public bool HasDataDrivenTests(UTF.ITestMethod testMethodInfo)
        {
            UTF.DataSourceAttribute[] dataSourceAttribute = testMethodInfo.GetAttributes<UTF.DataSourceAttribute>(false);
            if (dataSourceAttribute != null && dataSourceAttribute.Length == 1)
                return true;
            return false;
        }

        public UTF.TestResult[] RunDataDrivenTest(UTF.TestContext testContext, UTF.ITestMethod testMethodInfo, ITestMethod testMethod, UTF.TestMethodAttribute executor)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();

            // Figure out where (as well as the current directory) we could look for data files
            // for unit tests this means looking at the the location of the test itself
            List<string> dataFolders = new List<string>();
            dataFolders.Add(Path.GetDirectoryName(new Uri(testMethodInfo.MethodInfo.Module.Assembly.CodeBase).LocalPath));

            List<UTF.TestResult> dataRowResults = new List<UTF.TestResult>();

            // Connect to data source.
            TestDataConnectionFactory factory = new TestDataConnectionFactory();

            string providerNameInvariant;
            string connectionString;
            string tableName;
            UTF.DataAccessMethod dataAccessMethod;

            try
            {
                GetConnectionProperties((testMethodInfo.GetAttributes<UTF.DataSourceAttribute>(false))[0], out providerNameInvariant, out connectionString, out tableName, out dataAccessMethod);
            }
            catch (Exception ex)
            {
                watch.Stop();
                var result = new UTF.TestResult();
                result.Outcome = UTF.UnitTestOutcome.Failed;
                result.TestFailureException = ex;
                result.Duration = watch.Elapsed;
                return new UTF.TestResult[] { result };
            }

            try
            {

                using (TestDataConnection connection = factory.Create(providerNameInvariant, connectionString, dataFolders))
                {
                    DataTable table = connection.ReadTable(tableName, null);
                    DataRow[] rows = table.Select();
                    Debug.Assert(rows != null);

                    if (rows.Length == 0)
                    {
                        watch.Stop();
                        var inconclusiveResult = new UTF.TestResult();
                        inconclusiveResult.Outcome = UTF.UnitTestOutcome.Inconclusive;
                        inconclusiveResult.Duration = watch.Elapsed;
                        return new UTF.TestResult[] { inconclusiveResult };
                    }

                    IEnumerable<int> permutation = GetPermutation(dataAccessMethod, rows.Length);
                    TestContextImplementation testContextImpl = testContext as TestContextImplementation;

                    try
                    {
                        testContextImpl.SetDataConnection(connection.Connection);

                        // For each data row...
                        foreach (int rowIndex in permutation)
                        {
                            watch.Reset();
                            watch.Start();

                            testContextImpl.SetDataRow(rows[rowIndex]);

                            UTF.TestResult[] currentResult = new UTF.TestResult[1];

                            try
                            {
                                currentResult = executor.Execute(testMethodInfo);
                            }
                            catch (Exception ex)
                            {
                                currentResult[0].Outcome = UTF.UnitTestOutcome.Failed;

                                // Trace whole exception but do not show call stack to the user, only show message.
                                EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled,"Unit Test Adapter threw exception: {0}", ex);

                            }

                            currentResult[0].DatarowIndex = rowIndex;

                            watch.Stop();
                            currentResult[0].Duration = watch.Elapsed;

                            Debug.Assert(currentResult[0] != null);
                            dataRowResults.Add(currentResult[0]);

                            // Clear the testContext's internal string writer to start afresh for the next datarow iteration
                            testContextImpl.ClearMessages();
                        }
                    }
                    finally
                    {
                        testContextImpl.SetDataConnection(null);
                        testContextImpl.SetDataRow(null);
                    }
                }
            }
            catch (Exception ex)
            {
                string message = ExceptionExtensions.GetExceptionMessage(ex);
                UTF.TestResult failedResult = new UTF.TestResult();
                failedResult.Outcome = UTF.UnitTestOutcome.Error;
                failedResult.TestFailureException = new Exception(string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorDataConnectionFailed, ex.Message), ex);
                return new UTF.TestResult[] { failedResult };
            }

            return dataRowResults.ToArray();
        }


        /// <summary>
        /// Get permutations for data row access
        /// </summary>
        private IEnumerable<int> GetPermutation(UTF.DataAccessMethod dataAccessMethod, int length)
        {
            switch (dataAccessMethod)
            {
                case UTF.DataAccessMethod.Sequential:
                    return new SequentialIntPermutation(length);

                case UTF.DataAccessMethod.Random:
                    return new RandomIntPermutation(length);

                default:
                    Debug.Fail("Unknown DataAccessMehtod: " + dataAccessMethod);
                    return new SequentialIntPermutation(length);
            }
        }

        /// <summary>
        /// Get connection property based on DataSourceAttribute. If its in config file then read it from config.
        /// </summary>
        private void GetConnectionProperties(UTF.DataSourceAttribute dataSourceAttribute, out string providerNameInvariant, out string connectionString, out string tableName, out UTF.DataAccessMethod dataAccessMethod)
        {
            if (string.IsNullOrEmpty(dataSourceAttribute.DataSourceSettingName) == false)
            {
                UTF.DataSourceElement elem = UTF.TestConfiguration.ConfigurationSection.DataSources[dataSourceAttribute.DataSourceSettingName];
                if (elem == null)
                {
                    throw new Exception(string.Format(CultureInfo.CurrentCulture, Resource.UTA_DataSourceConfigurationSectionMissing, dataSourceAttribute.DataSourceSettingName));
                }

                providerNameInvariant = ConfigurationManager.ConnectionStrings[elem.ConnectionString].ProviderName;
                connectionString = ConfigurationManager.ConnectionStrings[elem.ConnectionString].ConnectionString;
                tableName = elem.DataTableName;
                dataAccessMethod = (UTF.DataAccessMethod)Enum.Parse(typeof(UTF.DataAccessMethod), elem.DataAccessMethod);
            }
            else
            {
                providerNameInvariant = dataSourceAttribute.ProviderInvariantName;
                connectionString = dataSourceAttribute.ConnectionString;
                tableName = dataSourceAttribute.TableName;
                dataAccessMethod = dataSourceAttribute.DataAccessMethod;
            }
        }

        public bool SetContext(string source)
        {
            if (string.IsNullOrEmpty(source))
                return false;

            Exception setWorkingDirectoryException = null;
            currentDirectory = Environment.CurrentDirectory;

            try
            {
                Environment.CurrentDirectory = Path.GetDirectoryName(source);
                EqtTrace.InfoIf(EqtTrace.IsInfoEnabled,"MSTestExecutor: Changed the working directory to {0}", Environment.CurrentDirectory);
                return true;
            }
            catch (IOException ex)
            {
                setWorkingDirectoryException = ex;
            }
            catch (System.Security.SecurityException ex)
            {
                setWorkingDirectoryException = ex;
            }

            if (setWorkingDirectoryException != null)
            {
                EqtTrace.ErrorIf(EqtTrace.IsErrorEnabled, "MSTestExecutor.SetWorkingDirectory: Failed to set the working directory to '{0}'. {1}", Path.GetDirectoryName(source), setWorkingDirectoryException);
            }
            return false;
        }

        public void ResetContext()
        {
            if (!string.IsNullOrEmpty(this.currentDirectory))
            {
                Environment.CurrentDirectory = this.currentDirectory;
            }
        }

        private string currentDirectory = null;
    }
}