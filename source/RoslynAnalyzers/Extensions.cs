using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Octopus.RoslynAnalyzers
{
    public static class SemanticModelExtensions
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
    
    public static class SyntaxModelExtensions
    {

        /// <summary>
        /// Walks up the syntax tree until it reaches a namespace at the top. Returns emptystring if one cannot be found
        /// </summary>
        /// <remarks>
        /// Adapted from https://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/
        /// </remarks>
        public static string GetNamespace(this SyntaxNode syntax)
        {
            // If we don't have a namespace at all we'll return an empty string
            // This accounts for the "default namespace" case
            var nameSpace = string.Empty;

            // Get the containing syntax node for the type declaration
            // (could be a nested type, for example)
            var potentialNamespaceParent = syntax.Parent;
    
            // Keep moving "out" of nested classes etc until we get to a namespace
            // or until we run out of parents
            while (potentialNamespaceParent != null &&
                   potentialNamespaceParent is not NamespaceDeclarationSyntax
                   && potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
            {
                potentialNamespaceParent = potentialNamespaceParent.Parent;
            }

            // Build up the final namespace by looping until we no longer have a namespace declaration
            if (potentialNamespaceParent is not BaseNamespaceDeclarationSyntax namespaceParent)
                return nameSpace;
            
            // We have a namespace. Use that as the type
            nameSpace = namespaceParent.Name.ToString();
        
            // Keep moving "out" of the namespace declarations until we 
            // run out of nested namespace declarations
            while (true)
            {
                if (namespaceParent.Parent is not NamespaceDeclarationSyntax parent) break;

                // Add the outer namespace as a prefix to the final namespace
                nameSpace = $"{namespaceParent.Name}.{nameSpace}";
                namespaceParent = parent;
            }

            // return the final namespace
            return nameSpace;
        }
    }
}