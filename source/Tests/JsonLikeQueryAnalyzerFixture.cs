using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.RoslynAnalyzers.JsonLikeQueryAnalyzer>;

namespace Tests
{
    public class JsonLikeQueryAnalyzerFixture
    {
        [TestCase("\"[JSON] like\"")]
        [TestCase("\"jSoN lIkE\"")]
        [TestCase("@\"jSoN \r\n lIkE\"")]
        public async Task DetectsCallToNuGetPackagingAddRangeExtension(string str)
        {
            string source = @"
namespace TheNamespace
{
    class TheType
    {
        public void TheMethod()
        {
            var s = {|#0:" + str + @"|};
        }
    }        
}
";

            var result = new DiagnosticResult(JsonLikeQueryAnalyzer.Rule).WithLocation(0);

            await Verify.VerifyAnalyzerAsync(source, result);
        }
    }
}