using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers.Testing
{
    public abstract class OctopusTestingDiagnosticAnalyzer<T> : DiagnosticAnalyzer where T : ISymbol
    {
        static readonly Dictionary<Type, SymbolKind> SymbolKindMap = new Dictionary<Type, SymbolKind>
        {
            { typeof(INamedTypeSymbol), SymbolKind.NamedType }
        };

        public sealed override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            analysisContext.EnableConcurrentExecution();
            analysisContext.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                var octopusTestingContext = new OctopusTestingContext(compilationStartAnalysisContext.Compilation);
                var symbolKind = SymbolKindMap[typeof(T)];

                compilationStartAnalysisContext.RegisterSymbolAction(context =>
                    {
                        var typedSymbol = (T)context.Symbol;
                        AnalyzeCompilation(typedSymbol, context, octopusTestingContext);
                    },
                    symbolKind);
            });
        }

        internal abstract void AnalyzeCompilation(T symbol, SymbolAnalysisContext context, OctopusTestingContext octopusTestingContext);
    }
}