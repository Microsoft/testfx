// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MSTestAdapter.PlatformServices.Tests.Services
{
#if NETCOREAPP1_0
    using Microsoft.VisualStudio.TestTools.UnitTesting;
#else
    extern alias FrameworkV1;
    extern alias FrameworkV2;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using CollectionAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.CollectionAssert;
    using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UnitTestOutcome = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome;
#endif

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Moq;
    using MSTestAdapter.TestUtilities;
    using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

    [TestClass]
    public class TestContextImplementationTests
    {
        private Mock<ITestMethod> testMethod;

        private IDictionary<string, object> properties;

        private TestContextImplementation testContextImplementation;

        [TestInitialize]
        public void TestInit()
        {
            this.testMethod = new Mock<ITestMethod>();
            this.properties = new Dictionary<string, object>();
        }

        [TestMethod]
        public void TestContextConstructorShouldInitializeProperties()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.IsNotNull(this.testContextImplementation.Properties);
        }

        [TestMethod]
        public void TestContextConstructorShouldInitializeDefaultProperties()
        {
            this.testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");
            this.testMethod.Setup(tm => tm.Name).Returns("M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.IsNotNull(this.testContextImplementation.Properties);

            CollectionAssert.Contains(
                this.testContextImplementation.Properties.ToList(),
                new KeyValuePair<string, object>("FullyQualifiedTestClassName", "A.C.M"));
            CollectionAssert.Contains(
                this.testContextImplementation.Properties.ToList(),
                new KeyValuePair<string, object>("TestName", "M"));
        }

        [TestMethod]
        public void CurrentTestOutcomeShouldReturnDefaultOutcome()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.AreEqual(UnitTestOutcome.Failed, this.testContextImplementation.CurrentTestOutcome);
        }

        [TestMethod]
        public void CurrentTestOutcomeShouldReturnOutcomeSet()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            this.testContextImplementation.SetOutcome(UnitTestOutcome.InProgress);

            Assert.AreEqual(UnitTestOutcome.InProgress, this.testContextImplementation.CurrentTestOutcome);
        }

        [TestMethod]
        public void FullyQualifiedTestClassNameShouldReturnTestMethodsFullClassName()
        {
            this.testMethod.Setup(tm => tm.FullClassName).Returns("A.C.M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.AreEqual("A.C.M", this.testContextImplementation.FullyQualifiedTestClassName);
        }

        [TestMethod]
        public void TestNameShouldReturnTestMethodsName()
        {
            this.testMethod.Setup(tm => tm.Name).Returns("M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.AreEqual("M", this.testContextImplementation.TestName);
        }

        [TestMethod]
        public void PropertiesShouldReturnPropertiesPassedToTestContext()
        {
            var property1 = new KeyValuePair<string, object>("IntProperty", 1);
            var property2 = new KeyValuePair<string, object>("DoubleProperty", 2.023);

            this.properties.Add(property1);
            this.properties.Add(property2);

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            CollectionAssert.Contains(this.testContextImplementation.Properties.ToList(), property1);
            CollectionAssert.Contains(this.testContextImplementation.Properties.ToList(), property2);
        }

        [TestMethod]
        public void ContextShouldReturnTestContextObject()
        {
            this.testMethod.Setup(tm => tm.Name).Returns("M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            Assert.IsNotNull(this.testContextImplementation.Context);
            Assert.AreEqual("M", this.testContextImplementation.Context.TestName);
        }

        [TestMethod]
        public void TryGetPropertyValueShouldReturnTrueIfPropertyIsPresent()
        {
            this.testMethod.Setup(tm => tm.Name).Returns("M");

            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            object propValue;

            Assert.IsTrue(this.testContextImplementation.TryGetPropertyValue("TestName", out propValue));
            Assert.AreEqual("M", propValue);
        }

        [TestMethod]
        public void TryGetPropertyValueShouldReturnFalseIfPropertyIsNotPresent()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            object propValue;

            Assert.IsFalse(this.testContextImplementation.TryGetPropertyValue("Random", out propValue));
            Assert.IsNull(propValue);
        }

        [TestMethod]
        public void AddPropertyShouldAddPropertiesToThePropertyBag()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new System.IO.StringWriter(), this.properties);

            this.testContextImplementation.AddProperty("SomeNewProperty", "SomeValue");

            CollectionAssert.Contains(
                this.testContextImplementation.Properties.ToList(),
                new KeyValuePair<string, object>("SomeNewProperty", "SomeValue"));
        }

        [TestMethod]
        public void WriteLineShouldWriteToStringWriter()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            this.testContextImplementation.WriteLine("{0} Testing write", 1);

            StringAssert.Contains(stringWriter.ToString(), "1 Testing write");
        }

        [TestMethod]
        public void WriteLineShouldWriteToStringWriterForNullCharacters()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            this.testContextImplementation.WriteLine("{0} Testing \0 write \0", 1);

            StringAssert.Contains(stringWriter.ToString(), "1 Testing \\0 write \\0");
        }

        [TestMethod]
        public void WriteLineShouldNotThrowIfStringWriterIsDisposed()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            stringWriter.Dispose();

            this.testContextImplementation.WriteLine("{0} Testing write", 1);

            // Calling it twice to cover the direct return when we know the object has been disposed.
            this.testContextImplementation.WriteLine("{0} Testing write", 1);
        }

        [TestMethod]
        public void WriteLineWithMessageShouldWriteToStringWriter()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            this.testContextImplementation.WriteLine("1 Testing write");

            StringAssert.Contains(stringWriter.ToString(), "1 Testing write");
        }

        [TestMethod]
        public void WriteLineWithMessageShouldWriteToStringWriterForNullCharacters()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            this.testContextImplementation.WriteLine("1 Testing \0 write \0");

            StringAssert.Contains(stringWriter.ToString(), "1 Testing \\0 write \\0");
        }

        [TestMethod]
        public void WriteLineWithMessageShouldNotThrowIfStringWriterIsDisposed()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            stringWriter.Dispose();

            this.testContextImplementation.WriteLine("1 Testing write");

            // Calling it twice to cover the direct return when we know the object has been disposed.
            this.testContextImplementation.WriteLine("1 Testing write");
        }

        [TestMethod]
        public void GetDiagnosticMessagesShouldReturnMessagesFromWriteLine()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new StringWriter(), this.properties);

            this.testContextImplementation.WriteLine("1 Testing write");
            this.testContextImplementation.WriteLine("2 Its a happy day");

            StringAssert.Contains(this.testContextImplementation.GetDiagnosticMessages(), "1 Testing write");
            StringAssert.Contains(this.testContextImplementation.GetDiagnosticMessages(), "2 Its a happy day");
        }

        [TestMethod]
        public void ClearDiagnosticMessagesShouldClearMessagesFromWriteLine()
        {
            var stringWriter = new StringWriter();
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, stringWriter, this.properties);

            this.testContextImplementation.WriteLine("1 Testing write");
            this.testContextImplementation.WriteLine("2 Its a happy day");

            this.testContextImplementation.ClearDiagnosticMessages();

            Assert.AreEqual(string.Empty, stringWriter.ToString());
        }

        [TestMethod]
        public void AddResultFileShouldAddFiletoResultsFiles()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new StringWriter(), this.properties);

            this.testContextImplementation.AddResultFile("C:\\files\\myfile.txt");
            var resultFile = this.testContextImplementation.GetResultFiles();

            CollectionAssert.Contains(resultFile.ToList(), "C:\\files\\myfile.txt");
        }

        [TestMethod]
        public void AddResultFileShouldThrowIfFileNameIsNull()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new StringWriter(), this.properties);

            var exception = ActionUtility.PerformActionAndReturnException(() => this.testContextImplementation.AddResultFile(null));

            Assert.AreEqual(typeof(ArgumentException), exception.GetType());
            StringAssert.Contains(exception.Message, "The parameter should not be null or empty.");
        }

        [TestMethod]
        public void AddResultFileShouldThrowIfFileNameIsEmpty()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new StringWriter(), this.properties);

            var exception = ActionUtility.PerformActionAndReturnException(() => this.testContextImplementation.AddResultFile(string.Empty));

            Assert.AreEqual(typeof(ArgumentException), exception.GetType());
            StringAssert.Contains(exception.Message, "The parameter should not be null or empty.");
        }

        [TestMethod]
        public void AddResultFileShouldAddMultipleFilestoResultsFiles()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new StringWriter(), this.properties);

            this.testContextImplementation.AddResultFile("C:\\files\\file1.txt");
            this.testContextImplementation.AddResultFile("C:\\files\\files2.html");

            var resultsFiles = this.testContextImplementation.GetResultFiles();

            CollectionAssert.Contains(resultsFiles.ToList(), "C:\\files\\file1.txt");
            CollectionAssert.Contains(resultsFiles.ToList(), "C:\\files\\files2.html");
        }

        [TestMethod]
        public void GetResultFilesShouldReturnNullIfNoAddedResultFiles()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new StringWriter(), this.properties);

            var resultFile = this.testContextImplementation.GetResultFiles();

            Assert.IsNull(resultFile, "No added result files");
        }

        [TestMethod]
        public void GetResultFilesShouldReturnListOfAddedResultFiles()
        {
            this.testContextImplementation = new TestContextImplementation(this.testMethod.Object, new StringWriter(), this.properties);

            this.testContextImplementation.AddResultFile("C:\\files\\myfile.txt");
            this.testContextImplementation.AddResultFile("C:\\files\\myfile2.txt");

            var resultFiles = this.testContextImplementation.GetResultFiles();

            Assert.IsTrue(resultFiles.Count > 0, "GetResultFiles returned added elements");
            CollectionAssert.Contains(resultFiles.ToList(), "C:\\files\\myfile.txt");
            CollectionAssert.Contains(resultFiles.ToList(), "C:\\files\\myfile2.txt");
        }
    }

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName

}
