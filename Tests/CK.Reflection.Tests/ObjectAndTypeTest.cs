#region LGPL License
/*----------------------------------------------------------------------------
* This file (Tests\CK.Reflection.Tests\ObjectAndTypeTest.cs) is part of CiviKey. 
*  
* CiviKey is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Lesser General Public License as published 
* by the Free Software Foundation, either version 3 of the License, or 
* (at your option) any later version. 
*  
* CiviKey is distributed in the hope that it will be useful, 
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the 
* GNU Lesser General Public License for more details. 
* You should have received a copy of the GNU Lesser General Public License 
* along with CiviKey.  If not, see <http://www.gnu.org/licenses/>. 
*  
* Copyright © 2007-2015, 
*     Invenietis <http://www.invenietis.com>,
*     In’Tech INFO <http://www.intechinfo.fr>,
* All rights reserved. 
*-----------------------------------------------------------------------------*/
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;

namespace CK.Reflection.Tests
{
    [ExcludeFromCodeCoverage]
    public class ObjectAndType
    {
        static string _lastCalledName;
        static int _lastCalledParam;

        public class A
        {
            public virtual string SimpleMethod(int i)
            {
                _lastCalledName = "A.SimpleMethod";
                _lastCalledParam = i;
                return i.ToString();
            }

            public static string StaticMethod(int i)
            {
                _lastCalledName = "A.StaticMethod";
                _lastCalledParam = i;
                return i.ToString();
            }
        }

        public class B : A
        {
            public override string SimpleMethod(int i)
            {
                _lastCalledName = "B.SimpleMethod";
                _lastCalledParam = i;
                return i.ToString();
            }
        }

        [Test]
        public void StaticInvoker()
        {
            Type tA = typeof(A);
            Type tB = typeof(B);
            {
                {
                    // Null or MissingMethodException
                    Func<int, int> fsUnk1 = DelegateHelper.GetStaticInvoker<Func<int, int>>(tA, "StaticMethod");
                    fsUnk1.Should().BeNull();

                    Func<int, string> fsUnk2 = DelegateHelper.GetStaticInvoker<Func<int, string>>(tA, "StaticMethodUnk");
                    fsUnk2.Should().BeNull();

                    Should.Throw<MissingMethodException>(() => DelegateHelper.GetStaticInvoker<Func<int, int>>(tA, "StaticMethod", true));
                    Should.Throw<MissingMethodException>(() => DelegateHelper.GetStaticInvoker<Func<int, string>>(tA, "StaticMethodUnk", true));

                    DelegateHelper.GetStaticInvoker<Func<int, int>>(tA, "StaticMethod", false).Should().BeNull();
                    DelegateHelper.GetStaticInvoker<Func<int, string>>(tA, "StaticMethodUnk", false).Should().BeNull();
                }
                // Delegate to the static method.
                Func<int, string> fsA = DelegateHelper.GetStaticInvoker<Func<int, string>>(tA, "StaticMethod");
                fsA.Should().NotBeNull();
                fsA(1).Should().Be("1");
                _lastCalledName.Should().Be("A.StaticMethod");
                _lastCalledParam.Should().Be( 1 );

            }
        }

        [Test]
        public void InstanceInvoker()
        {
            Type tA = typeof(A);
            Type tB = typeof(B);

            {
                // Null or MissingMethodException.
                Func<A, int, int> fUnk1 = DelegateHelper.GetInstanceInvoker<Func<A, int, int>>(tA, "SimpleMethod");
                fUnk1.Should().BeNull();

                Func<A, int, string> fUnk2 = DelegateHelper.GetInstanceInvoker<Func<A, int, string>>(tA, "SimpleMethoddUnk");
                fUnk2.Should().BeNull();

                Should.Throw<MissingMethodException>(() => DelegateHelper.GetInstanceInvoker<Func<A, int, int>>(tA, "SimpleMethod", true));
                Should.Throw<MissingMethodException>(() => DelegateHelper.GetInstanceInvoker<Func<A, int, string>>(tA, "SimpleMethodUnk", true));

                DelegateHelper.GetInstanceInvoker<Func<A, int, int>>(tA, "SimpleMethod", false).Should().BeNull();
                DelegateHelper.GetInstanceInvoker<Func<A, int, string>>(tA, "SimpleMethodUnk", false).Should().BeNull();
            }

            A a = new A();
            B b = new B();
            {
                Func<A, int, string> fA = DelegateHelper.GetInstanceInvoker<Func<A, int, string>>(tA, "SimpleMethod");
                fA(a, 2).Should().Be("2");
                _lastCalledName.Should().Be("A.SimpleMethod");
                _lastCalledParam.Should().Be(2);

                fA(b, 3).Should().Be("3");
                _lastCalledName.Should().Be("B.SimpleMethod", "Calling the virtual method: B method.");
                _lastCalledParam.Should().Be(3);
            }
        }

        [Test]
        public void NonVirtualInstanceInvoker()
        {
            Type tA = typeof(A);
            Type tB = typeof(B);
            {
                // Null or MissingMethodException.
                Func<A, int, int> fUnk1 = DelegateHelper.GetNonVirtualInvoker<Func<A, int, int>>(tA, "SimpleMethod");
                fUnk1.Should().BeNull();

                Func<A, int, string> fUnk2 = DelegateHelper.GetNonVirtualInvoker<Func<A, int, string>>(tA, "SimpleMethoddUnk");
                fUnk2.Should().BeNull();

                Should.Throw<MissingMethodException>(() => DelegateHelper.GetNonVirtualInvoker<Func<A, int, int>>(tA, "SimpleMethod", true));
                Should.Throw<MissingMethodException>(() => DelegateHelper.GetNonVirtualInvoker<Func<A, int, string>>(tA, "SimpleMethodUnk", true));

                DelegateHelper.GetNonVirtualInvoker<Func<A, int, int>>(tA, "SimpleMethod", false).Should().BeNull();
                DelegateHelper.GetNonVirtualInvoker<Func<A, int, string>>(tA, "SimpleMethodUnk", false).Should().BeNull();
            }

            A a = new A();
            B b = new B();
            {
                Func<A, int, string> fA = DelegateHelper.GetNonVirtualInvoker<Func<A, int, string>>(tA, "SimpleMethod");
                fA(a, 20).Should().Be("20");
                _lastCalledName.Should().Be("A.SimpleMethod");
                _lastCalledParam.Should().Be(20);

                fA(b, 30).Should().Be("30");
                _lastCalledName.Should().Be("A.SimpleMethod", "It is the base A method that is called, even if b overrides it.");
                _lastCalledParam.Should().Be(30);
            }
        }
    }
}
