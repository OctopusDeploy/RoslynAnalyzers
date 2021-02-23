using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class JsonLikeQueryAnalyzer : DiagnosticAnalyzer
    {
        const string DiagnosticId = "Octopus_JsonLikeQuery";

        const string Title = "Query containing a like clause on JSON column";

        const string MessageFormat = "This looks like a query that is doing a like clause on the JSON column. This is not very performant and alternatives should be considered.";
        const string Category = "Octopus";

        const string Description = @"like queries on the JSON column are very expensive as SQL has to load all the data and do string searches.";

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
            context.RegisterSyntaxNodeAction(InspectString, SyntaxKind.StringLiteralExpression);
        }

        void InspectString(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is LiteralExpressionSyntax literal)
            {
                var text = literal.Token.Text.ToLower();
                if (
                    text.Length > 8 &&
                    text.Contains("json") &&
                    text.Contains("like")
                )
                {
                    var diagnostic = Diagnostic.Create(Rule, literal.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}