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
    public class PossibleUnintentionalCreationOfEnumeratorAnalyzer : DiagnosticAnalyzer
    {
        const string DiagnosticId = "Octopus_PossibleUnintentionalCreationOfEnumerator";

        const string Title = "Any(), Count() or None() call likely unintentionally creates an enumerator";

        const string MessageFormat = "This Any(), Count() or None() call on this type likely creates an enumerator unintentionally. This can cause performance problems. Use our custom extension methods or the Count or Length properties instead.";
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
            context.RegisterSyntaxNodeAction(CheckForUnexpectedEnumeration, SyntaxKind.InvocationExpression);
        }

        void CheckForUnexpectedEnumeration(SyntaxNodeAnalysisContext context)
        {
            if (
                context.Node is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                IsACallWeAreInterestedIn(memberAccess)
            )
            {
                var checkRequired = WhichCollectionsCouldThisCallCreateEnumeratorsFor(context, memberAccess);
                if (checkRequired == CollectionCheckRequired.None)
                    return;

                if (!IsTheTargetCollectionAKnownTypeThatDoesNotCreateAnEnumerator(context, invocation, checkRequired))
                {
                    var diagnostic = Diagnostic.Create(Rule, memberAccess.Name.GetLocation());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        enum CollectionCheckRequired
        {
            None,
            NonKnownTypes,
            All
        }

        static bool IsACallWeAreInterestedIn(MemberAccessExpressionSyntax memberAccessExpression)
        {
            var methodName = memberAccessExpression.Name.Identifier.Text;

            // This provides a quick short circuit as the GetSymbolInfo call (below) is relatively expensive
            switch (methodName)
            {
                case "Any":
                case "Count":
                case "None":
                    return true;
                default:
                    return false;
            }
        }

        static CollectionCheckRequired WhichCollectionsCouldThisCallCreateEnumeratorsFor(SyntaxNodeAnalysisContext context, MemberAccessExpressionSyntax memberAccessExpression)
        {
            var symbol = context.SemanticModel.GetSymbolInfo(memberAccessExpression).Symbol as IMethodSymbol;
            symbol = symbol?.ReducedFrom ?? symbol; // If called as an extension method gets the static invocation symbol

            if (
                symbol == null ||
                symbol.Parameters.Length != 1 ||
                !symbol.ContainingType.IsStatic
            )
                return CollectionCheckRequired.None;

            switch (symbol.Name)
            {
                case "Any":
                    return symbol.ContainingType.IsNonGenericType("Enumerable", "System", "Linq")
                        ? (
                            symbol.ContainingAssembly.Identity.Version.Major >= 5
                                ? CollectionCheckRequired.NonKnownTypes // .NET 5 brought Any inline with Count (see https://github.com/dotnet/corefx/pull/40377)
                                : CollectionCheckRequired.All
                        )
                        : CollectionCheckRequired.None;
                case "Count":
                    return symbol.ContainingType.IsNonGenericType("Enumerable", "System", "Linq")
                        ? CollectionCheckRequired.NonKnownTypes
                        : CollectionCheckRequired.None;
                case "None":
                    return symbol.ContainingNamespace.GetTopMostNamespace().Name == "Octopus"
                        ? CollectionCheckRequired.NonKnownTypes
                        : CollectionCheckRequired.None;
                default:
                    return CollectionCheckRequired.None;
            }
        }

        static bool IsTheTargetCollectionAKnownTypeThatDoesNotCreateAnEnumerator(
            SyntaxNodeAnalysisContext context,
            InvocationExpressionSyntax invocation,
            CollectionCheckRequired checkRequired
        )
        {
            var targetCollectionExpression = invocation.ArgumentList.Arguments.Count == 0
                ? ((MemberAccessExpressionSyntax)invocation.Expression).Expression // Target of the extension method
                : invocation.ArgumentList.Arguments[0].Expression; // Statically called argument

            var targetCollectionTypeInfo = context.SemanticModel.GetTypeInfo(targetCollectionExpression);

            if (targetCollectionTypeInfo.Type is INamedTypeSymbol targetCollectionType)
            {
                if (IsIEnumerableOfT(targetCollectionType))
                    return true;

                if (checkRequired == CollectionCheckRequired.All)
                    return false;

                return IsThisTypeSpeciallyHandledByTheImplementation(targetCollectionType) ||
                    targetCollectionType.AllInterfaces.Any(IsThisTypeSpeciallyHandledByTheImplementation);
            }

            if (targetCollectionTypeInfo.Type is IArrayTypeSymbol)
                return checkRequired != CollectionCheckRequired.All;

            return false;
        }

        // The Enumerable.Any method will call Length or Count on certain interfaces bypassing the need to create an enumerator
        // See https://github.com/dotnet/runtime/blob/master/src/libraries/System.Linq/src/System/Linq/AnyAll.cs#L18
        // See https://github.com/dotnet/runtime/blob/master/src/libraries/System.Linq/src/System/Linq/Count.cs
        static bool IsThisTypeSpeciallyHandledByTheImplementation(INamedTypeSymbol type)
            => type.IsGenericType("ICollection", 1, "System", "Collections", "Generic") ||
                type.IsGenericType("IReadOnlyList", 1, "System", "Collections", "Generic") || // Not explicitly handled, but implementers typically also implement IListProvider
                type.IsGenericType("IReadOnlyCollection", 1, "System", "Collections", "Generic") || // Not explicitly handled, but implementers typically also implement IListProvider
                type.IsGenericType("IListProvider", 1, "System", "Linq") ||
                type.IsNonGenericType("ICollection", "System", "Collections");

        static bool IsIEnumerableOfT(INamedTypeSymbol type)
            => type.IsGenericType("IEnumerable", 1, "System", "Collections", "Generic");
    }
}