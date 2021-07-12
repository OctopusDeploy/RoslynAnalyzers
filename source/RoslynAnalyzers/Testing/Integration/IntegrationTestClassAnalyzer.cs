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
            Descriptors.Oct2001NoIntegrationTestBaseClasses,
            Descriptors.Oct2003SingleIntegrationTestInEachClass
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

            var testMethodSymbols = classSymbol
                .GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => m.GetAttributes().Any(a => a.AttributeClass.IsAssignableTo(octopusTestingContext.FactAttributeType)))
                .ToArray();

            if (testMethodSymbols.Length > 1)
            {
                foreach (var testMethodSymbol in testMethodSymbols)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2003SingleIntegrationTestInEachClass, testMethodSymbol.Locations.First()));
                }
            }
        }
    }
}