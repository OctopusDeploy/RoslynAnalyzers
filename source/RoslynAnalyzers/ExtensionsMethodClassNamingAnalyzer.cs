using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ExtensionsMethodClassNamingAnalyzer : DiagnosticAnalyzer
    {
        const string DiagnosticId = "Octopus_ExtensionsMethodClassNaming";

        const string Title = "Extension method class naming";

        const string MessageFormat = "Extension methods should be in a class ending in ExtensionMethods";
        const string Category = "Octopus";

        const string Description = @"Octopus has Extensions, which are very different things to extension methods. Our convention is to put extension methods in classes ending in ExtensionMethods.";

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

        void CheckNaming(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is MethodDeclarationSyntax method &&
                method.ParameterList.Parameters.Count >= 1 &&
                method.ParameterList.Parameters[0].Modifiers.Any(m => m.Text == "this") &&
                method.Parent is ClassDeclarationSyntax classDec &&
                !classDec.Identifier.Text.EndsWith("ExtensionMethods")
            )
            {
                var diagnostic = Diagnostic.Create(Rule, classDec.Identifier.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}