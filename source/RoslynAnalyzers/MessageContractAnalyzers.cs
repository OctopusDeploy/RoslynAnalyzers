using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text.RegularExpressions;
using System;

namespace Octopus.RoslynAnalyzers
{
    // Once we establish that a type is a "Message" (that is, it implements ICommand<>, IRequest<> or IResponse) then
    // there's a whole variety of things that we want to assert against. We put them all in one analyzer to avoid the extra work that would
    // be incurred if we had a dozen analyzers all going "is this a MessageType"?
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MessageContractAnalyzers : DiagnosticAnalyzer
    {
        // ReSharper disable UnusedMethodReturnValue.Local

        const string Category = "Octopus";

        internal static readonly DiagnosticDescriptor CommandTypesMustBeNamedCorrectly = new(
            "Octopus_CommandTypesMustBeNamedCorrectly",
            "Types that implement ICommand must be named correctly",
            "Types that implement ICommand must be called <thing>Command",
            Category,
            DiagnosticSeverity.Error,
            true);

        internal static readonly DiagnosticDescriptor RequestTypesMustBeNamedCorrectly = new(
            "Octopus_RequestTypesMustBeNamedCorrectly",
            "Types that implement IRequest must be named correctly",
            "Types that implement IRequest must be called <thing>Request",
            Category,
            DiagnosticSeverity.Error,
            true);

        internal static readonly DiagnosticDescriptor CommandTypesMustHaveCorrectlyNamedResponseTypes = new(
            "Octopus_CommandTypesMustHaveCorrectlyNamedResponseTypes",
            "Types that implement ICommand must have responses with matching names",
            "Types that implement ICommand have response types called <thing>Response",
            Category,
            DiagnosticSeverity.Error,
            true);

        internal static readonly DiagnosticDescriptor RequestTypesMustHaveCorrectlyNamedResponseTypes = new(
            "Octopus_RequestTypesMustHaveCorrectlyNamedResponseTypes",
            "Types that implement IRequest must have responses with matching names",
            "Types that implement IRequest have response types called <thing>Response",
            Category,
            DiagnosticSeverity.Error,
            true);

        internal static readonly DiagnosticDescriptor PropertiesOnMessageTypesMustBeMutable = new(
            "Octopus_PropertiesOnMessageTypesMustBeMutable",
            "Properties on MessageTypes must be Mutable",
            "Properties on MessageTypes must be Mutable.",
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

        internal static readonly DiagnosticDescriptor RequiredPropertiesOnMessageTypesMustNotBeNullable = new(
            "Octopus_RequiredPropertiesOnMessageTypesMustNotBeNullable",
            "Required Properties on MessageTypes must not be nullable",
            "Required Properties on MessageTypes must not be nullable.",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"Properties marked as [Required] are just that - they MUST be supplied in the on-the-wire payload.
This convention enforces that all optional properties must be not-nullable, so that consumers of the type know they can safely dereference the information in these properties.");

        internal static readonly DiagnosticDescriptor OptionalPropertiesOnMessageTypesMustBeNullable = new(
            "Octopus_OptionalPropertiesOnMessageTypesMustBeNullable",
            "Optional Properties on MessageTypes must be nullable",
            "Optional Properties on MessageTypes must be nullable.",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"Properties marked as [Optional] are just that - they do not need to be supplied in the on-the-wire payload.
We would expect [Optional] properties to be null if they have not been provided in the payload.
This convention enforces that all optional properties must be nullable, so that consumers of the type are aware that they need to handle it appropriately.");

        internal static readonly DiagnosticDescriptor MessageTypesMustInstantiateCollections = new(
            "Octopus_MessageTypesMustInstantiateCollections",
            "MessageTypes must instantiate non-nullable collections",
            "MessageTypes must instantiate non-nullable collections.",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"With all [Required] properties set by the public parameterized constructor, we also want to make sure any collection types are initialized by default
so that they are safe to consume as soon as contracts come off the wire. This protects us when an [Optional] property is a collection type and is not
initialized by the constructor.");

        internal static readonly DiagnosticDescriptor PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute = new(
            "Octopus_PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute",
            "Properties on Message Types must be either [Optional] or [Required]",
            "Properties on Message Types must be either [Optional] or [Required]",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"Principle: if you give me a thing, that thing is valid.
By requiring validation attributes on all of our message contracts, we can be confident that we haven't forgotten to validate something.
If a parameter is genuinely optional, use the [Optional] attribute.");

        internal static readonly DiagnosticDescriptor SpaceIdPropertiesOnMessageTypesMustBeOfTypeSpaceId = new(
            "Octopus_SpaceIdPropertiesOnMessageTypesMustBeOfTypeSpaceId",
            "Properties on Message Types named SpaceId must be of type SpaceId",
            "Properties on Message Types named SpaceId must be of type SpaceId",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"All properties named SpaceId must be of type SpaceId so that the model binder can set them");

        internal static readonly DiagnosticDescriptor IdPropertiesOnMessageTypesMustBeACaseInsensitiveStringTinyType = new(
            "Octopus_IdPropertiesOnMessageTypesMustBeACaseInsensitiveStringTinyType",
            "Id Properties on Message Types should be CaseInsensitiveStringTinyTypes",
            "Id Properties on Message Types should be CaseInsensitiveStringTinyTypes",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"All Id properties on message contracts should be CaseInsensitiveStringTinyTypes.
We want to avoid stringly typed Ids as they can be mixed up. This convention encourages their use.
If a particular TinyType does not yet exist, add it to Octopus.Core.Features.[Area/Document/EntityName].MessageContracts");
        
        internal static readonly DiagnosticDescriptor MessageTypesMustHaveXmlDocComments = new(
            "Octopus_MessageTypesMustHaveXmlDocComments",
            "Message Types must have XMLDoc Comments",
            "Message Types must have XMLDoc Comments",
            Category,
            DiagnosticSeverity.Error,
            true,
            @"We want to be able to auto-generate our swagger docs, but also make it nice and easy for both internal
 and external developers to code against the api.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            CommandTypesMustBeNamedCorrectly,
            RequestTypesMustBeNamedCorrectly,
            CommandTypesMustHaveCorrectlyNamedResponseTypes,
            RequestTypesMustHaveCorrectlyNamedResponseTypes,
            PropertiesOnMessageTypesMustBeMutable,
            RequiredPropertiesOnMessageTypesMustNotBeNullable,
            OptionalPropertiesOnMessageTypesMustBeNullable,
            MessageTypesMustInstantiateCollections,
            PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute,
            SpaceIdPropertiesOnMessageTypesMustBeOfTypeSpaceId,
            IdPropertiesOnMessageTypesMustBeACaseInsensitiveStringTinyType,
            MessageTypesMustHaveXmlDocComments);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(CacheCommonTypes);
        }

        // ReSharper disable once InconsistentNaming
        record struct SpecialTypeDeclarations(
            INamedTypeSymbol String,
            INamedTypeSymbol IEnumerable,
            INamedTypeSymbol? SpaceId,
            INamedTypeSymbol? CaseInsensitiveStringTinyType);

        static SpecialTypeDeclarations cachedTypes; // if you happened to use this before CacheCommonTypes it will blow up; beware

        void CacheCommonTypes(CompilationStartAnalysisContext context)
        {
            cachedTypes = new SpecialTypeDeclarations(
                String: context.Compilation.GetSpecialType(SpecialType.System_String),
                IEnumerable: context.Compilation.GetSpecialType(SpecialType.System_Collections_IEnumerable),
                SpaceId: context.Compilation.GetTypeByMetadataName("Octopus.Server.MessageContracts.Features.Spaces.SpaceId"),
                CaseInsensitiveStringTinyType: context.Compilation.GetTypeByMetadataName("Octopus.TinyTypes.CaseInsensitiveStringTinyType"));

            context.RegisterSyntaxNodeAction(CheckNode, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
        }

        static readonly Regex RequestNameRegex = new("(?<!V\\d+)Request(V\\d+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex CommandNameRegex = new("(?<!V\\d+)Command(V\\d+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        const string TypeNameIRequest = "IRequest";
        const string TypeNameICommand = "ICommand";
        const string TypeNameIResponse = "IResponse";

        static void CheckNode(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is TypeDeclarationSyntax typeDec)
            {
                // This will match anything which has a basetype called ICommand<> whether it's our octopus one or not.
                // In practice this should be fine, we don't have other ICommand<> or IRequest<> types running around our codebase and the namespace check is more work to do.

                GenericNameSyntax? requestOrCommandDec = null;
                IdentifierNameSyntax? responseDec = null;
                foreach (var baseTypeDec in typeDec.BaseList?.ChildNodes().OfType<SimpleBaseTypeSyntax>() ?? Enumerable.Empty<SimpleBaseTypeSyntax>())
                {
                    foreach (var c in baseTypeDec.ChildNodes())
                    {
                        switch (c)
                        {
                            case GenericNameSyntax { Identifier.Text: TypeNameIRequest or TypeNameICommand } g:
                                requestOrCommandDec = g;
                                break;
                            case IdentifierNameSyntax { Identifier.Text: TypeNameIResponse } i:
                                responseDec = i;
                                break;
                        }
                    }
                }

                if (requestOrCommandDec != null || responseDec != null)
                {
                    // this is a "MessageType"; either request, command, or response
                    CheckProperties(context, typeDec);
                    MessageTypes_MustHaveXmlDocComments(context, typeDec);
                }

                // request/command/response specific
                switch (requestOrCommandDec?.Identifier.Text)
                {
                    case TypeNameICommand:
                        if (CommandTypes_MustBeNamedCorrectly(context, typeDec))
                            CommandTypes_MustHaveCorrectlyNamedResponseTypes(context, typeDec, requestOrCommandDec); // the expected response name depends on the command name; so only run this check if the CommandName was good
                        break;

                    case TypeNameIRequest:
                        if (RequestTypes_MustBeNamedCorrectly(context, typeDec))
                            RequestTypes_MustHaveCorrectlyNamedResponseTypes(context, typeDec, requestOrCommandDec); // the expected response name depends on the request name; so only run this check if the RequestName was good
                        break;
                }
            }
        }

        // ----- specific conventions ---------------

        static bool CheckProperties(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec)
        {
            var result = true;
            foreach (var propDec in typeDec.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                // only validate public properties
                if (!propDec.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) continue;

                // we have a number of places where we treat collection types differently; front-load that info
                var isCollectionType = IsCollectionType(context, propDec);
                var required = GetRequiredState(propDec);

                // if anything returns false, propagate false outwards
                result &= PropertiesOnMessageTypes_MustBeMutable(context, propDec);
                result &= RequiredPropertiesOnMessageTypes_MustNotBeNullable(context, propDec, required);
                result &= OptionalPropertiesOnMessageTypes_ExceptForCollections_MustBeNullable(context, propDec, required, isCollectionType);
                result &= MessageTypes_MustInstantiateCollections(context, propDec, required, isCollectionType);
                result &= PropertiesOnMessageTypes_MustHaveAtLeastOneValidationAttribute(context, propDec, required);
                result &= SpaceIdPropertiesOnMessageTypes_MustBeOfTypeSpaceId(context, propDec);
                result &= IdPropertiesOnMessageTypes_MustBeACaseInsensitiveStringTinyType(context, propDec);
            }

            return result;
        }

        enum RequiredState
        {
            Unspecified,
            Optional,
            Required,
        }

        static RequiredState GetRequiredState(PropertyDeclarationSyntax propDec)
        {
            var attrNames = propDec.AttributeLists.SelectMany(al => al.Attributes.Select(a => (a.Name as IdentifierNameSyntax)?.Identifier.Text));

            foreach (var attrName in attrNames)
            {
                // it has an attribute called Required. Not necessarily the one from System.ComponentModel.DataAnnotations but good enough.
                // Note: we could resolve the full type which is more expensive, it's unclear as to whether that makes a difference or not
                if (attrName == "Required") return RequiredState.Required;
                // it has an attribute called Optional. Not necessarily the one from Octopus.MessageContracts.Attribute but good enough.
                if (attrName == "Optional") return RequiredState.Optional;
            }

            return RequiredState.Unspecified;
        }

        static bool IsCollectionType(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propDec)
        {
            var propType = propDec.Type is NullableTypeSyntax n ? n.ElementType : propDec.Type;

            var typeInfo = context.SemanticModel.GetTypeInfo(propType);
            if (typeInfo.Type == null) return false;

            return typeInfo.Type.IsAssignableTo(cachedTypes.IEnumerable) && !SymbolEqualityComparer.Default.Equals(typeInfo.Type, cachedTypes.String);
        }

        static bool PropertiesOnMessageTypes_MustBeMutable(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propDec)
        {
            var hasSetter = propDec.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)) ?? false;
            if (!hasSetter)
            {
                context.ReportDiagnostic(Diagnostic.Create(PropertiesOnMessageTypesMustBeMutable, propDec.Identifier.GetLocation()));
                return false;
            }

            return true;
        }

        static bool RequiredPropertiesOnMessageTypes_MustNotBeNullable(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propDec, RequiredState required)
        {
            if (required == RequiredState.Required && propDec.Type is NullableTypeSyntax)
            {
                context.ReportDiagnostic(Diagnostic.Create(RequiredPropertiesOnMessageTypesMustNotBeNullable, propDec.Identifier.GetLocation()));
                return false;
            }

            return true;
        }

        static bool OptionalPropertiesOnMessageTypes_ExceptForCollections_MustBeNullable(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propDec, RequiredState required, bool isCollectionType)
        {
            if (required == RequiredState.Optional && !isCollectionType && propDec.Type is not NullableTypeSyntax)
            {
                // non-nullable optional property that isn't a collection (strings are enumerable but not collections)
                context.ReportDiagnostic(Diagnostic.Create(OptionalPropertiesOnMessageTypesMustBeNullable, propDec.Identifier.GetLocation()));
                return false;
            }

            return true;
        }

        static bool MessageTypes_MustInstantiateCollections(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propDec, RequiredState required, bool isCollectionType)
        {
            if (!isCollectionType || required != RequiredState.Optional || propDec.Type is NullableTypeSyntax) return true; // only applies to nonnullable optional collections

            if (propDec.Initializer == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(MessageTypesMustInstantiateCollections, propDec.Identifier.GetLocation()));
                return false;
            }

            return true;
        }

        static bool PropertiesOnMessageTypes_MustHaveAtLeastOneValidationAttribute(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propDec, RequiredState required)
        {
            if (required == RequiredState.Unspecified)
            {
                context.ReportDiagnostic(Diagnostic.Create(PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute, propDec.Identifier.GetLocation()));
                return false;
            }

            return true;
        }

        static bool MessageTypes_MustHaveXmlDocComments(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec)
        {
            var symbol = context.SemanticModel.GetDeclaredSymbol(typeDec, cancellationToken: context.CancellationToken);
            if(string.IsNullOrWhiteSpace(symbol?.GetDocumentationCommentXml(cancellationToken: context.CancellationToken)))
            {
                context.ReportDiagnostic(Diagnostic.Create(MessageTypesMustHaveXmlDocComments, typeDec.Identifier.GetLocation()));
                return false;
            }
            
            return true;
        }

        static bool IdPropertiesOnMessageTypes_MustBeACaseInsensitiveStringTinyType(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propDec)
        {
            // only applies to properties ending in Id, except SpaceId (handled below); also bail if we can't find the declaration of the CaseInsensitiveStringTinyType type
            if (!propDec.Identifier.Text.EndsWith("Id") || propDec.Identifier.Text == "SpaceId" || cachedTypes.CaseInsensitiveStringTinyType == null) return true;

            var propType = propDec.Type is NullableTypeSyntax n ? n.ElementType : propDec.Type;

            var typeInfo = context.SemanticModel.GetTypeInfo(propType);
            if (typeInfo.Type == null) return false;

            if (!typeInfo.Type.IsAssignableTo(cachedTypes.CaseInsensitiveStringTinyType))
            {
                context.ReportDiagnostic(Diagnostic.Create(IdPropertiesOnMessageTypesMustBeACaseInsensitiveStringTinyType, propDec.Identifier.GetLocation()));
                return false;
            }

            return true;
        }

        static bool SpaceIdPropertiesOnMessageTypes_MustBeOfTypeSpaceId(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propDec)
        {
            // only applies to properties literally called SpaceId; also bail if we can't find the declaration of the SpaceId type
            if (propDec.Identifier.Text != "SpaceId" || cachedTypes.SpaceId == null) return true;

            var propType = propDec.Type is NullableTypeSyntax n ? n.ElementType : propDec.Type;

            var typeInfo = context.SemanticModel.GetTypeInfo(propType);
            if (typeInfo.Type == null) return false;

            if (!SymbolEqualityComparer.Default.Equals(typeInfo.Type, cachedTypes.SpaceId))
            {
                context.ReportDiagnostic(Diagnostic.Create(SpaceIdPropertiesOnMessageTypesMustBeOfTypeSpaceId, propDec.Identifier.GetLocation()));
                return false;
            }

            return true;
        }

        static bool CommandTypes_MustBeNamedCorrectly(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec)
        {
            if (CommandNameRegex.IsMatch(typeDec.Identifier.Text))
                return true;

            context.ReportDiagnostic(Diagnostic.Create(CommandTypesMustBeNamedCorrectly, typeDec.Identifier.GetLocation()));
            return false;
        }

        static bool CommandTypes_MustHaveCorrectlyNamedResponseTypes(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec, GenericNameSyntax requestOrCommandDec)
            => CheckResponseTypeName(context,
                typeDec,
                requestOrCommandDec,
                "Command",
                CommandTypesMustHaveCorrectlyNamedResponseTypes);

        static bool RequestTypes_MustBeNamedCorrectly(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec)
        {
            if (RequestNameRegex.IsMatch(typeDec.Identifier.Text))
                return true;

            var diagnostic = Diagnostic.Create(RequestTypesMustBeNamedCorrectly, typeDec.Identifier.GetLocation());
            context.ReportDiagnostic(diagnostic);
            return false;
        }

        static bool RequestTypes_MustHaveCorrectlyNamedResponseTypes(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec, GenericNameSyntax requestOrCommandDec)
            => CheckResponseTypeName(context,
                typeDec,
                requestOrCommandDec,
                "Request",
                RequestTypesMustHaveCorrectlyNamedResponseTypes);

        // ----- helpers --------------- 

        // typeDec is the Request/Command concrete class declaration
        // genericDec is the <TRequest, TResponse> part of the IRequest/Command declaration
        static bool CheckResponseTypeName(SyntaxNodeAnalysisContext context,
            TypeDeclarationSyntax typeDec,
            GenericNameSyntax genericDec,
            string requestOrCommand,
            DiagnosticDescriptor diagnosticToRaise)
        {
            var typeList = genericDec.ChildNodes().OfType<TypeArgumentListSyntax>().FirstOrDefault(tl => tl.Arguments.Count == 2);
            if (typeList?.Arguments[1] is IdentifierNameSyntax ns)
            {
                var responseTypeStr = ns.Identifier.Text;
                // given class FooRequest : IRequest<FooRequest, FooResponse> we have extracted "FooResponse"

                var expectedName = ReplaceLast(typeDec.Identifier.Text, requestOrCommand, "Response");
                if (responseTypeStr != expectedName)
                {
                    // Future: we should be able to publish a fix-it given we know what the name is supposed to be.
                    var diagnostic = Diagnostic.Create(diagnosticToRaise, typeDec.Identifier.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                    return false;
                }
            }

            return true;
        }

        // "Request" might be part of the name of the request DTO, so we only want to replace the last occurrence of the word "Request"
        static string ReplaceLast(string str, string oldValue, string newValue)
        {
            var pos = str.LastIndexOf(oldValue, StringComparison.Ordinal);
            return pos != -1 ? str.Remove(pos, oldValue.Length).Insert(pos, newValue) : str;
        }
    }
}