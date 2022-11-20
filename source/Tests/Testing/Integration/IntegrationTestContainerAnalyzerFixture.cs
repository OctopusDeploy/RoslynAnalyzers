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
using System.Collections.Generic;

public static class Container
{
    // ----- Constants are OK -----

    const string SomeConstantString = ""foo"";

    // ----- Fields are only OK if they are readonly and hold a type which is known to be immutable -----

    static readonly string SomeReadOnlyString = ""foo"";

    // readonly ValueTypes are all OK
    static readonly int SomeReadOnlyInt = 1;
    static readonly DateTime SomeReadOnlyDateTime = DateTime.UtcNow;
    static readonly DateTimeKind SomeReadOnlyDateTimeKind = DateTimeKind.Utc;

    // readonly reference types are not OK except for special cases like IEnumerable, IReadOnlyCollection; tested below
    static readonly Random {|#0:SomeReadOnlyRandom|} = new(); // bad; System.Random is not known to be immutable, probably unsafe to share

    // not readonly = not ok
    static string {|#1:someMutableString|} = ""foo""; // bad; not readonly

    // public = not ok
    public static readonly string {|#2:SomePublicReadOnlyString|} = ""foo"";

    // ----- Properties are only OK if they are readonly and hold a type which is known to be immutable -----

    static string SomeReadOnlyProp { get; } = ""foo"";

    // readonly ValueTypes are all OK
    static int SomeReadOnlyIntProp => 1;
    static DateTime SomeReadOnlyDateTimeProp => DateTime.UtcNow;
    static DateTimeKind SomeReadOnlyDateTimeKindProp => DateTimeKind.Utc;

    // readonly reference types are not OK except for special cases like IEnumerable, IReadOnlyCollection; tested below
    static Random {|#3:SomeReadOnlyRandomProp|} { get; } = new(); // bad; System.Random is not known to be immutable, probably unsafe to share

    // not readonly = not ok
    static string {|#4:SomeMutableStringProp|} {get;set;} = ""foo""; // bad; not readonly

    // public = not ok
    public static string {|#5:SomePublicReadOnlyStringProp|} {get;} = ""foo"";

    // ----- Methods are only OK if they are nonpublic -----

    public static void {|#99:SomePublicMethod|}()  { }

    static void SomePrivateMethod() { }

    // ----- Nested types are all OK -----

    public class SomeIntegrationTest : IntegrationTest  // good, class is an integration test
    { }

    class SomeClass{ }

    struct SomeStruct{ }

    enum SomeEnum{ }

    // ----- fields holding collections -----

    static readonly IEnumerable<string> SomeReadonlyEnumerableOfString = new[]{ ""foo"", ""bar"" };  // good, property is immutable and nonpublic
    static readonly IReadOnlyList<string> SomeReadOnlyListOfString = new[]{ ""foo"", ""bar"" };  // good, property is immutable and nonpublic
    static readonly IReadOnlyDictionary<int, string> SomeReadOnlyDict = new Dictionary<int, string>(){ [0] = ""foo"", [1] = ""bar"" };  // good, property is immutable and nonpublic
    // static readonly IReadOnlySet<string> SomeReadOnlySet = new HashSet<string>(){ ""foo"", ""bar"" };  // good, property is immutable and nonpublic. COMMENTED: SEE NOTE
    
    static IEnumerable<string> {|#30:SomeMutableEnumerableOfString|} = new[]{ ""foo"", ""bar"" };  // bad, property is mutable

    static readonly string[] {|#31:SomeArrayOfString|} = new[]{ ""foo"", ""bar"" };  // bad, property is readonly but array itself is mutable
    static readonly List<string> {|#32:SomeListOfString|} = new(){ ""foo"", ""bar"" };  // bad, property is readonly but list itself is mutable
    static readonly Dictionary<int, string> {|#33:SomeMutableDictionary|} = new Dictionary<int, string>(){ [0] = ""foo"", [1] = ""bar"" }; // bad, property is readonly but list itself is mutable
    static readonly HashSet<string> {|#34:SomeMutableSet|} = new HashSet<string>(){ ""foo"", ""bar"" }; // bad, property is readonly but list itself is mutable

    // properties holding collections not explicitly tested because it's the same logic as fields
}
";
            // IReadOnlySet Note: Our DLL targets netstandard2.0, with the comment "As at Feb 2021 anything later than netstandard 2.0 doesn't work in Visual Studio".
            // IReadOnlySet was added in .NET 5 so isn't in netstandard2.0, so, while it works in the real world, we can't hit it with unit tests
            var oct2006 = Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData;
            var oct2007 = Descriptors.Oct2007IntegrationTestContainersMethodsMustBePrivate;

            var source = container.WithTestingTypes();
            Console.WriteLine(source); // to help with debugging
            
            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(oct2006).WithLocation(0),
                new DiagnosticResult(oct2006).WithLocation(1),
                new DiagnosticResult(oct2006).WithLocation(2),
                new DiagnosticResult(oct2006).WithLocation(3),
                new DiagnosticResult(oct2006).WithLocation(4),
                new DiagnosticResult(oct2006).WithLocation(5),
                
                new DiagnosticResult(oct2006).WithLocation(30),
                new DiagnosticResult(oct2006).WithLocation(31),
                new DiagnosticResult(oct2006).WithLocation(32),
                new DiagnosticResult(oct2006).WithLocation(33),
                new DiagnosticResult(oct2006).WithLocation(34),
                
                new DiagnosticResult(oct2007).WithLocation(99));
        }
    }
}