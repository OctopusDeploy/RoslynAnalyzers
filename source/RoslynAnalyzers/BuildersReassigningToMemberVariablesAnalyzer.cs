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
            if (!(context.Node is MethodDeclarationSyntax { Body: { }, Identifier: { Value: "Build" } } method))
                return;
            
            var localVariables = method.Body.Statements
                .Where(x => x is LocalDeclarationStatementSyntax)
                .Cast<LocalDeclarationStatementSyntax>()
                .Select(x => x.Declaration)
                .SelectMany(x => x.ChildNodes())
                .Where(syntaxNode => syntaxNode is VariableDeclaratorSyntax)
                .Cast<VariableDeclaratorSyntax>()
                .Select(x => x.Identifier.Value)
                .ToList();
                
            var assignmentStatements = method.Body.Statements
                .Where(x => x is ExpressionStatementSyntax)
                .Select(expressionStatementSyntax => ((ExpressionStatementSyntax)expressionStatementSyntax).Expression)
                .Where(expression => expression is AssignmentExpressionSyntax)
                .Cast<AssignmentExpressionSyntax>();

            foreach (var assignmentStatement in assignmentStatements)
            {
                
                if (assignmentStatement.Left is IdentifierNameSyntax left && localVariables.Contains(left.Identifier.Value))
                {
                    //Local variable, let's allow reassignment to it as it doesn't modify the state of the builder
                    continue;
                }
                if (assignmentStatement.Left is MemberAccessExpressionSyntax memberAccessExpression && memberAccessExpression.Expression is IdentifierNameSyntax identifier && localVariables.Contains(identifier.Identifier.Value))
                {
                    //Local variable, let's allow reassignment to it as it doesn't modify the state of the builder
                    continue;
                }

                var diagnostic = Diagnostic.Create(Rule, assignmentStatement.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

    }
}