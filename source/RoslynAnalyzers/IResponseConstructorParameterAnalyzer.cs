using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IResponseConstructorParameterAnalyzer : DiagnosticAnalyzer
    {
        const string Category = "Octopus";
        const string IResponseInterfaceName = "IResponse";
        const string DomainModelNamespaceRoot = "Octopus.Core.Model";

        const string DiagnosticId = Category + "_IResponseConstructorParameters";

        const string Title = "Domain models are not allowed in " + IResponseInterfaceName + " constructors";

        const string MessageFormat = IResponseInterfaceName + " implementation constructors should not take any types from " + DomainModelNamespaceRoot + ". Domain models should first be mapped to a resource.";

        const string Description = IResponseInterfaceName + " forms part of the boundary between the HTTP and API layers of the Octopus Server. Domain models should not leave the API, so implementations of " + IResponseInterfaceName + " should not take any model types. Instead, map domain models to a corresponding resources.";

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
            context.RegisterSyntaxNodeAction(DetectIfIResponseConstructorPassesAModelType, SyntaxKind.ClassDeclaration);
        }

        void DetectIfIResponseConstructorPassesAModelType(SyntaxNodeAnalysisContext context)
        {
            foreach (var constructor in GetAllConstructorsFromClassesThatImplementIResponse(context))
            {
                foreach (var parameterSymbol in constructor.Parameters)
                {
                    ReportDiagnosticForParameterIfItIsADomainModel(parameterSymbol, context);
                }
            }
        }

        ImmutableArray<IMethodSymbol> GetAllConstructorsFromClassesThatImplementIResponse(SyntaxNodeAnalysisContext context)
        {
            if (context.SemanticModel.GetDeclaredSymbol(context.Node) is INamedTypeSymbol symbol &&
                ImplementsIResponse(symbol))
            {
                return symbol.Constructors;
            }

            return ImmutableArray<IMethodSymbol>.Empty;
        }

        void ReportDiagnosticForParameterIfItIsADomainModel(IParameterSymbol? parameterSymbol, SyntaxNodeAnalysisContext context)
        {
            if (parameterSymbol == null || parameterSymbol.Type.ContainingNamespace == null)
            {
                return;
            }

            foreach (var namespaceSymbol in parameterSymbol.Type.ContainingNamespace.ConstituentNamespaces)
            {
                if (namespaceSymbol == null || !namespaceSymbol.ToString().StartsWith(DomainModelNamespaceRoot))
                {
                    continue;
                }

                var parameterLocation = GetParameterLocation(parameterSymbol);
                if (parameterLocation != null)
                {
                    var diagnostic = Diagnostic.Create(Rule, parameterLocation);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        static Location? GetParameterLocation(IParameterSymbol parameterSymbol)
        {
            var parameterDeclaringSyntaxReference = parameterSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            return parameterDeclaringSyntaxReference?.GetSyntax().GetLocation();
        }

        static bool ImplementsIResponse(INamedTypeSymbol convertedType)
        {
            var implementedInterfaces = convertedType?.AllInterfaces;
            if (implementedInterfaces != null)
            {
                foreach (var implementedInterface in implementedInterfaces)
                {
                    if (implementedInterface != null && implementedInterface.Name.Equals("IResponse"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}