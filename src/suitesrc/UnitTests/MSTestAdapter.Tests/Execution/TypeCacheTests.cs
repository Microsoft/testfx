// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Reflection;

    using global::MSTestAdapter.TestUtilities;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    //using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;

    using Moq;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TypeCacheTests
    {
        private TypeCache typeCache;

        private Mock<ReflectHelper> mockReflectHelper;
        
        private TestablePlatformServiceProvider testablePlatformServiceProvider;

        [TestInitialize]
        public void TestInit()
        {
            this.mockReflectHelper = new Mock<ReflectHelper>();
            this.typeCache = new TypeCache(this.mockReflectHelper.Object);   

            this.testablePlatformServiceProvider = new TestablePlatformServiceProvider();
            PlatformServiceProvider.Instance = this.testablePlatformServiceProvider;

            this.SetupMocks();
        }

        #region GetTestMethodInfo tests

        [TestMethod]
        public void GetTestMethodInfoShouldThrowIfTestMethodIsNull()
        {
            var testMethod = new TestMethod("M", "C", "A", isAsync: false);
            Action a = () => this.typeCache.GetTestMethodInfo(
                null,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));


            Assert.ThrowsException<ArgumentNullException>(a);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldThrowIfTestContextIsNull()
        {
            var testMethod = new TestMethod("M", "C", "A", isAsync: false);
            Action a = () => this.typeCache.GetTestMethodInfo(testMethod, null);
            
            Assert.ThrowsException<ArgumentNullException>(a);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldReturnNullIfClassInfoForTheMethodIsNull()
        {
            var testMethod = new TestMethod("M", "C", "A", isAsync: false);
            
            Assert.IsNull(
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>())));
        }

        [TestMethod]
        public void GetTestMethodInfoShouldReturnNullIfLoadingTypeThrowsTypeLoadException()
        {
            var testMethod = new TestMethod("M", "System.TypedReference[]", "A", isAsync: false);

            Assert.IsNull(
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>())));
        }

        [TestMethod]
        public void GetTestMethodInfoShouldThrowIfLoadingTypeThrowsException()
        {
            var testMethod = new TestMethod("M", "C", "A", isAsync: false);

            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>()))
                .Throws(new Exception("Load failure"));

            Action action = () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var exception = ActionUtility.PerformActionAndReturnException(action);

            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);
            StringAssert.StartsWith(exception.Message, "Unable to get type C. Error: System.Exception: Load failure");
        }

        [TestMethod]
        public void GetTestMethodInfoShouldThrowIfTypeDoesNotHaveADefaultConstructor()
        {
            string className = typeof(DummyTestClassWithNoDefaultConstructor).FullName;
            var testMethod = new TestMethod("M", className, "A", isAsync: false);
            
            Action action = () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var exception = ActionUtility.PerformActionAndReturnException(action);

            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);
            StringAssert.StartsWith(exception.Message, "Unable to get default constructor for class " + className);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldThrowIfTestContextHasATypeMismatch()
        {
            string className = typeof(DummyTestClassWithIncorrectTestContextType).FullName;
            var testMethod = new TestMethod("M", className, "A", isAsync: false);

            Action action = () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var exception = ActionUtility.PerformActionAndReturnException(action);

            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);
            StringAssert.StartsWith(exception.Message, string.Format("The {0}.TestContext has incorrect type.", className));
        }

        [TestMethod]
        public void GetTestMethodInfoShouldThrowIfTestContextHasMultipleAmbiguousTestContextProperties()
        {
            string className = typeof(DummyTestClassWithMultipleTestContextProperties).FullName;
            var testMethod = new TestMethod("M", className, "A", isAsync: false);

            Action action = () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var exception = ActionUtility.PerformActionAndReturnException(action);

            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);
            StringAssert.StartsWith(exception.Message, string.Format("Unable to find property {0}.TestContext. Error:{1}.", className, "Ambiguous match found."));
        }

        [TestMethod]
        public void GetTestMethodInfoShouldSetTestContextIfPresent()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));
            
            Assert.IsNotNull(testMethodInfo);
            Assert.IsNotNull(testMethodInfo.Parent.TestContextProperty);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldSetTestContextToNullIfNotPresent()
        {
            var type = typeof(DummyTestClassWithInitializeMethods);
            var methodInfo = type.GetMethod("TestInit");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.IsNotNull(testMethodInfo);
            Assert.IsNull(testMethodInfo.Parent.TestContextProperty);
        }

        #region Assembly Info Creation tests.

        [TestMethod]
        public void GetTestMethodInfoShouldAddAssemblyInfoToTheCache()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(1, this.typeCache.AssemblyInfoCache.Count());
        }

        [TestMethod]
        public void GetTestMethodInfoShouldNotThrowIfWeFailToDiscoverTypeFromAnAssembly()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(It.IsAny<Type>(), typeof(UTF.TestClassAttribute), true)).Throws(new Exception());

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(typeof(DummyTestClassWithTestMethods), typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(1, this.typeCache.AssemblyInfoCache.Count());
        }

        [TestMethod]
        public void GetTestMethodInfoShouldCacheAssemblyInitializeAttribute()
        {
            var type = typeof(DummyTestClassWithInitializeMethods);
            var testMethod = new TestMethod("TestInit", type.FullName, "A", isAsync: false);
            
            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.AssemblyInitializeAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(1, this.typeCache.AssemblyInfoCache.Count());
            Assert.AreEqual(type.GetMethod("AssemblyInit"), this.typeCache.AssemblyInfoCache.ToArray()[0].AssemblyInitializeMethod);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldCacheAssemblyCleanupAttribute()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var testMethod = new TestMethod("TestCleanup", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(1, this.typeCache.AssemblyInfoCache.Count());
            Assert.AreEqual(type.GetMethod("AssemblyCleanup"), this.typeCache.AssemblyInfoCache.ToArray()[0].AssemblyCleanupMethod);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldCacheAssemblyInitAndCleanupAttribute()
        {
            var type = typeof(DummyTestClassWithInitAndCleanupMethods);
            var testMethod = new TestMethod("TestInitOrCleanup", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.AssemblyInitializeAttribute), false)).Returns(true);
            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(1, this.typeCache.AssemblyInfoCache.Count());
            Assert.AreEqual(type.GetMethod("AssemblyCleanup"), this.typeCache.AssemblyInfoCache.ToArray()[0].AssemblyCleanupMethod);
            Assert.AreEqual(type.GetMethod("AssemblyInit"), this.typeCache.AssemblyInfoCache.ToArray()[0].AssemblyInitializeMethod);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldThrowIfAssemblyInitHasIncorrectSignature()
        {
            var type = typeof(DummyTestClassWithIncorrectInitializeMethods);
            var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.AssemblyInitializeAttribute), false)).Returns(true);

            Action a =
                () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var exception = ActionUtility.PerformActionAndReturnException(a);
            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);

            var methodInfo = type.GetMethod("AssemblyInit");
            var expectedMessage =
                string.Format(
                    "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should take a single parameter of type TestContext. Additionally, if you are using async-await in method then return-type must be Task.",
                    methodInfo.DeclaringType.FullName,
                    methodInfo.Name);

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldThrowIfAssemblyCleanupHasIncorrectSignature()
        {
            var type = typeof(DummyTestClassWithIncorrectCleanupMethods);
            var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(true);

            Action a =
                () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var exception = ActionUtility.PerformActionAndReturnException(a);
            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);

            var methodInfo = type.GetMethod("AssemblyCleanup");
            var expectedMessage =
                string.Format(
                    "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be Task.",
                    methodInfo.DeclaringType.FullName,
                    methodInfo.Name);

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldCacheAssemblyInfoInstanceAndReuseTheCache()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);
            
            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            this.mockReflectHelper.Verify(rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true), Times.Once);
            Assert.AreEqual(1, this.typeCache.AssemblyInfoCache.Count());
        }

        #endregion

        #region ClassInfo Creation tests.

        [TestMethod]
        public void GetTestMethodInfoShouldAddClassInfoToTheCache()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.IsNull(this.typeCache.ClassInfoCache.ToArray()[0].TestInitializeMethod);
            Assert.IsNull(this.typeCache.ClassInfoCache.ToArray()[0].TestCleanupMethod);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldCacheClassInitializeAttribute()
        {
            var type = typeof(DummyTestClassWithInitializeMethods);
            var testMethod = new TestMethod("TestInit", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.ClassInitializeAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.AreEqual(type.GetMethod("AssemblyInit"), this.typeCache.ClassInfoCache.ToArray()[0].ClassInitializeMethod);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldCacheClassCleanupAttribute()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var testMethod = new TestMethod("TestCleanup", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.AreEqual(type.GetMethod("AssemblyCleanup"), this.typeCache.ClassInfoCache.ToArray()[0].ClassCleanupMethod);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldCacheClassInitAndCleanupAttribute()
        {
            var type = typeof(DummyTestClassWithInitAndCleanupMethods);
            var testMethod = new TestMethod("TestInitOrCleanup", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.ClassInitializeAttribute), false)).Returns(true);
            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.AreEqual(type.GetMethod("AssemblyInit"), this.typeCache.ClassInfoCache.ToArray()[0].ClassInitializeMethod);
            Assert.AreEqual(type.GetMethod("AssemblyCleanup"), this.typeCache.ClassInfoCache.ToArray()[0].ClassCleanupMethod);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldThrowIfClassInitHasIncorrectSignature()
        {
            var type = typeof(DummyTestClassWithIncorrectInitializeMethods);
            var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyInit"), typeof(UTF.ClassInitializeAttribute), false)).Returns(true);

            Action a =
                () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var exception = ActionUtility.PerformActionAndReturnException(a);
            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);

            var methodInfo = type.GetMethod("AssemblyInit");
            var expectedMessage =
                string.Format(
                    "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should take a single parameter of type TestContext. Additionally, if you are using async-await in method then return-type must be Task.",
                    methodInfo.DeclaringType.FullName,
                    methodInfo.Name);

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldThrowIfClassCleanupHasIncorrectSignature()
        {
            var type = typeof(DummyTestClassWithIncorrectCleanupMethods);
            var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(true);

            Action a =
                () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var exception = ActionUtility.PerformActionAndReturnException(a);
            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);

            var methodInfo = type.GetMethod("AssemblyCleanup");
            var expectedMessage =
                string.Format(
                    "Method {0}.{1} has wrong signature. The method must be static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be Task.",
                    methodInfo.DeclaringType.FullName,
                    methodInfo.Name);

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldCacheTestInitializeAttribute()
        {
            var type = typeof(DummyTestClassWithInitializeMethods);
            var testMethod = new TestMethod("TestInit", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("TestInit"), typeof(UTF.TestInitializeAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.AreEqual(type.GetMethod("TestInit"), this.typeCache.ClassInfoCache.ToArray()[0].TestInitializeMethod);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldCacheTestCleanupAttribute()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var testMethod = new TestMethod("TestCleanup", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("TestCleanup"), typeof(UTF.TestCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.AreEqual(type.GetMethod("TestCleanup"), this.typeCache.ClassInfoCache.ToArray()[0].TestCleanupMethod);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldThrowIfTestInitOrCleanupHasIncorrectSignature()
        {
            var type = typeof(DummyTestClassWithIncorrectInitializeMethods);
            var testMethod = new TestMethod("M", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("TestInit"), typeof(UTF.TestInitializeAttribute), false)).Returns(true);

            Action a =
                () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var exception = ActionUtility.PerformActionAndReturnException(a);

            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);

            var methodInfo = type.GetMethod("TestInit");
            var expectedMessage =
                string.Format(
                    "Method {0}.{1} has wrong signature. The method must be non-static, public, does not return a value and should not take any parameter. Additionally, if you are using async-await in method then return-type must be Task.",
                    methodInfo.DeclaringType.FullName,
                    methodInfo.Name);

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldCacheTestInitializeAttributeDefinedInBaseClass()
        {
            var type = typeof(DummyDerivedTestClassWithInitializeMethods);
            var baseType = typeof(DummyTestClassWithInitializeMethods);
            var testMethod = new TestMethod("TestMehtod", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(baseType.GetMethod("TestInit"), typeof(UTF.TestInitializeAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.AreEqual(baseType.GetMethod("TestInit"), this.typeCache.ClassInfoCache.ToArray()[0].BaseTestInitializeMethodsQueue.Peek());
        }

        [TestMethod]
        public void GetTestMethodInfoShouldCacheTestCleanupAttributeDefinedInBaseClass()
        {
            var type = typeof(DummyDerivedTestClassWithCleanupMethods);
            var baseType = typeof(DummyTestClassWithCleanupMethods);
            var testMethod = new TestMethod("TestMehtod", type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(baseType.GetMethod("TestCleanup"), typeof(UTF.TestCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
            Assert.AreEqual(baseType.GetMethod("TestCleanup"), this.typeCache.ClassInfoCache.ToArray()[0].BaseTestCleanupMethodsQueue.Peek());
        }

        [TestMethod]
        public void GetTestMethodInfoShouldCacheClassInfoInstanceAndReuseFromCache()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            this.testablePlatformServiceProvider.MockFileOperations.Verify(fo => fo.LoadAssembly(It.IsAny<string>()), Times.Once);
            Assert.AreEqual(1, this.typeCache.ClassInfoCache.Count());
        }

        #endregion

        #region Method resolution tests

        [TestMethod]
        public void GetTestMethodInfoShouldThrowIfTestMethodHasIncorrectSignatureOrCannotBeFound()
        {
            var type = typeof(DummyTestClassWithIncorrectTestMethodSignatures);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            Action a =
                () =>
                this.typeCache.GetTestMethodInfo(
                    testMethod,
                    new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var exception = ActionUtility.PerformActionAndReturnException(a);

            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);

            var expectedMessage = string.Format(
                "Method {0}.{1} does not exist.",
                testMethod.FullClassName,
                testMethod.Name);

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldReturnTestMethodInfo()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethod");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));
            
            Assert.AreEqual(methodInfo, testMethodInfo.TestMethod);
            Assert.AreEqual(0, testMethodInfo.Timeout);
            Assert.AreEqual(this.typeCache.ClassInfoCache.ToArray()[0], testMethodInfo.Parent);
            Assert.IsNotNull(testMethodInfo.Executor);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldReturnTestMethodInfoWithTimeout()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithTimeout");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.TimeoutAttribute), false))
                .Returns(true);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(methodInfo, testMethodInfo.TestMethod);
            Assert.AreEqual(10, testMethodInfo.Timeout);
            Assert.AreEqual(this.typeCache.ClassInfoCache.ToArray()[0], testMethodInfo.Parent);
            Assert.IsNotNull(testMethodInfo.Executor);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldThrowWhenTimeoutIsIncorrect()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithIncorrectTimeout");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(rh => rh.IsAttributeDefined(methodInfo, typeof(UTF.TimeoutAttribute), false))
                .Returns(true);

            Action a = () => this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var exception = ActionUtility.PerformActionAndReturnException(a);

            Assert.IsNotNull(exception);
            Assert.IsTrue(exception is TypeInspectionException);

            var expectedMessage =
                string.Format(
                    "UTA054: {0}.{1} has invalid Timeout attribute. The timeout must be a valid integer value and cannot be less than 0.",
                    testMethod.FullClassName,
                    testMethod.Name);

            Assert.AreEqual(expectedMessage, exception.Message);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldReturnTestMethodInfoForMethodsAdornedWithADerivedTestMethodAttribute()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithDerivedTestMethodAttribute");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
            
            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(methodInfo, testMethodInfo.TestMethod);
            Assert.AreEqual(0, testMethodInfo.Timeout);
            Assert.AreEqual(this.typeCache.ClassInfoCache.ToArray()[0], testMethodInfo.Parent);
            Assert.IsNotNull(testMethodInfo.Executor);
            Assert.IsNotNull(testMethodInfo.Executor is DerivedTestMethodAttribute);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldSetTestContextWithCustomProperty()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithCustomProperty");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
            var testContext = new TestContextImplementation(
                testMethod,
                null,
                new Dictionary<string, object>());

            this.typeCache.GetTestMethodInfo(testMethod, testContext);
            var customProperty = testContext.Properties.FirstOrDefault(p => p.Key.Equals("WhoAmI"));

            Assert.IsNotNull(customProperty);
            Assert.AreEqual("Me", customProperty.Value);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldReportWarningIfCustomPropertyHasSameNameAsPredefinedProperties()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithOwnerAsCustomProperty");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
            var testContext = new TestContextImplementation(
                testMethod,
                 null,
                new Dictionary<string, object>());

            var testMethodInfo = this.typeCache.GetTestMethodInfo(testMethod, testContext);
            
            Assert.IsNotNull(testMethodInfo);
            var expectedMessage = string.Format(
                "UTA023: {0}: Cannot define predefined property {2} on method {1}.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name,
                "Owner");
            Assert.AreEqual(expectedMessage, testMethodInfo.NotRunnableReason);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldReportWarningIfCustomPropertyNameIsEmpty()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithEmptyCustomPropertyName");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
            var testContext = new TestContextImplementation(
                testMethod,
                null,
                new Dictionary<string, object>());

            var testMethodInfo = this.typeCache.GetTestMethodInfo(testMethod, testContext);

            Assert.IsNotNull(testMethodInfo);
            var expectedMessage = string.Format(
                "UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);
            Assert.AreEqual(expectedMessage, testMethodInfo.NotRunnableReason);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldReportWarningIfCustomPropertyNameIsNull()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithNullCustomPropertyName");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
            var testContext = new TestContextImplementation(
                testMethod,
                null,
                new Dictionary<string, object>());

            var testMethodInfo = this.typeCache.GetTestMethodInfo(testMethod, testContext);

            Assert.IsNotNull(testMethodInfo);
            var expectedMessage = string.Format(
                "UTA021: {0}: Null or empty custom property defined on method {1}. The custom property must have a valid name.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name);
            Assert.AreEqual(expectedMessage, testMethodInfo.NotRunnableReason);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldReportWarningIfCustomPropertyIsdefinedMultipleTimes()
        {
            var type = typeof(DummyTestClassWithTestMethods);
            var methodInfo = type.GetMethod("TestMethodWithDuplicateCustomPropertyNames");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);
            var testContext = new TestContextImplementation(
                testMethod,
                null,
                new Dictionary<string, object>());

            var testMethodInfo = this.typeCache.GetTestMethodInfo(testMethod, testContext);

            Assert.IsNotNull(testMethodInfo);
            var expectedMessage = string.Format(
                "UTA022: {0}.{1}: The custom property \"{2}\" is already defined. Using \"{3}\" as value.",
                methodInfo.DeclaringType.FullName,
                methodInfo.Name, "WhoAmI", "Me");
            Assert.AreEqual(expectedMessage, testMethodInfo.NotRunnableReason);
        }

        [TestMethod]
        public void GetTestMethodInfoShouldReturnTestMethodInfoForDerivedTestClasses()
        {
            var type = typeof(DerivedTestClass);
            var methodInfo = type.GetRuntimeMethod("DummyTestMethod", new Type[] {});
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            var testMethodInfo = this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            Assert.AreEqual(methodInfo, testMethodInfo.TestMethod);
            Assert.AreEqual(0, testMethodInfo.Timeout);
            Assert.AreEqual(this.typeCache.ClassInfoCache.ToArray()[0], testMethodInfo.Parent);
            Assert.IsNotNull(testMethodInfo.Executor);
        }

        #endregion

        #endregion

        #region ClassInfoListWithExecutableCleanupMethods tests

        [TestMethod]
        public void ClassInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenClassInfoCacheIsEmpty()
        {
            var cleanupMethods = this.typeCache.ClassInfoListWithExecutableCleanupMethods;

            Assert.IsNotNull(cleanupMethods);
            Assert.AreEqual(0, cleanupMethods.Count());
        }

        [TestMethod]
        public void ClassInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenClassInfoCacheDoesNotHaveTestCleanupMethods()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var methodInfo = type.GetMethod("TestCleanup");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("TestCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(false);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var cleanupMethods = this.typeCache.ClassInfoListWithExecutableCleanupMethods;

            Assert.IsNotNull(cleanupMethods);
            Assert.AreEqual(0, cleanupMethods.Count());
        }

        [TestMethod]
        public void ClassInfoListWithExecutableCleanupMethodsShouldReturnClassInfosWithExecutableCleanupMethods()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var methodInfo = type.GetMethod("TestCleanup");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.ClassCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var cleanupMethods = this.typeCache.ClassInfoListWithExecutableCleanupMethods;

            Assert.IsNotNull(cleanupMethods);
            Assert.AreEqual(1, cleanupMethods.Count());
            Assert.AreEqual(type.GetMethod("AssemblyCleanup"), cleanupMethods.ToArray()[0].ClassCleanupMethod);
        }

        #endregion

        #region AssemblyInfoListWithExecutableCleanupMethods tests

        [TestMethod]
        public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenAssemblyInfoCacheIsEmpty()
        {
            var cleanupMethods = this.typeCache.AssemblyInfoListWithExecutableCleanupMethods;

            Assert.IsNotNull(cleanupMethods);
            Assert.AreEqual(0, cleanupMethods.Count());
        }

        [TestMethod]
        public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnEmptyListWhenAssemblyInfoCacheDoesNotHaveTestCleanupMethods()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var methodInfo = type.GetMethod("TestCleanup");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(false);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var cleanupMethods = this.typeCache.AssemblyInfoListWithExecutableCleanupMethods;

            Assert.IsNotNull(cleanupMethods);
            Assert.AreEqual(0, cleanupMethods.Count());
        }

        [TestMethod]
        public void AssemblyInfoListWithExecutableCleanupMethodsShouldReturnAssemblyInfosWithExecutableCleanupMethods()
        {
            var type = typeof(DummyTestClassWithCleanupMethods);
            var methodInfo = type.GetMethod("TestCleanup");
            var testMethod = new TestMethod(methodInfo.Name, type.FullName, "A", isAsync: false);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type, typeof(UTF.TestClassAttribute), true)).Returns(true);

            this.mockReflectHelper.Setup(
                rh => rh.IsAttributeDefined(type.GetMethod("AssemblyCleanup"), typeof(UTF.AssemblyCleanupAttribute), false)).Returns(true);

            this.typeCache.GetTestMethodInfo(
                testMethod,
                new TestContextImplementation(testMethod, null, new Dictionary<string, object>()));

            var cleanupMethods = this.typeCache.AssemblyInfoListWithExecutableCleanupMethods;

            Assert.IsNotNull(cleanupMethods);
            Assert.AreEqual(1, cleanupMethods.Count());
            Assert.AreEqual(type.GetMethod("AssemblyCleanup"), cleanupMethods.ToArray()[0].AssemblyCleanupMethod);
        }

        #endregion

        private void SetupMocks()
        {
            this.testablePlatformServiceProvider.MockFileOperations.Setup(fo => fo.LoadAssembly(It.IsAny<string>()))
                .Returns(Assembly.GetExecutingAssembly());
        }

        #region dummy implementations

        private class DummyTestClassWithNoDefaultConstructor
        {
            private DummyTestClassWithNoDefaultConstructor(int a)
            {
            }
        }

        private class DummyTestClassWithIncorrectTestContextType
        {
            // This is TP.TF type.
            public virtual TestContext TestContext { get; set; }
        }

        private class DummyTestClassWithTestContextProperty : DummyTestClassWithIncorrectTestContextType
        {
            public string TestContext { get; set; }
        }

        private class DummyTestClassWithMultipleTestContextProperties : DummyTestClassWithTestContextProperty
        {
        }

        [UTF.TestClass]
        private class DummyTestClassWithInitializeMethods
        {
            public static void AssemblyInit(UTF.TestContext tc)
            {
            }

            public void TestInit()
            {
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithCleanupMethods
        {
            public static void AssemblyCleanup()
            {
            }

            public void TestCleanup()
            {
            }
        }

        [UTF.TestClass]
        private class DummyDerivedTestClassWithInitializeMethods : DummyTestClassWithInitializeMethods
        {
            public void TestMehtod()
            {
            }
        }

        [UTF.TestClass]
        private class DummyDerivedTestClassWithCleanupMethods : DummyTestClassWithCleanupMethods
        {
            public void TestMehtod()
            {
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithInitAndCleanupMethods
        {
            public static void AssemblyInit(UTF.TestContext tc)
            {
            }
            
            public static void AssemblyCleanup()
            {
            }

            public void TestInitOrCleanup()
            {
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithIncorrectInitializeMethods
        {
            public void AssemblyInit(UTF.TestContext tc)
            {
            }

            public static void TestInit(int i)
            {
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithIncorrectCleanupMethods
        {
            public void AssemblyCleanup()
            {
            }

            public static void TestCleanup(int i)
            {
            }
        }

        [UTF.TestClass]
        internal class DummyTestClassWithTestMethods
        {
            public UTF.TestContext TestContext { get; set; }

            [UTF.TestMethod]
            public void TestMethod()
            {
            }

            [DerivedTestMethod]
            public void TestMethodWithDerivedTestMethodAttribute()
            {
            }

            [UTF.TestMethod]
            [UTF.Timeout(10)]
            public void TestMethodWithTimeout()
            {
            }

            [UTF.TestMethod]
            [UTF.Timeout(-10)]
            public void TestMethodWithIncorrectTimeout()
            {
            }

            [UTF.TestMethod]
            [UTF.TestProperty("WhoAmI", "Me")]
            public void TestMethodWithCustomProperty()
            {
            }

            [UTF.TestMethod]
            [UTF.TestProperty("Owner", "You")]
            public void TestMethodWithOwnerAsCustomProperty()
            {
            }

            [UTF.TestMethod]
            [UTF.TestProperty("", "You")]
            public void TestMethodWithEmptyCustomPropertyName()
            {
            }

            [UTF.TestMethod]
            [UTF.TestProperty(null, "You")]
            public void TestMethodWithNullCustomPropertyName()
            {
            }

            [UTF.TestMethod]
            [UTF.TestProperty("WhoAmI", "Me")]
            [UTF.TestProperty("WhoAmI", "Me2")]
            public void TestMethodWithDuplicateCustomPropertyNames()
            {
            }
        }

        [UTF.TestClass]
        private class DummyTestClassWithIncorrectTestMethodSignatures
        {
            public static void TestMethod()
            {
            }
        }

        private class DerivedTestMethodAttribute : UTF.TestMethodAttribute
        {
        }

        [UTF.TestClass]
        internal class DerivedTestClass : BaseTestClass
        {
        }

        internal class BaseTestClass
        {
            [UTF.TestMethod]
            public void DummyTestMethod()
            {
            }
        }

        #endregion
    }
}
