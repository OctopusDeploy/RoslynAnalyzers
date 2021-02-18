using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CaseSensitiveEnumParsingAnalyzer : DiagnosticAnalyzer
    {
        const string DiagnosticId = "Octopus_CaseSensitiveEnumParsingAnalyzer";

        const string Title = "Enum.Parse is case sensitive";

        const string MessageFormat = "The Enum.Parse call is case sensitive, which is likely not intended. Use an overload that explicitely defines case behaviour, or our `.ToEnum()` extension method.";
        const string Category = "Octopus";

        const string Description = @"Case sensitive Enum parsing has been the cause of several bugs. This analyzer forces the author to specify the casing behavior explicitely.";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            true,
            Description
        );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(CheckForCaseSensitiveEnumParsing, SyntaxKind.InvocationExpression);
        }

        void CheckForCaseSensitiveEnumParsing(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is IdentifierNameSyntax identifierName &&
                identifierName.Identifier.Text == "Enum" &&
                (memberAccess.Name.Identifier.Text == "Parse" || memberAccess.Name.Identifier.Text == "TryParse"))
            {
                var symbol = context.SemanticModel.GetSymbolInfo(memberAccess).Symbol as IMethodSymbol;
                if (symbol == null)
                    return;

                var indexToLookAt = symbol.IsGenericMethod ? 1 : 2;
                if (symbol.Parameters.Length > indexToLookAt && symbol.Parameters[indexToLookAt].Name == "ignoreCase")
                    return;

                var diagnostic = Diagnostic.Create(Rule, memberAccess.Name.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}