using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<Octopus.RoslynAnalyzers.CaseSensitiveEnumParsingAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Tests
{
    public class CaseSensitiveEnumParsingAnalyzerFixture
    {
        [TestCase("Parse", "type, str")]
        [TestCase("Parse<ConsoleColor>", "str")]
        [TestCase("TryParse", "type, str, out var result")]
        [TestCase("TryParse<ConsoleColor>", "str, out var result")]
        public async Task DetectsEnumParseThatDoesNotSpecifyTheCasing(string method, string arguments)
        {
            var source = GetSource($"Enum.{{|#0:{method}|}}({arguments});");

            var result = new DiagnosticResult(CaseSensitiveEnumParsingAnalyzer.Rule).WithLocation(0);

            await Verify.VerifyAnalyzerAsync(source, result);
        }

        [TestCase("Parse", "type, str, true")]
        [TestCase("Parse", "type, str, false")]
        [TestCase("Parse<ConsoleColor>", "str, true")]
        [TestCase("Parse<ConsoleColor>", "str, false")]
        [TestCase("TryParse", "type, str, true, out var result")]
        [TestCase("TryParse", "type, str, false, out var result")]
        [TestCase("TryParse<ConsoleColor>", "str, true, out var result")]
        [TestCase("TryParse<ConsoleColor>", "str, false, out var result")]
        public async Task IgnoresEnumParseThatDoesSpecifiesTheCasing(string method, string arguments)
        {
            var source = GetSource($"Enum.{{|#0:{method}|}}({arguments});");

            await Verify.VerifyAnalyzerAsync(source);
        }

        static string GetSource(string line)
        {
            string source = @"
using System;

namespace TheNamespace
{
    class TheType
    {
        public void TheMethod()
        {
            var type = typeof(ConsoleColor);
            var str = """";
            " + line + @"
        }
    }        
}
";
            return source;
        }
    }
}

