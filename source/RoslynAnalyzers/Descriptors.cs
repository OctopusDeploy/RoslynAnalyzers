using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Octopus.RoslynAnalyzers
{
    public static class Descriptors
    {
        static DiagnosticDescriptor GetTestAnalyzerDescriptor(string id, string title, string? description = null)
        {
            return new DiagnosticDescriptor(id,
                title,
                title,
                "Octopus.Testing",
                DiagnosticSeverity.Warning,
                false, // Testing analysers are disabled by default, these will only be enabled for test projects
                description);
        }

        static readonly string noBaseClassesDescription = "Creating base classes for tests is a really good way to make tests neat, and 1 or 2 levels " +
            "of inheritance is generally pretty easy to reason with. From past experience, these hierarchies quickly " +
            "get out of control and it gets really hard to find where different things set up in the chain." +
            Environment.NewLine +
            "If you need to share logic and resources (that are expensive to set up) between multiple tests, xUnits " +
            "IClassFixture and IAssemblyFixture might be your answer: https://xunit.net/docs/shared-context" +
            Environment.NewLine +
            "If you just need to share some logic, setup, or helpers between these tests please make use of builders " +
            "or feel free to write a custom helper or context class." +
            Environment.NewLine +
            "And finally, if you want to run a bunch of the same tests with slightly different configuration, make use of " +
            "xUnit's Theories with InlineData/ClassData/MemberData. These won't run in parallel though, so please use sparingly " +
            "in integration test land." +
            Environment.NewLine +
            "Reach out to @team-core-blue if you need any help with strategies to test efficiently without base classes.";

        public static DiagnosticDescriptor Oct2001NoIntegrationTestBaseClasses => GetTestAnalyzerDescriptor(
            "OCT2001",
            "Integration test classes should inherit directly from IntegrationTest",
            noBaseClassesDescription
        );

        public static DiagnosticDescriptor Oct2002NoUnitTestBaseClasses => GetTestAnalyzerDescriptor(
            "OCT2002",
            "Unit test classes should inherit directly from UnitTest",
            noBaseClassesDescription
        );

        public static DiagnosticDescriptor Oct2004IntegrationTestContainerClassMustBeStatic => GetTestAnalyzerDescriptor(
            "OCT2004",
            "Integration test container class should be static",
            "Integration test container classes should be static. They're containers for organisation only " +
            "and should never be instantiated (also Rider/ReSharper gets upset if they're not static)."
        );

        public static DiagnosticDescriptor Oct2005DoNotNestIntegrationTestContainerClasses => GetTestAnalyzerDescriptor(
            "OCT2005",
            "Do not nest integration test container classes",
            "A single level of nesting should be enough for organising tests in a file. Creating separate files is a" +
            "good way to break things up. This convention is in place to stop files getting to big, and to stop tests being " +
            "added at multiple levels of the class hierarchy in a given file."
        );

        public static DiagnosticDescriptor Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData => GetTestAnalyzerDescriptor(
            "OCT2006",
            "Integration test container classes should only contain nested types, methods and immutable data",
            @"Integration test container classes should only contain integration test classes and methods or immutable data.
Any other complex logic or state should be in builders, class/assembly fixtures, or some other generic helper."
        );

        public static DiagnosticDescriptor Oct2007IntegrationTestContainersMethodsMustBePrivate => GetTestAnalyzerDescriptor(
            "OCT2007",
            "Methods in integration test container classes should be private",
            "Integration test container classes should only be used to organise tests (because we must have 1 test per class for maximum parallel goodness,"
            + " any logic that you want to share across multiple tests should be in builders, class/assembly fixtures, or some other generic helper."
        );

        public static DiagnosticDescriptor Oct2008IntegrationTestForwardCancellationTokenToInvocations()
        {
            return new DiagnosticDescriptor("OCT2008",
                "Integration test container classes should forward the 'CancellationToken' to methods that take one",
                "Cancellation Token is defined in IntegrationTest base class. Container classes should forward the 'CancellationToken' to methods that take one",
                "Octopus.Testing",
                DiagnosticSeverity.Info,
                false, // Testing analysers are disabled by default, these will only be enabled for test projects
                "Forward the CancellationToken property to compatible methods to ensure cancellation notifications get properly propagated,"
                + "or pass in 'CancellationToken.None' explicitly to indicate intentionally not propagating the token.");
        }
        
        // ----- Message Contract Analyzers -----
        
        const string Category = "Octopus";

        public static readonly DiagnosticDescriptor EventTypesMustBeNamedCorrectly = new(
            "OCT3001",
            "Event types must either end with Event or EventV[versionNumber]",
            "Event types must either end with Event or EventV[versionNumber]",
            Category,
            DiagnosticSeverity.Error,
            true);
        
        public static readonly DiagnosticDescriptor CommandTypesMustBeNamedCorrectly = new(
            "OCT3002",
            "Command types must either end with Command or CommandV[versionNumber]",
            "Command types must either end with Command or CommandV[versionNumber]",
            Category,
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor RequestTypesMustBeNamedCorrectly = new(
            "OCT3003",
            "Request types must either end with Request or RequestV[versionNumber]",
            "Request types must either end with Request or RequestV[versionNumber]",
            Category,
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor CommandTypesMustHaveCorrectlyNamedResponseTypes = new(
            "OCT3004",
            "Types that implement ICommand must have responses with matching names",
            "Response type should be \"{0}\" instead of \"{1}\" (Types that implement ICommand must have responses with matching names)",
            Category,
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor RequestTypesMustHaveCorrectlyNamedResponseTypes = new(
            "OCT3005",
            "Types that implement IRequest must have responses with matching names",
            "Response type should be \"{0}\" instead of \"{1}\" (Types that implement IRequest must have responses with matching names)",
            Category,
            DiagnosticSeverity.Error,
            true);

        public static readonly DiagnosticDescriptor PropertiesOnMessageTypesMustBeMutable = new(
            "OCT3006",
            "Properties on MessageTypes must be Mutable.",
            "Property \"{0}\" should have a setter (Properties on MessageTypes must be Mutable)",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"There is a tension between our DataAnnotation declarations, C# contract types, and our Swagger document generation
If we have [Optional] properties, they should not appear in the parameterized constructor - that is reserved for [Required] properties, and represents the minimum viable payload.
If these properties are immutable, even if our serializer could gazump the type system and set their values, our Swagger document would mark these as ReadOnly = true
To keep things simple, we will make all properties on our message contracts mutable, for contracts used in API endpoints covered by Swagger
(i.e. excluding backend for frontend (bff) controllers, and events.)
The tradeoff here is that we sacrifice some encapsulation and correctness for simplifying the relationship between our attributions, types and API documentation.
But what if someone messes with a payload after it comes off the wire? Please don't do this - you know better :) ");

        public static readonly DiagnosticDescriptor RequiredPropertiesOnMessageTypesMustNotBeNullable = new(
            "OCT3007",
            "Required Properties on MessageTypes must not be nullable",
            "Property \"{0}\" should be of type {1} (Required Properties on MessageTypes must not be nullable)",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"Properties marked as [Required] are just that - they MUST be supplied in the on-the-wire payload.
This convention enforces that all optional properties must be not-nullable, so that consumers of the type know they can safely dereference the information in these properties.");

        public static readonly DiagnosticDescriptor OptionalPropertiesOnMessageTypesMustBeNullable = new(
            "OCT3008",
            "Optional Properties on MessageTypes must be nullable",
            "Property \"{0}\" should be of type {1}? (Optional Properties on MessageTypes must be nullable)",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"Properties marked as [Optional] are just that - they do not need to be supplied in the on-the-wire payload.
We would expect [Optional] properties to be null if they have not been provided in the payload.
This convention enforces that all optional properties must be nullable, so that consumers of the type are aware that they need to handle it appropriately.");

        public static readonly DiagnosticDescriptor MessageTypesMustInstantiateCollections = new(
            "OCT3009",
            "MessageTypes must instantiate non-nullable collections",
            "MessageTypes must instantiate non-nullable collections.",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"With all [Required] properties set by the public parameterized constructor, we also want to make sure any collection types are initialized by default
so that they are safe to consume as soon as contracts come off the wire. This protects us when an [Optional] property is a collection type and is not
initialized by the constructor.");

        public static readonly DiagnosticDescriptor PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute = new(
            "OCT3010",
            "Properties on Message Types must be either [Optional] or [Required]",
            "Properties on Message Types must be either [Optional] or [Required]",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"Principle: if you give me a thing, that thing is valid.
By requiring validation attributes on all of our message contracts, we can be confident that we haven't forgotten to validate something.
If a parameter is genuinely optional, use the [Optional] attribute.");

        public static readonly DiagnosticDescriptor SpaceIdPropertiesOnMessageTypesMustBeOfTypeSpaceId = new(
            "OCT3011",
            "Properties on Message Types named SpaceId must be of type SpaceId",
            "Properties on Message Types named SpaceId must be of type SpaceId",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"All properties named SpaceId must be of type SpaceId so that the model binder can set them");

        public static readonly DiagnosticDescriptor IdPropertiesOnMessageTypesMustBeACaseInsensitiveStringTinyType = new(
            "OCT3012",
            "Id Properties on Message Types should be CaseInsensitiveStringTinyTypes",
            "Id Properties on Message Types should be CaseInsensitiveStringTinyTypes",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"All Id properties on message contracts should be CaseInsensitiveStringTinyTypes.
We want to avoid stringly typed Ids as they can be mixed up. This convention encourages their use.
If a particular TinyType does not yet exist, add it to Octopus.Core.Features.[Area/Document/EntityName].MessageContracts");
        
        public static readonly DiagnosticDescriptor MessageTypesMustHaveXmlDocComments = new(
            "OCT3013",
            "Message Types must have XMLDoc Comments",
            "Message Types must have XMLDoc Comments",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"We want to be able to auto-generate our swagger docs, but also make it nice and easy for both internal
 and external developers to code against the api.");
        
        public static readonly DiagnosticDescriptor ApiContractTypesMustLiveInTheAppropriateNamespace = new(
            "OCT3014",
            "Contracts must live in either the Octopus.Server.MessageContracts project or (temporarily) under some namespace containing MessageContracts.",
            "Contracts must live in either the Octopus.Server.MessageContracts project or (temporarily) under some namespace containing MessageContracts.",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"These exceptions are temporary, and only until the dependency consolidation work brings the Octopus.Server.MessageContracts
project back into this Git repository and C# solution.
- After that, all message contracts must live in the Octopus.Server.MessageContracts project.
- Until then, message contracts may _temporarily_ live in the Octopus.Core project under a MessageContracts namespace
  related to that feature to at least indicate that it's a temporary location,
  e.g. Octopus.Core.Features.FooFeature.MessageContracts..");
    }
}