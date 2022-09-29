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
            internal static readonly string SystemThreadingCancellationToken = "System.Threading.CancellationToken";
            internal static readonly string SystemThreadingTasksTask1 = "System.Threading.Tasks.Task`1";
            internal static readonly string SystemThreadingTasksValueTask1 = "System.Threading.Tasks.ValueTask`1";
        }
    }
}