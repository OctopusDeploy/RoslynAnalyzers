using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers.Testing.Unit
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UnitTestClassAnalyzer : OctopusTestingDiagnosticAnalyzer<INamedTypeSymbol>
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptors.Oct2002NoUnitTestBaseClasses);

        internal override void AnalyzeCompilation(INamedTypeSymbol classSymbol, SymbolAnalysisContext context, OctopusTestingContext octopusTestingContext)
        {
            if (classSymbol.IsAssignableToButNotTheSame(octopusTestingContext.UnitTestType)
                && !classSymbol.DirectlyInheritsFrom(octopusTestingContext.UnitTestType))
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2002NoUnitTestBaseClasses, classSymbol.Locations.First()));
            }
        }
    }
}