using System;
using Microsoft.CodeAnalysis;

namespace Octopus.RoslynAnalyzers
{
    public static class SemanticModelExtensionMethods
    {
        public static bool IsNonGenericType(this INamedTypeSymbol type, string name, params string[] namespaceParts)
            => !type.IsGenericType &&
                type.Name == name &&
                type.IsInNamespace(namespaceParts);

        public static bool IsGenericType(this INamedTypeSymbol type, string name, int numberOfGenericParameters, params string[] namespaceParts)
            => type.IsGenericType &&
                type.TypeArguments.Length == numberOfGenericParameters &&
                type.Name == name &&
                type.IsInNamespace(namespaceParts);

        static bool IsInNamespace(this ITypeSymbol type, params string[] namespaceParts)
        {
            var ns = type.ContainingNamespace;
            for (var x = namespaceParts.Length - 1; x >= 0; x--)
            {
                if (ns?.Name != namespaceParts[x])
                    return false;
                ns = ns.ContainingNamespace;
            }

            return ns.IsGlobalNamespace;
        }

        public static INamespaceSymbol GetTopMostNamespace(this INamespaceSymbol ns)
            => ns.IsGlobalNamespace || ns.ContainingNamespace.IsGlobalNamespace
                ? ns
                : GetTopMostNamespace(ns.ContainingNamespace);
    }
}