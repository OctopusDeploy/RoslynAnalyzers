using System;

namespace Octopus.RoslynAnalyzers.Testing
{
    public static class Constants
    {
        public static class Types
        {
            internal static readonly string IntegrationTest = "Octopus.IntegrationTests.IntegrationTest";
            internal static readonly string UnitTest = "Octopus.IntegrationTests.UnitTest";
            internal static readonly string XunitFact = "Xunit.FactAttribute";
            internal static readonly string XunitTheory = "Xunit.TheoryAttribute";
        }
    }
}