using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using FluentAssertions;
using NUnit.Framework;

namespace CK.Reflection.Tests
{
    [ExcludeFromCodeCoverage]
    class AttributesBehaviorTests
    {

        interface IMarker
        {
        }

        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property)]
        class MarkerAttribute : Attribute, IMarker
        {
        }

        [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property)]
        class Marker2Attribute : Attribute, IMarker
        {
        }

        [Marker]
        class Test
        {
            [Marker]
            public void Method() { }

            [Marker]
            [Marker2]
            public void Method2() { }
        }

        [Test]
        public void WorksWithAbstractions()
        {
            typeof(Test).GetTypeInfo().IsDefined(typeof(IMarker), false).Should().Be( true, "IsDefined works with any base type of attributes.");
            typeof(Test).GetMethod("Method").IsDefined(typeof(IMarker), false).Should().Be( true,  "IsDefined works with any base type of attributes.");

            typeof(Test).GetMethod("Method2").IsDefined(typeof(IMarker), false).Should().Be( true,  "IsDefined works with multiple attributes.");
            typeof(Test).GetMethod("Method2").GetCustomAttributes(typeof(IMarker), false).Should().HaveCount(2, "GetCustomAttributes works with multiple base type attributes.");

        }

        [Test]
        public void CreatedEachTimeGetCustomAttributesIsCalled()
        {
            object a1 = typeof(Test).GetMethod("Method").GetCustomAttributes(typeof(IMarker), false).First();
            object a2 = typeof(Test).GetMethod("Method").GetCustomAttributes(typeof(IMarker), false).First();
            a1.Should().NotBeSameAs( a2 );
        }
    }
}
