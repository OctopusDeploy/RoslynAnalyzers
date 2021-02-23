using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.RoslynAnalyzers.NuGetPackagingAddRangeAnalyzer>;

namespace Tests
{
    public class NuGetPackagingAddRangeAnalyzerFixture
    {
        [Test]
        public async Task DetectsCallToNuGetPackagingAddRangeExtension()
        {
            string source = @"
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NuGet.Packaging;

namespace TheNamespace
{
    class TheType
    {
        public void TheMethod()
        {
            new Collection<string>().{|#0:AddRange|}(new string[0]);
            new List<string>().AddRange(new string[0]); // List has an AddRange method itself
        }
    }        
}

namespace NuGet.Packaging 
{
    public static class CollectionExtensions
    {
        public static void AddRange<T>(this IList<T> list, string[] items)
        {
        }
    }
}
";

            var result = new DiagnosticResult(NuGetPackagingAddRangeAnalyzer.Rule).WithLocation(0);

            await Verify.VerifyAnalyzerAsync(source, result);
        }
    }
}

