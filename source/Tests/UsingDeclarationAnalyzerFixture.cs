using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.RoslynAnalyzers.UsingDeclarationAnalyzer>;

namespace Tests
{
    public class UsingDeclarationAnalyzerFixture
    {
        static string usingBlock = @"
using System;

namespace TheNamespace
{
    public class FooBar
    {
        public void Baz()
        {
            using (var boo = new BooDis())
            {
                int foo = 5;
            }

            var bar = 7;
        }


        class BooDis : IDisposable
        {
            public void Dispose()
            {

            }
        }
    }       
}
";

        static string usingDeclaration = @"
using System;

namespace TheNamespace
{
    public class FooBar
    {
        public void Baz()
        {
            using var yah = new BooDis();
            {
                int foo = 5;
            }

            var bar = 7;
        }


        class BooDis : IDisposable
        {
            public void Dispose()
            {

            }
        }
    }       
}
";
        [Test]
        public async Task DetectsEnumParseThatDoesNotSpecifyTheCasing()
        {
            var source = usingDeclaration;

            var result = new DiagnosticResult(UsingDeclarationAnalyzer.Rule)
                .WithSpan("", 10, 13, 10 ,42);

            await Verify.VerifyAnalyzerAsync(source, result);
        }

        [Test]
        public async Task IgnoresEnumParseThatDoesSpecifiesTheCasing()
        {
            var source = usingBlock;

            await Verify.VerifyAnalyzerAsync(source);
        }
    }
}

