using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UsingDeclarationAnalyzer : DiagnosticAnalyzer
    {
        const string DiagnosticId = "Octopus_UsingDeclaration";

        const string Title = "\"Using\" declarations encourage holding onto IDisposable's for longer than needed";

        const string MessageFormat = "Please use braces to specify the scope of the \"Using\" statement instead";
        const string Category = "Octopus";

        const string Description = @"""Using"" declarations (without braces to specify the scope) have been sources of bugs where IDisposable's are held longer than needed. 
The problem is especially tricky if long running code is added to the function AFTER the ""using"" declaration is written.

This is This analyzer bans their usage.";

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
            context.RegisterSyntaxNodeAction(CheckForUsingDeclaration, SyntaxKind.LocalDeclarationStatement);
        }

        void CheckForUsingDeclaration(SyntaxNodeAnalysisContext context)
        {
            if (context.Node is LocalDeclarationStatementSyntax syntax && (string?)syntax.UsingKeyword.Value == "using")
            {
                var diagnostic = Diagnostic.Create(Rule, context.Node.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}