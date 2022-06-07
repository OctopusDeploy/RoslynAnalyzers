using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Octopus.RoslynAnalyzers.Testing.Integration;
using Verify = Tests.CSharpVerifier<Octopus.RoslynAnalyzers.Testing.Integration.IntegrationTestClassAnalyzer>;

namespace Tests.Testing.Integration
{
    public class IntegrationTestClassAnalyzerFixture
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
        public async Task IgnoresClassesThatDirectlyInheritFromIntegrationTestAndAreEmpty()
        {
            var container = @"
public class TestClass : IntegrationTest
{
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task DetectsClassesThatInheritFromSomeOtherClassThatInheritsFromIntegrationTest()
        {
            var container = @"
public class BaseClass : IntegrationTest
{
}

public class {|#0:TestClass|} : BaseClass
{
}
";
            var result = new DiagnosticResult(Descriptors.Oct2001NoIntegrationTestBaseClasses).WithLocation(0);
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(), result);
        }

        [TestCase]
        public async Task IgnoresIntegrationTestClassesWithASingleFact()
        {
            var container = @"
public class TestClass : IntegrationTest
{
    [Xunit.Fact]
    public void Test() {}
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task IgnoresIntegrationTestClassesWithASingleTheory()
        {
            var container = @"
public class TestClass : IntegrationTest
{
    [Xunit.Theory]
    [Xunit.InlineData(""1"")]
    public void Test(string data) {}
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task IgnoresIntegrationTestClassesWithASingleTheoryWithMultipleLinesOfData()
        {
            var container = @"
public class TestClass : IntegrationTest
{
    [Xunit.Theory]
    [Xunit.InlineData(""1"")]
    [Xunit.InlineData(""2"")]
    [Xunit.InlineData(""3"")]
    public void Test(string data) {}
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task IgnoresClassesThatDoNotInheritFromIntegrationTest()
        {
            var container = @"
public class TestClass
{
    public string CanHaveThis { get; set; }
    string thisToo;

    public void SureWhyNotLetsHaveSomeMethods() { }
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }
    }
}