// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    using System;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
    
    /// <summary>
    /// Tests for <see cref="ExceptionExtensions"/> class.
    /// </summary>
    [TestClass]
    public class ExceptionExtensionsTests
    {
        #region GetInnerExceptionOrDefault scenarios

        [TestMethod]
        public void ExceptionGetInnerExceptionOrDefaultReturnsInnerExceptionIfAvailable()
        {
            var innerException = new NotImplementedException("notImplementedException");
            var exceptionWithInnerException = new InvalidOperationException("invalidOperationException", innerException);

            var exception = exceptionWithInnerException.GetInnerExceptionOrDefault();

            Assert.AreSame(innerException, exception);
        }

        [TestMethod]
        public void ExceptionGetInnerExceptionOrDefaultShouldNotThrowForNullException()
        {
            Action action = () => ((Exception)null).GetInnerExceptionOrDefault();

            action();
        }

        [TestMethod]
        public void ExceptionGetInnerExceptionOrDefaultShouldReturnNullForNullException()
        {
            var exception = ((Exception)null).GetInnerExceptionOrDefault();

            Assert.IsNull(exception);
        }

        [TestMethod]
        public void ExceptionGetInnerExceptionOrDefaultShouldReturnExceptionIfInnerExceptionIsNull()
        {
            var exceptionWithNoInnerException = new InvalidOperationException("invalidOperationException", innerException: null);

            var exception = exceptionWithNoInnerException.GetInnerExceptionOrDefault();

            Assert.AreSame(exceptionWithNoInnerException, exception);
        }

        #endregion

        #region TryGetExceptionMessage scenarios

        [TestMethod]
        public void ExceptionTryGetMessageGetsTheExceptionMessage()
        {
            var exception = new Exception("dummyMessage");

            Assert.AreEqual<string>("dummyMessage", exception.TryGetMessage());
        }

        [TestMethod]
        public void ExceptionTryGetMessageReturnsEmptyStringIfExceptionMessageIsNull()
        {
            var exception = new DummyException(() => null);

            Assert.AreEqual(string.Empty, exception.TryGetMessage());
        }

        [TestMethod]
        public void ExceptionTryGetMessageReturnsErrorMessageIfExceptionIsNull()
        {
            var errorMessage = string.Format(Resource.UTF_FailedToGetExceptionMessage, "null");

            var exception = (Exception)null;

            Assert.AreEqual(errorMessage, exception.TryGetMessage());
        }

        [TestMethod]
        public void ExceptionTryGetMessageReturnsErrorMessageIfExceptionMessageThrows()
        {
            var errorMessage = string.Format(Resource.UTF_FailedToGetExceptionMessage, "System.NotImplementedException");

            var exception = new DummyException(() => { throw new NotImplementedException(); });

            Assert.AreEqual(errorMessage, exception.TryGetMessage());
        }

        internal class DummyException : Exception
        {
            private readonly Func<string> getMessage;

            public DummyException(Func<string> message)
            {
                this.getMessage = message;
            }

            public override string Message => this.getMessage();
        }

        #endregion

        #region TryGetStackTraceInformation scenarios

        [TestMethod]
        public void TryGetStackTraceInformationReturnsNullIfExceptionIsNull()
        {
            var exception = (Exception)null;

            Assert.IsNull(exception.TryGetStackTraceInformation());
        }

        [TestMethod]
        public void TryGetStackTraceInformationReturnsNullIfExceptionStackTraceIsNullOrEmpty()
        {
            var exception = new DummyExceptionForStackTrace(() => null);

            Assert.IsNull(exception.TryGetStackTraceInformation());
        }

        [TestMethod]
        public void TryGetStackTraceInformationReturnsStackTraceForAnException()
        {
            var exception = new DummyExceptionForStackTrace(() => "    at A()\r\n    at B()");

            var stackTraceInformation = exception.TryGetStackTraceInformation();

            StringAssert.StartsWith(stackTraceInformation.ErrorStackTrace, "    at A()");
            Assert.IsNull(stackTraceInformation.ErrorFilePath);
            Assert.AreEqual(0, stackTraceInformation.ErrorLineNumber);
        }

        [TestMethod]
        public void TryGetStackTraceInformationReturnsNullIfStackTraceThrows()
        {
            var exception = new DummyExceptionForStackTrace(() => { throw new NotImplementedException(); });

            Assert.IsNull(exception.TryGetStackTraceInformation());
        }

        public class DummyExceptionForStackTrace : Exception
        {
            private readonly Func<string> getStackTrace;

            public DummyExceptionForStackTrace(Func<string> getStackTrace)
            {
                this.getStackTrace = getStackTrace;
            }

            public override string StackTrace => this.getStackTrace();
        }

        #endregion
    }
}