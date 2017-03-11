
using FluentAssertions;
using System;

namespace CK.Reflection.Tests
{
    public static class Should
    {
        public static void Throw<T>(Action a) where T : Exception => a.ShouldThrow<T>();
    }

#if !NET452
    class  ExcludeFromCodeCoverageAttribute : Attribute
    {
    }
#endif
}
