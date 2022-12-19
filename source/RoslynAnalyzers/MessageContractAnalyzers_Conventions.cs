using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static Octopus.RoslynAnalyzers.Descriptors;

namespace Octopus.RoslynAnalyzers
{
    // ReSharper disable UnusedMethodReturnValue.Local

    // This part of the MessageContractAnalyzers contains all the specific methods which enforce each convention
    public partial class MessageContractAnalyzers
    {
        static bool ApiContractTypes_MustLiveInTheAppropriateNamespace(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec)
        {
            var ns = typeDec.GetNamespace();
            if (ns == "") return true; // skip if we can't determine the namespace

            if (ns.StartsWith("Octopus.Server.MessageContracts") ||
                (ns.StartsWith("Octopus.Server.Extensibility") && ns.EndsWith(".MessageContracts")) ||
                // this last one is a temporary exemption until the dependency consolidation work brings the Octopus.Server.MessageContracts
                // back into the main git repository. Remove it after that work completes
                (ns.StartsWith("Octopus.Core") && ns.Contains("MessageContracts")))
            {
                // we're good
                return true;
            }

            context.ReportDiagnostic(Diagnostic.Create(ApiContractTypesMustLiveInTheAppropriateNamespace, typeDec.Identifier.GetLocation()));
            return false;
        }

        static bool WeDoNotUseEnums_InOurApiSurface(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propDec, bool isExperimental)
        {
            // types marked experimental such as BFF's are allowed to use enums as they aren't constrained by compatibility across more than one version.
            if (isExperimental) return true;
            
            var systemEnum = context.Compilation.GetSpecialType(SpecialType.System_Enum);

            var propertyType = propDec.Type is NullableTypeSyntax n ? n.ElementType : propDec.Type;
            var propertyTypeInfo = context.SemanticModel.GetTypeInfo(propertyType);
            
            if (SymbolEqualityComparer.Default.Equals(propertyTypeInfo.Type?.BaseType, systemEnum))
            {
                context.ReportDiagnostic(Diagnostic.Create(WeDoNotUseEnumsInOurApiSurface,
                    location: propDec.Identifier.GetLocation(),
                    propDec.Identifier.Text));
                return false;
            }

            return true;
        }

        static bool PropertiesOnMessageTypes_MustBePublic(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propDec)
        {
            var isPublic = propDec.Modifiers.Any(SyntaxKind.PublicKeyword);
            if (isPublic) return true;

            context.ReportDiagnostic(Diagnostic.Create(PropertiesOnMessageTypesMustBePublic,
                location: propDec.Identifier.GetLocation(),
                propDec.Identifier.Text));
            return false;
        }

        static bool PropertiesOnMessageTypes_MustBeMutable(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propDec)
        {
            var hasSetter = propDec.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)) ?? false;
            if (hasSetter) return true;

            context.ReportDiagnostic(Diagnostic.Create(PropertiesOnMessageTypesMustBeMutable,
                location: propDec.Identifier.GetLocation(),
                propDec.Identifier.Text));
            return false;
        }

        static bool RequiredPropertiesOnMessageTypes_MustNotBeNullable(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propDec, RequiredState required)
        {
            if (required == RequiredState.Required && propDec.Type is NullableTypeSyntax nts)
            {
                var typeInfo = ModelExtensions.GetTypeInfo(context.SemanticModel, nts.ElementType);
                context.ReportDiagnostic(Diagnostic.Create(RequiredPropertiesOnMessageTypesMustNotBeNullable,
                    location: propDec.Identifier.GetLocation(),
                    propDec.Identifier.Text,
                    CSharpNameForType(typeInfo.Type)));
                return false;
            }

            return true;
        }

        static bool OptionalPropertiesOnMessageTypes_MustBeInitializedOrNullable(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propDec, RequiredState required)
        {
            if (required == RequiredState.Optional && propDec.Type is not NullableTypeSyntax && propDec.Initializer == null)
            {
                var typeInfo = ModelExtensions.GetTypeInfo(context.SemanticModel, propDec.Type);

                // non-nullable optional property that isn't a collection (strings are enumerable but not collections)
                context.ReportDiagnostic(Diagnostic.Create(OptionalPropertiesOnMessageTypesMustBeInitializedOrNullable,
                    location: propDec.Identifier.GetLocation(),
                    propDec.Identifier.Text,
                    CSharpNameForType(typeInfo.Type)));
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

        static bool MessageTypes_MustHaveXmlDocComments(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec, bool isExperimental)
        {
            // Filter out types that have [Experimental] attribute, those don't need XMLDocs.
            // The syntax model is faster than the semantic model so we check this first 
            if (isExperimental) return true;

            var symbol = ModelExtensions.GetDeclaredSymbol(context.SemanticModel, typeDec, cancellationToken: context.CancellationToken);
            if (string.IsNullOrWhiteSpace(symbol?.GetDocumentationCommentXml(cancellationToken: context.CancellationToken)))
            {
                context.ReportDiagnostic(Diagnostic.Create(MessageTypesMustHaveXmlDocComments, typeDec.Identifier.GetLocation()));
                return false;
            }

            return true;
        }

        static bool SpaceIdPropertiesOnMessageTypes_MustBeOfTypeSpaceId(SyntaxNodeAnalysisContext context, PropertyDeclarationSyntax propDec)
        {
            var spaceIdType = context.Compilation.GetTypeByMetadataName("Octopus.Server.MessageContracts.Features.Spaces.SpaceId");

            // only applies to properties literally called SpaceId; also bail if we can't find the declaration of the SpaceId type
            if (propDec.Identifier.Text != "SpaceId" || spaceIdType == null) return true;

            var propType = propDec.Type is NullableTypeSyntax n ? n.ElementType : propDec.Type;

            var typeInfo = ModelExtensions.GetTypeInfo(context.SemanticModel, propType);
            if (typeInfo.Type == null) return false;

            if (!SymbolEqualityComparer.Default.Equals(typeInfo.Type, spaceIdType))
            {
                context.ReportDiagnostic(Diagnostic.Create(SpaceIdPropertiesOnMessageTypesMustBeOfTypeSpaceId, propDec.Identifier.GetLocation()));
                return false;
            }

            return true;
        }

        static bool EventTypes_MustBeNamedCorrectly(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec)
        {
            if (EventNameRegex.IsMatch(typeDec.Identifier.Text))
                return true;

            context.ReportDiagnostic(Diagnostic.Create(EventTypesMustBeNamedCorrectly, typeDec.Identifier.GetLocation()));
            return false;
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
                    context.ReportDiagnostic(Diagnostic.Create(diagnosticToRaise,
                        location: typeDec.Identifier.GetLocation(),
                        expectedName,
                        responseTypeStr));
                    return false;
                }
            }

            return true;
        }
    }
}