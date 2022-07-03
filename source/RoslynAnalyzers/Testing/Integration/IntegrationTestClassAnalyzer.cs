using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers.Testing.Integration
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IntegrationTestClassAnalyzer : OctopusTestingDiagnosticAnalyzer<INamedTypeSymbol>
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            Descriptors.Oct2001NoIntegrationTestBaseClasses
        );

        internal override void AnalyzeCompilation(INamedTypeSymbol classSymbol, SymbolAnalysisContext context, OctopusTestingContext octopusTestingContext)
        {
            if (!classSymbol.IsAssignableToButNotTheSame(octopusTestingContext.IntegrationTestType))
            {
                return;
            }

            if (!classSymbol.DirectlyInheritsFrom(octopusTestingContext.IntegrationTestType))
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2001NoIntegrationTestBaseClasses, classSymbol.Locations.First()));
            }
        }
    }
}