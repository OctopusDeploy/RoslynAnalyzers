using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.RoslynAnalyzers.UsingDeclarationAnalyzer>;

namespace Tests
{
    public class UsingDeclarationAnalyzerFixture
    {
        [Test]
        public async Task DetectsUsingDeclaration()
        {
            var result = new DiagnosticResult(UsingDeclarationAnalyzer.Rule)
                .WithSpan("", 10, 13, 10 ,42);

            await Verify.VerifyAnalyzerAsync(UsingDeclaration, result);
        }

        [Test]
        public async Task IgnoresUsingBlockWithBraces()
        {
            await Verify.VerifyAnalyzerAsync(UsingBlockWithBraces);
        }

        [Test]
        public async Task IgnoresUsingBlockWithoutBraces()
        {
            await Verify.VerifyAnalyzerAsync(UsingBlockWithoutBraces);
        }

        const string UsingBlockWithBraces = @"
using System;

namespace TheNamespace
{
    public class FooBar
    {
        public void Baz()
        {
            using (var boo = new BooDis())
            {
                Console.Write(3);
            }

            Console.Write(7);
        }

        class BooDis : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }       
}";

        const string UsingBlockWithoutBraces = @"
using System;

namespace TheNamespace
{
    public class FooBar
    {
        public void Baz()
        {
            using (var boo = new BooDis())
                Console.Write(3);

            Console.Write(7);
        }

        class BooDis : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }       
}";

        const string UsingDeclaration = @"
using System;

namespace TheNamespace
{
    public class FooBar
    {
        public void Baz()
        {
            using var yah = new BooDis();
            {
                Console.Write(3);
            }

            Console.Write(7);
        }

        class BooDis : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }       
}";
    }
}

