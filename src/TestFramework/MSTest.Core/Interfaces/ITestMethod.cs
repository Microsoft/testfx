// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Reflection;

    /// <summary>
    /// TestMethod for execution.
    /// </summary>
    public interface ITestMethod
    {
        /// <summary>
        /// Gets the name of test method.
        /// </summary>
        string TestMethodName { get; }

        /// <summary>
        /// Gets the name of test class.
        /// </summary>
        string TestClassName { get; }

        /// <summary>
        /// Gets the return type of test method.
        /// </summary>
        Type ReturnType { get; }

        /// <summary>
        /// Gets the parameters of test method.
        /// </summary>
        ParameterInfo[] ParameterTypes { get; }

        /// <summary>
        /// Gets the methodInfo for test method. Should not be used to invoke test method.
        /// Its just for any other info may needed by extension.
        /// </summary>
        MethodInfo MethodInfo { get; }

        /// <summary>
        /// Invokes the test method.
        /// </summary>
        /// <param name="arguments">
        /// Arguments to pass to test method. (E.g. For data driven)
        /// </param>
        /// <returns>
        /// Result of test method invocation.
        /// </returns>
        /// <remarks>
        /// This call handles asynchronous test methods as well.
        /// </remarks>
        TestResult Invoke(object[] arguments);

        /// <summary>
        /// Get all attributes of the test method.
        /// </summary>
        /// <param name="inherit">
        /// Whether attribute defined in parent class is valid.
        /// </param>
        /// <returns>
        /// All attributes.
        /// </returns>
        Attribute[] GetAllAttributes(bool inherit);

        /// <summary>
        /// Get attribute of specific type.
        /// </summary>
        /// <typeparam name="AttributeType"> System.Attribute type. </typeparam>
        /// <param name="inherit">
        /// Whether attribute defined in parent class is valid.
        /// </param>
        /// <returns>
        /// The attributes of the specified type.
        /// </returns>
        AttributeType[] GetAttributes<AttributeType>(bool inherit)
            where AttributeType : Attribute;
    }
}
