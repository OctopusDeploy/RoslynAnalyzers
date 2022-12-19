using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Octopus.RoslynAnalyzers
{
    public static class AnalyzerExtensionMethods
    {
        public static bool ContainsTypesInheritingFromSpecifiedType(this ITypeSymbol sourceType, ITypeSymbol? baseType)
        {
            if (baseType is null)
            {
                return false;
            }

            return sourceType
                .GetMembers()
                .OfType<ITypeSymbol>()
                .Any(m => m.IsAssignableTo(baseType));
        }

        public static bool IsAssignableToButNotTheSame(this ITypeSymbol? sourceType, ITypeSymbol? targetType)
        {
            return !SymbolEqualityComparer.Default.Equals(sourceType, targetType) && sourceType.IsAssignableTo(targetType);
        }

        public static bool IsAssignableTo(this ITypeSymbol? sourceType, ITypeSymbol? targetType)
        {
            if (targetType != null)
            {
                while (sourceType != null)
                {
                    if (SymbolEqualityComparer.Default.Equals(sourceType, targetType))
                    {
                        return true;
                    }

                    if (targetType.TypeKind == TypeKind.Interface)
                    {
                        return sourceType.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, targetType));
                    }

                    sourceType = sourceType.BaseType;
                }
            }

            return false;
        }
        
        /// <summary>Returns true if sourceType inherits from baseType, walking down the type hierarchy as far as it can</summary>
        public static bool InheritsFrom(this ITypeSymbol? sourceType, ITypeSymbol? baseType)
        {
            if (sourceType?.BaseType is not { } candidate) return false;

            while (candidate != null)
            {
                if (SymbolEqualityComparer.Default.Equals(candidate, baseType)) return true;

                candidate = candidate.BaseType?.MetadataName == "Object" ? null : candidate.BaseType;
            }

            return false;
        }

        /// <summary>Returns true if sourceType directly inherits from baseType. Does not consider any kind of hierarchy</summary>
        public static bool DirectlyInheritsFrom(this ITypeSymbol? sourceType, ITypeSymbol? baseType)
        {
            return sourceType?.BaseType != null && SymbolEqualityComparer.Default.Equals(sourceType.BaseType, baseType);
        }

        public static bool IsDirectlyInNamespace(this ITypeSymbol type)
        {
            return type.ContainingType is null;
        }

        public static IEnumerable<T> GetAllMembersOfType<T>(this ITypeSymbol type) where T : ISymbol
        {
            return type.GetMembers().OfType<T>();
        }

        public static IEnumerable<ISymbol> ExceptOfType<T>(this IEnumerable<ISymbol> types) where T : ISymbol
        {
            var typesList = types.ToList();
            var membersOfType = typesList.OfType<T>().Cast<ISymbol>();
            return typesList.Except(membersOfType, SymbolEqualityComparer.Default);
        }

        public static IEnumerable<ISymbol> ExceptPropertyAccessors(this IEnumerable<ISymbol> symbols)
        {
            return symbols.Where(s =>
            {
                if (s is IMethodSymbol m)
                {
                    return m.MethodKind != MethodKind.PropertyGet && m.MethodKind != MethodKind.PropertySet;
                }

                return true;
            });
        }

        public static IEnumerable<ISymbol> ExceptImplicitlyDeclared(this IEnumerable<ISymbol> symbols)
        {
            return symbols.Where(s => !s.IsImplicitlyDeclared);
        }
    }
}