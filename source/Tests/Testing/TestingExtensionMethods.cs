using System;

namespace Tests.Testing
{
    public static class TestingExtensionMethods
    {
        public static string WithTestingTypes(this string classStructureToTest)
        {
            return $@"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octopus.IntegrationTests
{{
    public abstract class LoggedTest : IDisposable
    {{
        public void Dispose() {{ }}
    }}

    public abstract class IntegrationTest : LoggedTest 
    {{ 
        readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        public CancellationToken CancellationToken => cancellationTokenSource.Token;
    }}

    public abstract class UnitTest : LoggedTest {{ }}
}}

namespace Octopus.IntegrationTests.Something
{{
{classStructureToTest}
}}
";
        }
    }
}