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
        [TestCase("{|#0:Age = new AgeClass()|};")]
        [TestCase("{|#0:Age.age = 1|};")]
        [TestCase("{|#0:(Age, Age2) = (new AgeClass(), new AgeClass())|};")]
        public async Task ShouldNotAllowAssignmentBackToMemberVariable(string line)
        {
            var source = CreateSource(line);
            var result = new DiagnosticResult(BuildersReassigningToMemberVariablesAnalyzer.Rule).WithLocation(0);
            await Verify.VerifyAnalyzerAsync(source, result);
        }
        
        [TestCase("var someOtherLocalVariable = 10;\n" + "someOtherLocalVariable = 5;")]
        [TestCase("var someAge = new AgeClass();\nsomeAge.age = 1;")]
        [TestCase("var (a, b) = (1, 2);")]
        [TestCase("var (a, b, c, d) = (1, 2, 3, 4);")]
        [TestCase("var someAge = new AgeClass();someAge.foo.bar = 10;")]
        public async Task ShouldAllowReassignmentOfLocalVariables(string line)
        {
            var source = CreateSource(line);
            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task ShouldOnlyApplyRulesToClassesThatEndInBuilder()
        {
            var source = CreateSource("Age ??= new AgeClass();", "NonBuilderClass");
            await Verify.VerifyAnalyzerAsync(source);
        }

        static string CreateSource(string line, string className = "SomethingSomethingBuilder")
        {
            string source = @"
namespace TheNamespace
{
    public class " + className + @"
    {
        public class AgeClass
        {   
            public int age = 10;
            public Foo foo = new Foo();
        }

        public class Foo
        {
            public int bar = 1;
        }

        public AgeClass? Age {get; private set;}
        public AgeClass? Age2 {get; private set;} 

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