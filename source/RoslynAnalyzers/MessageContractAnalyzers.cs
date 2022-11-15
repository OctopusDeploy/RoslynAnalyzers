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
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CommandAndRequestTypesMustBeNamedCorrectlyAnalyzer : DiagnosticAnalyzer
    {
        public const string Category = "Octopus";

        public const string CommandNameDiagnosticId = "Octopus_CommandTypesMustBeNamedCorrectly";
        public const string CommandNameTitle = "Types that implement ICommand must be named correctly";
        public const string CommandNameMessageFormat = "Types that implement ICommand must be called <thing>Command";

        public const string RequestNameDiagnosticId = "Octopus_RequestTypesMustBeNamedCorrectly";
        public const string RequestNameTitle = "Types that implement IRequest must be named correctly";
        public const string RequestNameMessageFormat = "Types that implement IRequest must be called <thing>Request";

        public const string CommandResponseNameDiagnosticId = "Octopus_CommandTypesMustHaveCorrectlyNamedResponseTypes";
        public const string CommandResponseNameTitle = "Types that implement ICommand must have responses with matching names";
        public const string CommandResponseNameMessageFormat = "Types that implement ICommand have response types called <thing>Response";

        public const string RequestResponseNameDiagnosticId = "Octopus_RequestTypesMustHaveCorrectlyNamedResponseTypes";
        public const string RequestResponseNameTitle = "Types that implement IRequest must have responses with matching names";
        public const string RequestResponseNameMessageFormat = "Types that implement IRequest have response types called <thing>Response";

        internal static readonly DiagnosticDescriptor CommandNameRule = new DiagnosticDescriptor(
            CommandNameDiagnosticId,
            CommandNameTitle,
            CommandNameMessageFormat,
            Category,
            DiagnosticSeverity.Error,
            true);

        internal static readonly DiagnosticDescriptor RequestNameRule = new DiagnosticDescriptor(
            RequestNameDiagnosticId,
            RequestNameTitle,
            RequestNameMessageFormat,
            Category,
            DiagnosticSeverity.Error,
            true);

        internal static readonly DiagnosticDescriptor CommandResponseNameRule = new DiagnosticDescriptor(
            CommandResponseNameDiagnosticId,
            CommandResponseNameTitle,
            CommandResponseNameMessageFormat,
            Category,
            DiagnosticSeverity.Error,
            true);

        internal static readonly DiagnosticDescriptor RequestResponseNameRule = new DiagnosticDescriptor(
            RequestResponseNameDiagnosticId,
            RequestResponseNameTitle,
            RequestResponseNameMessageFormat,
            Category,
            DiagnosticSeverity.Error,
            true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            CommandNameRule, 
            RequestNameRule,
            CommandResponseNameRule,
            RequestResponseNameRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(CheckNaming, SyntaxKind.ClassDeclaration, SyntaxKind.StructDeclaration);
        }

        static readonly Regex RequestNameRegex = new("(?<!V\\d+)Request(V\\d+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex CommandNameRegex = new("(?<!V\\d+)Command(V\\d+)*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        const string IRequestName = "IRequest";
        const string ICommandName = "ICommand";

        // typeDec is the Request/Command concrete class declaration
        // genericDec is the <TRequest, TResponse> part of the IRequest/Command declaration
        static void CheckResponseTypeName(SyntaxNodeAnalysisContext context, TypeDeclarationSyntax typeDec, GenericNameSyntax genericDec, string requestOrCommand, DiagnosticDescriptor diagnosticToRaise)
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
                }
            }
        }

        static void CheckNaming(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is TypeDeclarationSyntax typeDec)
            {
                // This will match anything which has a basetype called ICommand<> whether it's our octopus one or not.
                // In practice this should be fine, we don't have other ICommand<> or IRequest<> types running around our codebase and the namespace check is more work to do.

                var baseTypeDec = typeDec.BaseList?.ChildNodes().OfType<SimpleBaseTypeSyntax>().FirstOrDefault();
                var genericDec = baseTypeDec?.ChildNodes().OfType<GenericNameSyntax>().FirstOrDefault(g => g.Identifier.Text is IRequestName or ICommandName);

                if (genericDec?.Identifier.Text == ICommandName)
                {
                    if (!CommandNameRegex.IsMatch(typeDec.Identifier.Text))
                    {
                        var diagnostic = Diagnostic.Create(CommandNameRule, typeDec.Identifier.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                    }
                    else
                    {   // only run the MustHaveCorrectlyNamedResponseTypes logic if the command name was correct.
                        // the expected response name depends on the command name; so it's garbage if the command name isn't right
                        CheckResponseTypeName(context, typeDec, genericDec, "Command", CommandResponseNameRule);
                    }

                    return;
                    // we should never have a class that implements both IRequest and ICommand.
                    // the analyzer could pick this up, but nobody's going to actually do that so not worth it.
                }

                if (genericDec?.Identifier.Text == IRequestName)
                {
                    if (!RequestNameRegex.IsMatch(typeDec.Identifier.Text))
                    {
                        var diagnostic = Diagnostic.Create(RequestNameRule, typeDec.Identifier.GetLocation());
                        context.ReportDiagnostic(diagnostic);
                    }
                    else
                    {   // only run the MustHaveCorrectlyNamedResponseTypes logic if the request name was correct.
                        // the expected response name depends on the request name; so it's garbage if the request name isn't right
                        CheckResponseTypeName(context, typeDec, genericDec, "Request", RequestResponseNameRule);
                    }
                }
            }
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
