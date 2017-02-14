// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    /// <summary>
    /// Internal implementation of TestContext exposed to the user.
    /// </summary>
    /// <remarks>
    /// The virtual string properties of the TestContext are retreived from the property dictionary
    /// like GetProperty<string>("TestName") or GetProperty<string>("FullyQualifiedTestClassName");
    /// </remarks>
    public class TestContextImplementation : UTF.TestContext, ITestContext
    {
        private static readonly string FullyQualifiedTestClassNameLabel = "FullyQualifiedTestClassName";
        private static readonly string TestNameLabel = "TestName";

        /// <summary>
        /// Properties
        /// </summary>
        private IDictionary<string, object> properties;

        /// <summary>
        /// Unit test outcome
        /// </summary>
        private UTF.UnitTestOutcome outcome;

        /// <summary>
        /// Test Method
        /// </summary>
        private ITestMethod testMethod;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestContextImplementation"/> class.
        /// </summary>
        /// <param name="testMethod"> The test method. </param>
        /// <param name="writer"> A writer for logging. </param>
        /// <param name="properties"> The properties. </param>
        public TestContextImplementation(ITestMethod testMethod, StringWriter writer, IDictionary<string, object> properties)
        {
            Debug.Assert(testMethod != null, "TestMethod is not null");
            Debug.Assert(properties != null, "properties is not null");

            this.testMethod = testMethod;
            this.properties = new Dictionary<string, object>(properties);

            this.InitializeProperties();
        }

        #region TestContext impl

        // Summary:
        //     You can use this property in a TestCleanup method to determine the outcome
        //     of a test that has run.
        //
        // Returns:
        //     A Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome that states
        //     the outcome of a test that has run.
        public override UTF.UnitTestOutcome CurrentTestOutcome
        {
            get
            {
                return this.outcome;
            }
        }

        /// <summary>
        /// Gets fully-qualified name of the class containing the test method currently being executed
        /// </summary>
        /// <remarks>
        /// This property can be useful in attributes derived from ExpectedExceptionBaseAttribute.
        /// Those attributes have access to the test context, and provide messages that are included
        /// in the test results. Users can benefit from messages that include the fully-qualified
        /// class name in addition to the name of the test method currently being executed.
        /// </remarks>
        public override string FullyQualifiedTestClassName
        {
            get
            {
                return this.GetPropertyValue(FullyQualifiedTestClassNameLabel) as string;
            }
        }

        /// <summary>
        /// Gets name of the test method currently being executed
        /// </summary>
        public override string TestName
        {
            get
            {
                return this.GetPropertyValue(TestNameLabel) as string;
            }
        }

        /// <summary>
        /// Gets the test properties when overridden in a derived class.
        /// </summary>
        /// <returns>
        /// An System.Collections.IDictionary object that contains key/value pairs that
        ///  represent the test properties.
        /// </returns>
        public override IDictionary<string, object> Properties
        {
            get
            {
                return this.properties as IDictionary<string, object>;
            }
        }

        public UTF.TestContext Context
        {
            get
            {
                return this as UTF.TestContext;
            }
        }

        /// <summary>
        /// Set the unit-test outcome
        /// </summary>
        /// <param name="outcome">The test outcome.</param>
        public void SetOutcome(UTF.UnitTestOutcome outcome)
        {
            this.outcome = outcome;
        }

        /// <summary>
        /// Returns whether property with parameter name is present or not
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <param name="propertyValue">Property value.</param>
        /// <returns>True if property with parameter name is present.</returns>
        public bool TryGetPropertyValue(string propertyName, out object propertyValue)
        {
            if (this.properties == null)
            {
                propertyValue = null;
                return false;
            }

            return this.properties.TryGetValue(propertyName, out propertyValue);
        }

        /// <summary>
        /// Adds the parameter name/value pair to property bag
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <param name="propertyValue">Property value.</param>
        public void AddProperty(string propertyName, string propertyValue)
        {
            if (this.properties == null)
            {
                this.properties = new Dictionary<string, object>();
            }

            this.properties.Add(propertyName, propertyValue);
        }

        /// <summary>
        /// Returning null as this feature is not supported in ASP .net and UWP
        /// </summary>
        /// <returns>List of result files. Null presently.</returns>
        public IList<string> GetResultFiles()
        {
            return null;
        }

        #endregion

        /// <summary>
        /// Helper to safely fetch a property value.
        /// </summary>
        /// <param name="propertyName">Property Name</param>
        /// <returns>Property value</returns>
        private object GetPropertyValue(string propertyName)
        {
            object propertyValue = null;
            this.properties.TryGetValue(propertyName, out propertyValue);

            return propertyValue;
        }

        /// <summary>
        /// Helper to initialize the properties.
        /// </summary>
        private void InitializeProperties()
        {
            this.properties[FullyQualifiedTestClassNameLabel] = this.testMethod.FullClassName;
            this.properties[TestNameLabel] = this.testMethod.Name;
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
}
