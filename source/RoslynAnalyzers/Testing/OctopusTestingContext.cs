using System;
using Microsoft.CodeAnalysis;

namespace Octopus.RoslynAnalyzers.Testing
{
    public class OctopusTestingContext
    {
        readonly Lazy<INamedTypeSymbol?> lazyFactAttributeType;
        readonly Lazy<INamedTypeSymbol?> lazyTheoryAttributeType;
        readonly Lazy<INamedTypeSymbol?> lazyIntegrationTestType;
        readonly Lazy<INamedTypeSymbol?> lazyUnitTestType;

        internal OctopusTestingContext(Compilation compilation)
        {
            Compilation = compilation;

            lazyFactAttributeType = new Lazy<INamedTypeSymbol?>(() => compilation.GetTypeByMetadataName(Constants.Types.XunitFact));
            lazyTheoryAttributeType = new Lazy<INamedTypeSymbol?>(() => compilation.GetTypeByMetadataName(Constants.Types.XunitTheory));
            lazyIntegrationTestType = new Lazy<INamedTypeSymbol?>(() => compilation.GetTypeByMetadataName(Constants.Types.IntegrationTest));
            lazyUnitTestType = new Lazy<INamedTypeSymbol?>(() => compilation.GetTypeByMetadataName(Constants.Types.UnitTest));
        }

        public Compilation Compilation { get; set; }

        public INamedTypeSymbol? FactAttributeType => lazyFactAttributeType.Value;
        public INamedTypeSymbol? TheoryAttributeType => lazyTheoryAttributeType.Value;
        public INamedTypeSymbol? IntegrationTestType => lazyIntegrationTestType.Value;
        public INamedTypeSymbol? UnitTestType => lazyUnitTestType.Value;
    }
}