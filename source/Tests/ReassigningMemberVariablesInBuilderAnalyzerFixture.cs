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
        [Test]
        public async Task ShouldNotAllowAssignmentBackToMemberVariable()
        {
            const string source = @"
namespace TheNamespace
{
    public class SomethingSomethingBuilder
    {
        public int? Age {get; private set;}

        public SomethingSomethingBuilder WithAge(int age)
        {
            Age = age;
            return this;
        }

        public int? Build()
        {
            {|#0:Age ??= 10|};
            return Age;
        }
    }
}
";

            var result = new DiagnosticResult(BuildersReassigningToMemberVariablesAnalyzer.Rule).WithLocation(0); 
            await Verify.VerifyAnalyzerAsync(source, result);
        }
        
        [Test]
        public async Task ShouldAllowReassignmentOfLocalVariables()
        {
            const string source = @"
namespace TheNamespace
{
    public class SomethingSomethingBuilder
    {
        class SomeClass
        {   
            public int X = 10;
        }

        public int? Age {get; private set;}

        public SomethingSomethingBuilder WithAge(int age)
        {
            Age = age;
            return this;
        }

        public int? Build()
        {
            var someOtherLocalVariable = 10;
            someOtherLocalVariable = 5;
            var someVector = new SomeClass();
            someVector.X = 1;
            return Age;
        }
    }
}
";
            await Verify.VerifyAnalyzerAsync(source);
        }
        
        
    }
}