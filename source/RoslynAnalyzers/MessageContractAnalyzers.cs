using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Text.RegularExpressions;
using System;
using System.Runtime.InteropServices.ComTypes;

namespace Octopus.RoslynAnalyzers
{
    // Once we establish that a type is a "Message" (that is, it implements ICommand<>, IRequest<> or IResponse) then
    // there's a whole variety of things that we want to assert against. We put them all in one analyzer to avoid the extra work that would
    // be incurred if we had a dozen analyzers all going "is this a MessageType"?
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class MessageContractAnalyzers : DiagnosticAnalyzer
    {
        const string Category = "Octopus";

        internal static readonly DiagnosticDescriptor Octopus_CommandTypesMustBeNamedCorrectly = new(
            "Octopus_CommandTypesMustBeNamedCorrectly",
            "Types that implement ICommand must be named correctly",
            "Types that implement ICommand must be called <thing>Command",
            Category,
            DiagnosticSeverity.Error,
            true);

        internal static readonly DiagnosticDescriptor Octopus_RequestTypesMustBeNamedCorrectly = new(
            "Octopus_RequestTypesMustBeNamedCorrectly",
            "Types that implement IRequest must be named correctly",
            "Types that implement IRequest must be called <thing>Request",
            Category,
            DiagnosticSeverity.Error,
            true);

        internal static readonly DiagnosticDescriptor Octopus_CommandTypesMustHaveCorrectlyNamedResponseTypes = new(
            "Octopus_CommandTypesMustHaveCorrectlyNamedResponseTypes",
            "Types that implement ICommand must have responses with matching names",
            "Types that implement ICommand have response types called <thing>Response",
            Category,
            DiagnosticSeverity.Error,
            true);

        internal static readonly DiagnosticDescriptor Octopus_RequestTypesMustHaveCorrectlyNamedResponseTypes = new(
            "Octopus_RequestTypesMustHaveCorrectlyNamedResponseTypes",
            "Types that implement IRequest must have responses with matching names",
            "Types that implement IRequest have response types called <thing>Response",
            Category,
            DiagnosticSeverity.Error,
            true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            Octopus_CommandTypesMustBeNamedCorrectly,
            Octopus_RequestTypesMustBeNamedCorrectly,
            Octopus_CommandTypesMustHaveCorrectlyNamedResponseTypes,
            Octopus_RequestTypesMustHaveCorrectlyNamedResponseTypes);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(CheckNode, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
        }

        static readonly Regex RequestNameRegex = new("(?<!V\\d+)Request(V\\d+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex CommandNameRegex = new("(?<!V\\d+)Command(V\\d+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        const string IRequestName = "IRequest";
        const string ICommandName = "ICommand";
        const string IResponseName = "IResponse";

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
                            case GenericNameSyntax { Identifier.Text: IRequestName or ICommandName } g:
                                requestOrCommandDec = g;
                                break;
                            case IdentifierNameSyntax { Identifier.Text: IResponseName } i:
                                responseDec = i;
                                break;
                        }
                    }
                }

                if (requestOrCommandDec != null || responseDec != null)
                {
                    // this is a "MessageType"; either request, command, or response
                    PropertiesOnMessageTypes_MustBeMutable(context, typeDec);
                }

                // request/command/response specific
                switch (requestOrCommandDec?.Identifier.Text)
                {
                    case ICommandName:
                        if (CommandTypes_MustBeNamedCorrectly(context, typeDec))
                            CommandTypes_MustHaveCorrectlyNamedResponseTypes(context, typeDec, requestOrCommandDec); // the expected response name depends on the command name; so only run this check if the CommandName was good
                        break;

                    case IRequestName:
                        if (RequestTypes_MustBeNamedCorrectly(context, typeDec))
                            RequestTypes_MustHaveCorrectlyNamedResponseTypes(context, typeDec, requestOrCommandDec); // the expected response name depends on the request name; so only run this check if the RequestName was good
                        break;
                }
            }
        }

        // ----- specific conventions ---------------

        static bool PropertiesOnMessageTypes_MustBeMutable(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec)
        {
            return false;
        }
        
        static bool CommandTypes_MustBeNamedCorrectly(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec)
        {
            if (CommandNameRegex.IsMatch(typeDec.Identifier.Text))
                return true;

            var diagnostic = Diagnostic.Create(Octopus_CommandTypesMustBeNamedCorrectly, typeDec.Identifier.GetLocation());
            context.ReportDiagnostic(diagnostic);
            return false;
        }

        static bool CommandTypes_MustHaveCorrectlyNamedResponseTypes(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec, GenericNameSyntax requestOrCommandDec)
            => CheckResponseTypeName(context,
                typeDec,
                requestOrCommandDec,
                "Command",
                Octopus_CommandTypesMustHaveCorrectlyNamedResponseTypes);

        static bool RequestTypes_MustBeNamedCorrectly(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec)
        {
            if (RequestNameRegex.IsMatch(typeDec.Identifier.Text))
                return true;

            var diagnostic = Diagnostic.Create(Octopus_RequestTypesMustBeNamedCorrectly, typeDec.Identifier.GetLocation());
            context.ReportDiagnostic(diagnostic);
            return false;
        }

        static bool RequestTypes_MustHaveCorrectlyNamedResponseTypes(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec, GenericNameSyntax requestOrCommandDec)
            => CheckResponseTypeName(context,
                typeDec,
                requestOrCommandDec,
                "Request",
                Octopus_RequestTypesMustHaveCorrectlyNamedResponseTypes);

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
            if (typeList != null && typeList.Arguments[1] is IdentifierNameSyntax idns)
            {
                var responseTypeStr = idns.Identifier.Text;
                // given class FooRequest : IRequest<FooRequest, FooResponse> we have extracted "FooResponse"

                var expectedName = ReplaceLast(typeDec.Identifier.Text, requestOrCommand, "Response");
                if (responseTypeStr != expectedName)
                {
                    // TODO we should be able to publish a fix-it given we know what the name is supposed to be
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
            if (pos != -1)
            {
                return str.Remove(pos, oldValue.Length).Insert(pos, newValue);
            }

            return str;
        }
    }
}