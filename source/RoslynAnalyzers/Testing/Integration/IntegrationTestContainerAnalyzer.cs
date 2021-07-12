using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers.Testing.Integration
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IntegrationTestContainerAnalyzer : OctopusTestingDiagnosticAnalyzer<INamedTypeSymbol>
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            Descriptors.Oct2004IntegrationTestContainerClassMustBeStatic,
            Descriptors.Oct2005DoNotNestIntegrationTestContainerClasses,
            Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethods,
            Descriptors.Oct2007IntegrationTestContainersMethodsMustBePrivate);

        internal override void AnalyzeCompilation(INamedTypeSymbol classSymbol, SymbolAnalysisContext context, OctopusTestingContext octopusTestingContext)
        {
            // A type is considered an integration test container if it has at least
            // 1 nested type that is assignable to integration test.
            if (!classSymbol.ContainsTypesInheritingFromSpecifiedType(octopusTestingContext.IntegrationTestType))
            {
                return;
            }

            if (!classSymbol.IsStatic)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2004IntegrationTestContainerClassMustBeStatic, classSymbol.Locations.First()));
            }

            if (!classSymbol.IsDirectlyInNamespace())
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2005DoNotNestIntegrationTestContainerClasses, classSymbol.Locations.First()));
            }

            var memberSymbolsThatAreNotTypeOrMethodDefinitions = classSymbol
                .GetMembers()
                .ExceptOfType<ITypeSymbol>()
                .ExceptOfType<IMethodSymbol>()
                .ExceptImplicitlyDeclared();

            foreach (var symbol in memberSymbolsThatAreNotTypeOrMethodDefinitions)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethods, symbol.Locations.First()));
            }

            var methodsOnClassThatAreNotPrivate = classSymbol
                .GetAllMembersOfType<IMethodSymbol>()
                .ExceptImplicitlyDeclared()
                .ExceptPropertyAccessors()
                .Where(m => m.DeclaredAccessibility != Accessibility.Private);

            foreach (var symbol in methodsOnClassThatAreNotPrivate)
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2007IntegrationTestContainersMethodsMustBePrivate, symbol.Locations.First()));
            }
        }
    }
}