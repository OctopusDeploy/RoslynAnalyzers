using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Verify = Tests.CSharpVerifier<Octopus.RoslynAnalyzers.Testing.Integration.IntegrationTestContainerAnalyzer>;

namespace Tests.Testing.Integration
{
    public class IntegrationTestContainerAnalyzerFixture
    {
        [TestCase]
        public async Task IgnoresClassesFullOfThingsThatHaveNothingToDoWithThisAnalyser()
        {
            var container = @"
public class JustAClassSittingAroundDoingItsThing
{
    public string Property { get; set; }
    string field;

    public bool IsThisMethodSuperCool()
    {
        return false; // Unfortunately it is not
    }
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task IgnoresIntegrationTestClassesThatHaveAllSortsOfThingsInThem()
        {
            var container = @"
public class AnIntegrationTestClassLivingItsOwnLife : IntegrationTest
{
    public string Property { get; set; }
    string field;

    public void Test()
    {
    }
}
";
            // This analyser only cares about the integration test container classes, this
            // code is tip-top as far as this analyser is concerned
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task IgnoresIntegrationTestContainerClassesThatAreStaticAndOnlyContainTestClasses()
        {
            var container = @"
public static class TheContainerClass
{
    public class TestOne : IntegrationTest
    {
    }

    public class TestTwo : IntegrationTest
    {
    }

    public class TestThree : IntegrationTest
    {
    }
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task IgnoresNestedClassesThatDoNotContainAnyTests()
        {
            var container = @"
public class ContainerOne
{
    public class ContainerTwo
    {
        public class ContainerThree
        {
            public class ContainerFour
            {
            }
        }
    }
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task IgnoresNestedClassesEvenIfThereIsATestIsRightAtTheTop()
        {
            var container = @"
public static class ContainerOne
{
    public class TestClass : IntegrationTest
    {
    }

    public class ContainerTwo
    {
        public class ContainerThree
        {
            public class ContainerFour
            {
            }
        }
    }
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task IgnoresEverythingWhenTheIntegrationTestBaseClassDoesNotExistInTheContext()
        {
            var containerInStandaloneNamespace = @"
namespace Octopus.IntegrationTests.Something
{
    class TheContainerClass
    {
        public class TheIntegrationTestClass
        {
        }
    }
}
";
            await Verify.VerifyAnalyzerAsync(containerInStandaloneNamespace);
        }

        [TestCase]
        public async Task DoesNotCrashWhenTheClassBaseTypeListIsNull()
        {
            var container = @"
public abstract class VersionRuleTestBase
{
    public class VersionRuleTestResponse
    {
    }
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task DetectsIntegrationTestContainerClassesThatAreNotStatic()
        {
            var container = @"
public class {|#0:TheContainerClass|}
{
    public class TheIntegrationTestClass : IntegrationTest
    {
    }
}
";
            var result = new DiagnosticResult(Descriptors.Oct2004IntegrationTestContainerClassMustBeStatic).WithLocation(0);
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(), result);
        }

        [TestCase]
        public async Task DetectsWhenThereAreTwoLevelsOfNesting()
        {
            var container = @"
public static class ContainerOne
{
    public static class {|#0:ContainerTwo|}
    {
        public class TestClass : IntegrationTest
        {
        }
    }
}
";
            var result = new DiagnosticResult(Descriptors.Oct2005DoNotNestIntegrationTestContainerClasses).WithLocation(0);
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(), result);
        }

        [TestCase]
        public async Task DetectsWhenThereAreManyLevelsOfNesting()
        {
            var container = @"
public static class ContainerOne
{
    public static class ContainerTwo
    {
        public static class ContainerThree
        {
            public static class {|#0:ContainerFour|}
            {
                public class TestClass : IntegrationTest
                {
                }
            }
        }
    }
}
";
            var result = new DiagnosticResult(Descriptors.Oct2005DoNotNestIntegrationTestContainerClasses).WithLocation(0);
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(), result);
        }

        [TestCase]
        public async Task DetectsIntegrationTestContainerClassesWithMembersThatAreNotTypesOrPrivateMethods()
        {
            var container = @"
public static class Container
{
    public static string {|#0:Property|} { get; set; }
    static string {|#1:field|};

    public static void {|#2:PublicMethod|}()
    {
    }

    static void PrivateMethod()
    {
    }

    public class TestOne : IntegrationTest
    {
    }
}
";
            var propertyResult = new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethods).WithLocation(0);
            var fieldResult = new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethods).WithLocation(1);
            var methodResult = new DiagnosticResult(Descriptors.Oct2007IntegrationTestContainersMethodsMustBePrivate).WithLocation(2);
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(), propertyResult, fieldResult, methodResult);
        }
    }
}