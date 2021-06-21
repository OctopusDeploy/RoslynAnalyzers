using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Verify = Tests.CSharpVerifier<Octopus.RoslynAnalyzers.Testing.Unit.UnitTestClassAnalyzer>;

namespace Tests.Testing.Unit
{
    public class UnitTestClassAnalyzerFixture
    {
        [TestCase]
        public async Task IgnoresClassesThatHaveNothingToDoWithThisAnalyser()
        {
            var container = @"
public class JustAClassSittingAroundDoingItsThing
{
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task IgnoresClassesThatDirectlyInheritFromUnitTest()
        {
            var container = @"
public class TestClass : UnitTest
{
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task DetectsClassesThatInheritFromSomeOtherClassThatInheritsFromUnitTest()
        {
            var container = @"
public class BaseClass : UnitTest
{
}

public class {|#0:TestClass|} : BaseClass
{
}

";
            var result = new DiagnosticResult(Descriptors.Oct2002NoUnitTestBaseClasses).WithLocation(0);
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(), result);
        }
    }
}