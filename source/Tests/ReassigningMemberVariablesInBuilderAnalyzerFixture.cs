using System.Numerics;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.RoslynAnalyzers.BuildersReassigningToMemberVariablesAnalyzer>;

namespace Tests
{
    public class ReassigningMemberVariablesInBuilderAnalyzerFixture
    {
        [TestCase("{|#0:Age ??= new AgeClass()|};")]
        [TestCase("{|#0:Age.age = 1|};")]
        public async Task ShouldNotAllowAssignmentBackToMemberVariable(string line)
        {
            var source = GetSource(line);
            var result = new DiagnosticResult(BuildersReassigningToMemberVariablesAnalyzer.Rule).WithLocation(0);
            await Verify.VerifyAnalyzerAsync(source, result);
        }

        [TestCase("var someOtherLocalVariable = 10;\n" + "someOtherLocalVariable = 5;")]
        [TestCase("var someAge = new AgeClass();\nsomeAge.age = 1;")]
        public async Task ShouldAllowReassignmentOfLocalVariables(string line)
        {
            var source = GetSource(line);
            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task ShouldOnlyApplyRulesToClassesThatEndInBuilder()
        {
            var source = GetSource("Age ??= new AgeClass();", "NonBuilderClass");
            await Verify.VerifyAnalyzerAsync(source);
        }

        static string GetSource(string line, string className = "SomethingSomethingBuilder")
        {
            string source = @"
namespace TheNamespace
{
    public class " + className + @"
    {
        public class AgeClass
        {   
            public int age = 10;
        }

        public AgeClass? Age {get; private set;}

        public " + className + @" WithAge(AgeClass age)
        {
            Age = age;
            return this;
        }

        public AgeClass? Build()
        {
            " + line + @"
            return Age;
        }
    }
}
";
            return source;
        }
    }
}