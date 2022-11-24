using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text.RegularExpressions;
using System;
using static Octopus.RoslynAnalyzers.Descriptors;

namespace Octopus.RoslynAnalyzers
{
    // Once we establish that a type is a "Message" (that is, it implements ICommand<>, IRequest<> or IResponse) then
    // there's a whole variety of things that we want to assert against. We put them all in one analyzer to avoid the extra work that would
    // be incurred if we had a dozen analyzers all going "is this a MessageType"?
    // MessageContractAnalyzers use the ID range OCT3xxx
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public partial class MessageContractAnalyzers : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            EventTypesMustBeNamedCorrectly,
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
            MessageTypesMustHaveXmlDocComments,
            ApiContractTypesMustLiveInTheAppropriateNamespace);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(CacheCommonTypes);
        }

        // ReSharper disable once InconsistentNaming
        record struct SpecialTypeDeclarations(
            INamedTypeSymbol Boolean,
            INamedTypeSymbol String,
            INamedTypeSymbol IEnumerable,
            INamedTypeSymbol? SpaceId,
            INamedTypeSymbol? CaseInsensitiveStringTinyType);

        static SpecialTypeDeclarations cachedTypes; // if you happened to use this before CacheCommonTypes it will blow up; beware

        void CacheCommonTypes(CompilationStartAnalysisContext context)
        {
            cachedTypes = new SpecialTypeDeclarations(
                Boolean: context.Compilation.GetSpecialType(SpecialType.System_Boolean),
                String: context.Compilation.GetSpecialType(SpecialType.System_String),
                IEnumerable: context.Compilation.GetSpecialType(SpecialType.System_Collections_IEnumerable),
                SpaceId: context.Compilation.GetTypeByMetadataName("Octopus.Server.MessageContracts.Features.Spaces.SpaceId"),
                CaseInsensitiveStringTinyType: context.Compilation.GetTypeByMetadataName("Octopus.TinyTypes.CaseInsensitiveStringTinyType"));

            context.RegisterSyntaxNodeAction(CheckNode, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
        }

        static readonly Regex EventNameRegex = new("(?<!V\\d+)Event(V\\d+)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex RequestNameRegex = new("(?<!V\\d+)Request(V\\d+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex CommandNameRegex = new("(?<!V\\d+)Command(V\\d+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        const string TypeNameIRequest = "IRequest";
        const string TypeNameICommand = "ICommand";
        const string TypeNameIResponse = "IResponse";
        const string TypeNameIEvent = "IEvent";
        const string TypeNameResource = "Resource";

        static void CheckNode(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is TypeDeclarationSyntax typeDec)
            {
                // This will match anything which has a basetype called ICommand<> whether it's our octopus one or not.
                // In practice this should be fine, we don't have other ICommand<> or IRequest<> types running around our codebase and the namespace check is more work to do.

                GenericNameSyntax? requestOrCommandDec = null;
                IdentifierNameSyntax? responseEventOrResourceDec = null;
                foreach (var baseTypeDec in typeDec.BaseList?.ChildNodes().OfType<SimpleBaseTypeSyntax>() ?? Enumerable.Empty<SimpleBaseTypeSyntax>())
                {
                    foreach (var c in baseTypeDec.ChildNodes())
                    {
                        switch (c)
                        {
                            case GenericNameSyntax { Identifier.Text: TypeNameIRequest or TypeNameICommand } g:
                                requestOrCommandDec = g;
                                break;
                            case IdentifierNameSyntax { Identifier.Text: TypeNameIResponse or TypeNameIEvent or TypeNameResource } i:
                                responseEventOrResourceDec = i;
                                break;
                        }
                    }
                }

                if (requestOrCommandDec != null || responseEventOrResourceDec != null)
                {
                    // this is an "API Surface" type; either (request, command, response) or event or resource
                    // note technically everything else in the Octopus.Server.MessageContracts namespace is also an "API surface" type, 
                    // but verifying that would be more expensive and we don't need to do it yet
                    ApiContractTypes_MustLiveInTheAppropriateNamespace(context, typeDec);

                    if (requestOrCommandDec != null || responseEventOrResourceDec?.Identifier.Text == TypeNameIResponse)
                    {
                        // this is a "MessageType"; either request, command, or response
                        CheckProperties(context, typeDec);
                        MessageTypes_MustHaveXmlDocComments(context, typeDec);
                    }
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

                if (responseEventOrResourceDec?.Identifier.Text == TypeNameIEvent)
                {
                    EventTypes_MustBeNamedCorrectly(context, typeDec);
                }
            }
        }

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

        // ----- helpers --------------- 

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

        // "Request" might be part of the name of the request DTO, so we only want to replace the last occurrence of the word "Request"
        static string ReplaceLast(string str, string oldValue, string newValue)
        {
            var pos = str.LastIndexOf(oldValue, StringComparison.Ordinal);
            return pos != -1 ? str.Remove(pos, oldValue.Length).Insert(pos, newValue) : str;
        }

        // Roslyn deals in MSIL types rather than C# types, so we get Int32 rather than int.
        // this converts back to language-specific aliases.
        static string CSharpNameForType(ITypeSymbol? symbol)
        {
            var symName = symbol?.Name;
            // there is probably some builtin method to do this reverse lookup; replace this switch if you find out how
            return symName switch
            {
                // add more here if we've missed any.
                "Float" => "float",
                "Double" => "double",
                "String" => "string",
                "Int32" => "int",
                "Int64" => "long",
                null => "null",
                _ => symName,
            };
        }
    }
}