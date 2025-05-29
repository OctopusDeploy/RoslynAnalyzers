using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<Octopus.RoslynAnalyzers.PossibleUnintentionalCreationOfEnumeratorAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Tests
{
    public class PossibleUnintentionalCreationOfEnumeratorAnalyzerFixture
    {

        [Test]
        public async Task DetectsNonExtensionInvocation()
        {
            var source = GetSource("var n = System.Linq.Enumerable.{|#0:Any|}(new Octopus.Test.CustomCollection());");
            var result = new DiagnosticResult(PossibleUnintentionalCreationOfEnumeratorAnalyzer.Rule).WithLocation(0);

            await Verify.VerifyAnalyzerAsync(source, result);
        }

        [TestCase("Any")]
        [TestCase("None")]
        [TestCase("Count")]
        public async Task DetectsCallThatCreateAnEnumerator(string call)
        {
            var source = GetSource($"var n = new Octopus.Test.CustomCollection().{{|#0:{call}|}}();");
            var result = new DiagnosticResult(PossibleUnintentionalCreationOfEnumeratorAnalyzer.Rule).WithLocation(0);

            await Verify.VerifyAnalyzerAsync(source, result);
        }

        public static IEnumerable<string> KnownTypesThatThisCheckShouldIgnore()
        {
            yield return "new List<string>()";
            yield return "((ICollection<string>) new List<string>())";
            yield return "new Dictionary<string, string>()";
            yield return "new HashSet<string>()";
            yield return "new Queue<string>()"; // Only implements ICollection
            yield return "new string[0]";
            yield return "((IReadOnlyList<string>) new string[0])";
            yield return "((IReadOnlyCollection<string>) new string[0])";
        }

        [TestCaseSource(nameof(KnownTypesThatThisCheckShouldIgnore))]
        public async Task AnyCallIsIgnoredOnKnownTypesForNet50(string type)
        {
            var source = GetSource($"var n = {type}.Any();");
            var test = new CSharpAnalyzerTest<PossibleUnintentionalCreationOfEnumeratorAnalyzer, DefaultVerifier>
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.Net.Net50
            };

            await test.RunAsync();
        }

        [TestCaseSource(nameof(KnownTypesThatThisCheckShouldIgnore))]
        public async Task AnyCallIsDetectedOnKnownTypesForPreNet50(string type)
        {
            var source = GetSource($"var n = {type}.{{|#0:Any|}}();");
            var test = new CSharpAnalyzerTest<PossibleUnintentionalCreationOfEnumeratorAnalyzer, DefaultVerifier>
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies.NetCore.NetCoreApp31
            };
            var result = new DiagnosticResult(PossibleUnintentionalCreationOfEnumeratorAnalyzer.Rule).WithLocation(0);
            test.ExpectedDiagnostics.Add(result);

            await test.RunAsync();
        }

        [TestCaseSource(nameof(KnownTypesThatThisCheckShouldIgnore))]
        public async Task CountCallIsIgnoredOnKnownTypes(string type)
        {
            var source = GetSource($"var n = {type}.Count();");
            await Verify.VerifyAnalyzerAsync(source);
        }

        [TestCaseSource(nameof(KnownTypesThatThisCheckShouldIgnore))]
        public async Task NoneCallIsIgnoredOnKnownTypes(string type)
        {
            var source = GetSource($"var n = {type}.None();");
            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task IgnoresIEnumerable()
        {
            var source = GetSource(@"var n = ((IEnumerable<string>) null).Any();");
            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task IgnoresIGrouping()
        {
            var source = GetSource(@"var n = ((IGrouping<string, string>) null).Count();");
            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task IgnoresIfTheParameterTypeIsNotIEnumerable()
        {
            var source = GetSource(@"var n = ""test"".None();");
            await Verify.VerifyAnalyzerAsync(source);
        }

        static string GetSource(string line)
        {
            string source = @"
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Octopus.Test;

namespace TheNamespace
{
    class TheType
    {
        public void TheMethod()
        {
             " + line + @"
        }
    }        
}

namespace Octopus.Test {
	public class CustomCollection : IEnumerable<string>
	{
		public IEnumerator<string> GetEnumerator() => null;
		IEnumerator IEnumerable.GetEnumerator() => null;
	}
	
	public static class Extensions {
		public static bool None<T>(this IEnumerable<T> collection)
			=> false;

        public static bool None(this string str)
			=> false;
	}
}

";
            return source;
        }
    }
}