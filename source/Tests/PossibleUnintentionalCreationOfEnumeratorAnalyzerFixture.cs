using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalysers;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.RoslynAnalysers.PossibleUnintentionalCreationOfEnumeratorAnalyzer>;

namespace Tests
{
    public class PossibleUnintentionalCreationOfEnumeratorAnalyzerFixture
    {
        const string CustomCollectionAndNoneExtensionSource = @"

";

        [Test]
        public async Task DetectsNonExtensionInvocation()
        {
            var source = GetSource("var n = System.Linq.Enumerable.{|#0:Any|}(new Octopus.Test.CustomCollection());");
            source += CustomCollectionAndNoneExtensionSource;
            var result = new DiagnosticResult(PossibleUnintentionalCreationOfEnumeratorAnalyzer.Rule).WithLocation(0);

            await Verify.VerifyAnalyzerAsync(source, result);
        }

        [TestCase("Any")]
        [TestCase("None")]
        [TestCase("Count")]
        public async Task DetectsCallThatCreateAnEnumerator(string call)
        {
            var source = GetSource($"var n = new Octopus.Test.CustomCollection().{{|#0:{call}|}}();");
            source += CustomCollectionAndNoneExtensionSource;
            var result = new DiagnosticResult(PossibleUnintentionalCreationOfEnumeratorAnalyzer.Rule).WithLocation(0);

            await Verify.VerifyAnalyzerAsync(source, result);
        }

        [TestCase("new List<string>().Any()")]
        [TestCase("((ICollection<string>) new List<string>()).Any()")]
        [TestCase("new Dictionary<string, string>().Any()")]
        [TestCase("new HashSet<string>().Any()")]
        [TestCase("new Queue<string>().Any()")] // Only implements ICollection
        [TestCase("new string[0].Any()")]
        public async Task IgnoresTypesWhereExtensionImplementationUsedLengthOrCountDirectly(string call)
        {
            var source = GetSource($"var n = {call};");
            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task IgnoresIEnumerable()
        {
            var source = GetSource(@"IEnumerable<string> collection = new CustomCollection(); 
                                              var n = collection.Any();");
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
		public static bool None(this IEnumerable<string> collection)
			=> false;
	}
}

";
            return source;
        }
    }
}