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
        public async Task IntegrationTestContainerClassMembers_ConstsAllowed()
        {
            var container = @"
public static class Container
{
    const string SomeConstantString = ""foo"";

    public class SomeIntegrationTest : IntegrationTest { } // always need this to trigger container-class logic
}";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }
        
        [TestCase]
        public async Task IntegrationTestContainerClassMembers_FieldsOkIfReadOnlyAndImmutable()
        {
            var container = @"
public static class Container
{
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

    public class SomeIntegrationTest : IntegrationTest { } // always need this to trigger container-class logic
}";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(0),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(1),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(2));
        }
        
        [TestCase]
        public async Task IntegrationTestContainerClassMembers_PropertiesOkIfReadOnlyAndImmutable()
        {
            var container = @"
public static class Container
{
    static string SomeReadOnlyProp { get; } = ""foo"";

    // readonly ValueTypes are all OK
    static int SomeReadOnlyIntProp => 1;
    static DateTime SomeReadOnlyDateTimeProp => DateTime.UtcNow;
    static DateTimeKind SomeReadOnlyDateTimeKindProp => DateTimeKind.Utc;

    // readonly reference types are not OK except for special cases like IEnumerable, IReadOnlyCollection; tested below
    static Random {|#0:SomeReadOnlyRandomProp|} { get; } = new(); // bad; System.Random is not known to be immutable, probably unsafe to share

    // not readonly = not ok
    static string {|#1:SomeMutableStringProp|} {get;set;} = ""foo""; // bad; not readonly

    // public = not ok
    public static string {|#2:SomePublicReadOnlyStringProp|} {get;} = ""foo"";

    public class SomeIntegrationTest : IntegrationTest { } // always need this to trigger container-class logic
}";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(0),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(1),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(2));
        }
        
        [TestCase]
        public async Task IntegrationTestContainerClassMembers_MethodsOkIfNonPublic()
        {
            var container = @"
public static class Container
{
    public static void {|#0:SomePublicMethod|}()  { }

    static void SomePrivateMethod() { }

    public class SomeIntegrationTest : IntegrationTest { } // always need this to trigger container-class logic
}";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(),
                new DiagnosticResult(Descriptors.Oct2007IntegrationTestContainersMethodsMustBePrivate).WithLocation(0));
        }
        
        [TestCase]
        public async Task IntegrationTestContainerClassMembers_NestedTypesOk()
        {
            var container = @"
public static class Container
{
    class SomeClass{ }

    struct SomeStruct{ }

    enum SomeEnum{ }

    public class SomeIntegrationTest : IntegrationTest { } // always need this to trigger container-class logic
}";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }
        
        [TestCase]
        public async Task IntegrationTestContainerClassMembers_FieldsHoldingCollectionsOkIfTypeIsImmutable()
        {
            var container = @"
using System.Collections.Generic;
public static class Container
{
    static readonly IEnumerable<string> SomeReadonlyEnumerableOfString = new[]{ ""foo"", ""bar"" };  // good, property is immutable and nonpublic
    static readonly IReadOnlyList<string> SomeReadOnlyListOfString = new[]{ ""foo"", ""bar"" };  // good, property is immutable and nonpublic
    static readonly IReadOnlyDictionary<int, string> SomeReadOnlyDict = new Dictionary<int, string>(){ [0] = ""foo"", [1] = ""bar"" };  // good, property is immutable and nonpublic
    // static readonly IReadOnlySet<string> SomeReadOnlySet = new HashSet<string>(){ ""foo"", ""bar"" };  // good, property is immutable and nonpublic. COMMENTED: SEE NOTE
    
    static IEnumerable<string> {|#0:SomeMutableEnumerableOfString|} = new[]{ ""foo"", ""bar"" };  // bad, property is mutable

    static readonly string[] {|#1:SomeArrayOfString|} = new[]{ ""foo"", ""bar"" };  // bad, property is readonly but array itself is mutable
    static readonly List<string> {|#2:SomeListOfString|} = new(){ ""foo"", ""bar"" };  // bad, property is readonly but list itself is mutable
    static readonly Dictionary<int, string> {|#3:SomeMutableDictionary|} = new Dictionary<int, string>(){ [0] = ""foo"", [1] = ""bar"" }; // bad, property is readonly but list itself is mutable
    static readonly HashSet<string> {|#4:SomeMutableSet|} = new HashSet<string>(){ ""foo"", ""bar"" }; // bad, property is readonly but list itself is mutable

    public class SomeIntegrationTest : IntegrationTest { } // always need this to trigger container-class logic
}";
            // IReadOnlySet Note: Our DLL targets netstandard2.0, with the comment "As at Feb 2021 anything later than netstandard 2.0 doesn't work in Visual Studio".
            // IReadOnlySet was added in .NET 5 so isn't in netstandard2.0, so, while it works in the real world, we can't hit it with unit tests
            
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(0),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(1),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(2),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(3),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(4));
        }
        
        [TestCase]
        public async Task IntegrationTestContainerClassMembers_PropertiesHoldingCollectionsOkIfTypeIsImmutable()
        {
            var container = @"
using System.Collections.Generic;
public static class Container
{
    static IEnumerable<string> SomeReadonlyEnumerableOfString {get;} = new[]{ ""foo"", ""bar"" };  // good, property is immutable and nonpublic
    static IReadOnlyList<string> SomeReadOnlyListOfString {get;} = new[]{ ""foo"", ""bar"" };  // good, property is immutable and nonpublic
    static IReadOnlyDictionary<int, string> SomeReadOnlyDict {get;} = new Dictionary<int, string>(){ [0] = ""foo"", [1] = ""bar"" };  // good, property is immutable and nonpublic
    // static IReadOnlySet<string> SomeReadOnlySet {get;} = new HashSet<string>(){ ""foo"", ""bar"" };  // good, property is immutable and nonpublic. COMMENTED: SEE NOTE
   
    static string[] {|#0:SomeArrayOfString|} {get;} = new[]{ ""foo"", ""bar"" };  // bad, property is readonly but array itself is mutable
    static List<string> {|#1:SomeListOfString|} {get;} = new(){ ""foo"", ""bar"" };  // bad, property is readonly but list itself is mutable
    static Dictionary<int, string> {|#2:SomeMutableDictionary|} {get;} = new Dictionary<int, string>(){ [0] = ""foo"", [1] = ""bar"" }; // bad, property is readonly but list itself is mutable
    static HashSet<string> {|#3:SomeMutableSet|} {get;} = new HashSet<string>(){ ""foo"", ""bar"" }; // bad, property is readonly but list itself is mutable

    public class SomeIntegrationTest : IntegrationTest { } // always need this to trigger container-class logic
}";
            // IReadOnlySet Note: Our DLL targets netstandard2.0, with the comment "As at Feb 2021 anything later than netstandard 2.0 doesn't work in Visual Studio".
            // IReadOnlySet was added in .NET 5 so isn't in netstandard2.0, so, while it works in the real world, we can't hit it with unit tests
            
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(0),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(1),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(2),
                new DiagnosticResult(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData).WithLocation(3));
        }
    }
}