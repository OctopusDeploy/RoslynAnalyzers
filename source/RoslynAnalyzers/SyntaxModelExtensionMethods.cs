using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Octopus.RoslynAnalyzers
{
    public static class SyntaxModelExtensionMethods
    {
        /// <summary>
        /// Walks up the syntax tree until it reaches a TypeDeclaration. Returns null if one cannot be found
        /// </summary>
        /// <param name="methodDec"></param>
        /// <returns></returns>
        public static TypeDeclarationSyntax? GetDeclaringType(this MethodDeclarationSyntax methodDec)
        {
            var parent = methodDec.Parent;
            while (parent is not null && parent is not TypeDeclarationSyntax)
            {
                parent = parent.Parent;
            }

            return parent as TypeDeclarationSyntax;
        }
        
        /// <summary>
        /// Walks up the syntax tree until it reaches a namespace at the top. Returns empty string if one cannot be found
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

            // Keep moving "out" of the namespace declarations until we run out of nested namespace declarations
            var parent = namespaceParent.Parent as NamespaceDeclarationSyntax;
            while (parent != null)
            {
                // Add the outer namespace as a prefix to the final namespace
                nameSpace = $"{parent.Name}.{nameSpace}";
                parent = parent.Parent as NamespaceDeclarationSyntax;
            }

            // return the final namespace
            return nameSpace;
        }
    }
}