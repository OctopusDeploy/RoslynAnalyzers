using System;

namespace Tests.Testing
{
    public static class TestingExtensionMethods
    {
        public static string WithTestingTypes(this string classStructureToTest)
        {
            return $@"
using System;

namespace Octopus.IntegrationTests
{{
    public abstract class LoggedTest : IDisposable
    {{
        public void Dispose() {{ }}
    }}

    public abstract class IntegrationTest : LoggedTest {{ }}

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