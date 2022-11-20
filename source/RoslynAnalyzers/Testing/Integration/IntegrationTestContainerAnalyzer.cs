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
            Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData,
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

            foreach (var symbol in classSymbol.GetMembers().ExceptImplicitlyDeclared())
            {
                switch (symbol)
                {
                    case ITypeSymbol:
                        // nested enums/structs/classes are all OK regardless of public/private/etc
                        // nested IntegrationTest classes themselves fall under this.
                        break;

                    case IMethodSymbol methodSymbol:
                    {
                        // only private methods are OK; ignoring property setters (they are handled below)
                        if (methodSymbol.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet) continue;

                        if (methodSymbol.DeclaredAccessibility != Accessibility.Private)
                        {
                            context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2007IntegrationTestContainersMethodsMustBePrivate, symbol.Locations.First()));
                        }

                        break;
                    }

                    case IFieldSymbol fieldSymbol:
                    {
                        if (fieldSymbol.DeclaredAccessibility != Accessibility.Private)
                        {
                            // fields must be private
                            context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData, symbol.Locations.First()));
                        }

                        if (fieldSymbol.IsConst)
                        {
                            // const is OK; the compiler won't let us put anything mutable in a const field so we don't have to check
                            break;
                        }

                        if (fieldSymbol.IsReadOnly)
                        {
                            // readonly is only OK if it holds a type that is known to be immutable 
                            if (!IsKnownImmutableType(context, fieldSymbol.Type))
                            {
                                context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData, symbol.Locations.First()));
                            }
                        }
                        else
                        {
                            // if it's not readonly or const, then it's definitely not OK
                            context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData, symbol.Locations.First()));
                        }

                        break;
                    }

                    case IPropertySymbol propertySymbol:
                    {
                        if (propertySymbol.DeclaredAccessibility != Accessibility.Private)
                        {
                            // fields must be private
                            context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData, symbol.Locations.First()));
                        }

                        if (propertySymbol.IsReadOnly)
                        {
                            // readonly is only OK if it holds a type that is known to be immutable 
                            if (!IsKnownImmutableType(context, propertySymbol.Type))
                            {
                                context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData, symbol.Locations.First()));
                            }
                        }
                        else
                        {
                            // if it's not readonly or const, then it's definitely not OK
                            context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData, symbol.Locations.First()));
                        }

                        break;
                    }

                    default:
                        // other than types, properties and fields, what else can we put in a class?
                        // whatever it is, report it (this is what the previous version of the analyzer did so we maintain compatibility)
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2006IntegrationTestContainersMustOnlyContainTypesAndMethodsAndImmutableData, symbol.Locations.First()));
                        break;
                }
            }
        }

        static bool IsKnownImmutableType(SymbolAnalysisContext context, ITypeSymbol typeSymbol)
        {
            // we are already enforcing the symbol is readonly, so all ValueTypes are OK
            if (typeSymbol.IsValueType) return true;

            // TODO what about IReadOnlyDictionary
            return typeSymbol.SpecialType switch
            {
                SpecialType.System_Delegate => true,
                SpecialType.System_String => true,
                SpecialType.System_Collections_IEnumerable => true, // IEnumerable exactly is OK; subtypes are not neccessarily
                
                // link from IEnumerable<string> => IEnumerable<> so we can compare
                SpecialType.None => typeSymbol is INamedTypeSymbol { IsGenericType: true } namedTypeSymbol && namedTypeSymbol.OriginalDefinition.SpecialType switch
                {
                    SpecialType.System_Collections_Generic_IEnumerable_T => true,
                    SpecialType.System_Collections_Generic_IReadOnlyList_T => true,
                    SpecialType.System_Collections_Generic_IReadOnlyCollection_T => true,
                    SpecialType.None => namedTypeSymbol.OriginalDefinition.Name switch
                    {
                        "IReadOnlySet" => true,
                        "IReadOnlyDictionary" => true,
                        _ => false
                    },
                    _ => false
                },
                _ => false  
            };
        }
    }
}