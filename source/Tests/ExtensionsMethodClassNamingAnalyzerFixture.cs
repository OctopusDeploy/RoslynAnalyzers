using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<Octopus.RoslynAnalyzers.ExtensionsMethodClassNamingAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Tests
{
    public class ExtensionsMethodClassNamingAnalyzerFixture
    {
        [Test]
        public async Task DetectsIncorrectNaming()
        {
            string source = @"
namespace TheNamespace
{
    public static class {|#0:Strings|}
    {
        public static string DoNothing(this string str) => str;
    }
}
";

            var result = new DiagnosticResult(ExtensionsMethodClassNamingAnalyzer.Rule).WithLocation(0);

            await Verify.VerifyAnalyzerAsync(source, result);
        }

        [Test]
        public async Task IgnoresCorrectNaming()
        {
            string source = @"
namespace TheNamespace
{
    public static class StringExtensionMethods
    {
        public static string DoNothing(this string str) => str;
    }
}
";
            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task IgnoresNonExtensionsStaticClasses()
        {
            string source = @"
namespace TheNamespace
{
    public static class Strings
    {
        public static string DoNothing(string str) => str;
    }
}
";
            await Verify.VerifyAnalyzerAsync(source);
        }
    }
}

