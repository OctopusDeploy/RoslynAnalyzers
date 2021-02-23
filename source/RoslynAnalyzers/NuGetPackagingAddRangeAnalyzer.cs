using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NuGetPackagingAddRangeAnalyzer : DiagnosticAnalyzer
    {
        const string DiagnosticId = "Octopus_NuGetPackagingAddRange";

        const string Title = "Call to AddRange in NuGet Extensions";

        const string MessageFormat = "This call uses the NuGet Extension methods, but it should use the one we define instead. Remove the NuGet.Packaging namespace and add Octopus.CoreUtilities.Extensions.";
        const string Category = "Octopus";

        const string Description = @"The NuGet.Packaging dll defines an AddRange extension method. We want to avoid using it as it'll lead to import clashes later.";

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
            context.RegisterSyntaxNodeAction(CheckForNuGetPackageAddRangeCall, SyntaxKind.InvocationExpression);
        }

        void CheckForNuGetPackageAddRangeCall(SyntaxNodeAnalysisContext context)
        {
            if (
                context.Node is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "AddRange"
            )
            {
                var symbol =
                    context.SemanticModel.GetSymbolInfo(memberAccess).Symbol as IMethodSymbol;
                var containingType = symbol?.ReducedFrom?.ContainingType;

                if (
                    containingType != null &&
                    containingType.IsStatic &&
                    containingType.IsNonGenericType("CollectionExtensions", "NuGet", "Packaging")
                )
                {
                    var diagnostic = Diagnostic.Create(Rule, memberAccess.Name.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}