using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalysers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PossibleUnintentionalCreationOfEnumeratorAnalyzer : DiagnosticAnalyzer
    {
        const string DiagnosticId = "Octopus_PossibleUnintentionalCreationOfEnumerator";

        const string Title = "Any(), Count() or None() call likely unintentionally creates an enumerator";

        const string MessageFormat = "This Any(), Count() or None() call on this type likely creates an enumerator unintentionally. This can cause performance problems. Use the Count or Length properties instead.";
        const string Category = "Octopus";
        const string Description = @"The Any() and Count() extension methods cause the target to be enumerated unless it 
implements IListProvider<T>, ICollection<T> or ICollection. Most collections do this, but some only implement 
IEnumerable<T>. The creation of an enumerator is expensive relative to calling the Count or Length properties, 
so it should be avoided when possible. We have several implementations of a None() extension method which calls 
Any(). A best effort is used to catch this usage as they should mainly be under the Octopus namespace.";

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
            context.RegisterSyntaxNodeAction(CheckUnwantedMethodCalls, SyntaxKind.InvocationExpression);
        }

        void CheckUnwantedMethodCalls(SyntaxNodeAnalysisContext context)
        {
            if (
                context.Node is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                IsItAnExtensionMethodThatMightCreateAnEnumerator(context, memberAccess) &&
                !IsTheTargetCollectionIgnoredForThisCheck(context, invocation)
            )
            {
                var diagnostic = Diagnostic.Create(Rule, memberAccess.Name.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }

        static bool IsItAnExtensionMethodThatMightCreateAnEnumerator(SyntaxNodeAnalysisContext context, MemberAccessExpressionSyntax memberAccessExpression)
        {
            var symbol = context.SemanticModel.GetSymbolInfo(memberAccessExpression).Symbol as IMethodSymbol;
            symbol = symbol?.ReducedFrom ?? symbol; // If called as an extension method gets the static invocation symbol

            if (
                symbol == null ||
                symbol.Parameters.Length != 1 ||
                !symbol.ContainingType.IsStatic
            )
                return false;

            switch (symbol.Name)
            {
                case "Any":
                case "Count":
                    return symbol.ContainingType.IsNonGenericType("Enumerable", "System", "Linq");
                case "None":
                    return symbol.ContainingNamespace.GetTopMostNamespace().Name == "Octopus";
                default:
                    return false;
            }
        }

        static bool IsTheTargetCollectionIgnoredForThisCheck(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
        {
            var targetCollectionExpression = invocation.ArgumentList.Arguments.Count == 0
                ? ((MemberAccessExpressionSyntax) invocation.Expression).Expression // Target of the extension method
                : invocation.ArgumentList.Arguments[0].Expression; // Statically called argument

            var targetCollectionTypeInfo = context.SemanticModel.GetTypeInfo(targetCollectionExpression);
            if (targetCollectionTypeInfo.Type is IArrayTypeSymbol)
                return true;

            if(targetCollectionTypeInfo.Type is INamedTypeSymbol targetCollectionType)
                return IsIEnumerableOfT(targetCollectionType) ||
                    targetCollectionType.AllInterfaces.Any(IsThisTypeSpeciallyHandledByTheImplementation);

            return false;
        }

        // The Enumerable.Any method will call Length or Count on certain interfaces bypassing the need to create an enumerator
        // See https://github.com/dotnet/runtime/blob/master/src/libraries/System.Linq/src/System/Linq/AnyAll.cs#L18
        // See https://github.com/dotnet/runtime/blob/master/src/libraries/System.Linq/src/System/Linq/Count.cs
        static bool IsThisTypeSpeciallyHandledByTheImplementation(INamedTypeSymbol type)
            => type.IsGenericType("ICollection", 1, "System", "Collections", "Generic") ||
                type.IsGenericType("IListProvider", 1, "System", "Linq") ||
                type.IsNonGenericType("ICollection", "System", "Collections");

        static bool IsIEnumerableOfT(INamedTypeSymbol type)
            => type.IsGenericType("IEnumerable", 1, "System", "Collections", "Generic");
    }
}