using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text.RegularExpressions;
using System;
using System.Diagnostics;
using System.Threading;
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
            OptionalPropertiesOnMessageTypesMustBeInitializedOrNullable,
            PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute,
            SpaceIdPropertiesOnMessageTypesMustBeOfTypeSpaceId,
            MessageTypesMustHaveXmlDocComments,
            ApiContractTypesMustLiveInTheAppropriateNamespace);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
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

        void CheckNode(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is not TypeDeclarationSyntax typeDec) return; // we are only interested in class or struct declarations

            // This analyzer inspects "API Surface" types; i.e. Commands, Requests, Responses, Events and Resources
            // first we have to figure out which kind of type we've encountered (if any)
            var apiSurfaceType = DetermineApiSurfaceType(typeDec);
            if (apiSurfaceType == null) return;

            // if we get here this is an "API Surface" type; either (request, command, response) or event or resource
            // note technically everything else in the Octopus.Server.MessageContracts namespace is also an "API surface" type, 
            // but verifying that would be more expensive and we don't need to do it yet
            ApiContractTypes_MustLiveInTheAppropriateNamespace(context, typeDec);

            if (apiSurfaceType is MessageType messageType)
            {
                // this is a "MessageType"; either request, command, or response
                CheckMessageTypeProperties(context, typeDec);
                MessageTypes_MustHaveXmlDocComments(context, typeDec);

                switch (messageType)
                {
                    case MessageType.Command commandType:
                        if (CommandTypes_MustBeNamedCorrectly(context, typeDec))
                        {
                            // the expected response name depends on the command name; so only run this check if the CommandName was good
                            CommandTypes_MustHaveCorrectlyNamedResponseTypes(context, typeDec, commandType.NameSyntax);
                        }

                        break;
                    case MessageType.Request requestType:
                        if (RequestTypes_MustBeNamedCorrectly(context, typeDec))
                        {
                            // the expected response name depends on the request name; so only run this check if the RequestName was good
                            RequestTypes_MustHaveCorrectlyNamedResponseTypes(context, typeDec, requestType.NameSyntax);
                        }

                        break;
                }
                // no specific checks for responses at this point
            }
            else if (apiSurfaceType is ApiSurfaceType.Event)
            {
                EventTypes_MustBeNamedCorrectly(context, typeDec);
            }
            // no specific checks for resources at this point
        }

        static bool CheckMessageTypeProperties(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec)
        {
            var result = true;
            foreach (var propDec in typeDec.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                // only validate public properties
                if (!propDec.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) continue;

                var required = GetRequiredState(propDec);

                // if anything returns false, propagate false outwards
                result &= PropertiesOnMessageTypes_MustBeMutable(context, propDec);
                result &= RequiredPropertiesOnMessageTypes_MustNotBeNullable(context, propDec, required);
                result &= OptionalPropertiesOnMessageTypes_MustBeInitializedOrNullable(context, propDec, required);
                result &= PropertiesOnMessageTypes_MustHaveAtLeastOneValidationAttribute(context, propDec, required);
                result &= SpaceIdPropertiesOnMessageTypes_MustBeOfTypeSpaceId(context, propDec);
            }

            return result;
        }

        // ----- helpers --------------- 

        // Don't think of this as an OOP class hierarchy; think of it as an Enum with associated values.
        //
        // it bugs me that these aren't structs because our syntax parser is now incurring allocations.
        // I could ObjectPool and make these things mutable but that seems like overkill. Sit on it for a while and think.
        public abstract record ApiSurfaceType
        {
            public record Event(IdentifierNameSyntax NameSyntax) : ApiSurfaceType;

            public record Resource(IdentifierNameSyntax NameSyntax) : ApiSurfaceType;
        }

        public abstract record MessageType : ApiSurfaceType
        {
            public record Command(GenericNameSyntax NameSyntax) : MessageType;

            public record Request(GenericNameSyntax NameSyntax) : MessageType;

            public record Response(IdentifierNameSyntax NameSyntax) : MessageType;
        }

        static ApiSurfaceType? DetermineApiSurfaceType(TypeDeclarationSyntax typeDeclaration)
        {
            var baseTypeList = typeDeclaration.BaseList?.ChildNodes().OfType<SimpleBaseTypeSyntax>().SelectMany(baseTypeDec => baseTypeDec.ChildNodes());
            foreach (var baseTypeNode in baseTypeList ?? Enumerable.Empty<SyntaxNode>())
            {
                switch (baseTypeNode)
                {
                    // This will match anything which has a basetype called ICommand<> whether it's our octopus one or not.
                    // In practice this should be fine, we don't have other ICommand<> or IRequest<> types running around our codebase and the full namespace check is more work.
                    case GenericNameSyntax genericName:
                        switch (genericName.Identifier.Text)
                        {
                            case TypeNameIRequest:
                                return new MessageType.Request(genericName);
                            case TypeNameICommand:
                                return new MessageType.Command(genericName);
                        }

                        break;

                    case IdentifierNameSyntax identifierName:
                        switch (identifierName.Identifier.Text)
                        {
                            case TypeNameIResponse:
                                return new MessageType.Response(identifierName);
                            case TypeNameIEvent:
                                return new ApiSurfaceType.Event(identifierName);
                            case TypeNameResource:
                                return new ApiSurfaceType.Resource(identifierName);
                        }

                        break;
                }
            }

            return null;
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

            var enumerableType = context.Compilation.GetSpecialType(SpecialType.System_Collections_IEnumerable);
            var stringType = context.Compilation.GetSpecialType(SpecialType.System_String);
            return typeInfo.Type.IsAssignableTo(enumerableType) && !SymbolEqualityComparer.Default.Equals(typeInfo.Type, stringType);
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
            if(symName == "") symName = symbol?.ToDisplayString(); // explicit empty string is the "name" for complex types like string[]
            
            return symName switch
            {
                nameof(Byte) => "byte",
                nameof(SByte) => "sbyte",
                nameof(Int16) => "short",
                nameof(UInt16) => "ushort",
                nameof(Int32) => "int",
                nameof(UInt32) => "uint",
                nameof(Int64) => "long",
                nameof(UInt64) => "ulong",
                nameof(Single) => "float",
                nameof(Double) => "double",
                nameof(Decimal) => "decimal",
                nameof(Object) => "object",
                nameof(Boolean) => "bool",
                nameof(Char) => "char",
                nameof(String) => "string",
                // System.Void is a builtin type, but it cannot be used in C#
                null => "null",
                _ => symName, // all other types get left alone
            };
        }
    }
}

// Hack to let us use records targeting netstandard2.0
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}