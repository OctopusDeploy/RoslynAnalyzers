using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class BuildersReassigningToMemberVariablesAnalyzer : DiagnosticAnalyzer
    {
        const string DiagnosticId = "Octopus_PreventBuildersReassigningToMemberVariablesFromBuild";

        const string Title = "Reassigning of member variable in builder";

        const string MessageFormat = "Builders should be state bags and not reassign values back to the state when building";
        const string Category = "Octopus";

        const string Description = @"What goes here?.";

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
            context.RegisterSyntaxNodeAction(CheckNaming, SyntaxKind.MethodDeclaration);
        }

        static void CheckNaming(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is MethodDeclarationSyntax { Body: { }, Identifier: { Value: "Build" } } method)
            {
                var expressionStatements = method.Body.Statements.Where(x => x is ExpressionStatementSyntax).Cast<ExpressionStatementSyntax>();
                var assignmentStatements = expressionStatements.Where(x => x.Expression is AssignmentExpressionSyntax);

                foreach (var assignmentStatement in assignmentStatements)
                {
                    var diagnostic = Diagnostic.Create(Rule, assignmentStatement.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

    }
}