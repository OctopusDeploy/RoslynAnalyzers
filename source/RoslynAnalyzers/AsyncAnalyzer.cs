using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers;

using static Octopus.RoslynAnalyzers.Descriptors;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AsyncAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        MethodsReturningTaskMustBeAsync);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        
        if(!Debugger.IsAttached) context.EnableConcurrentExecution();
        
        context.RegisterSyntaxNodeAction(CheckNode, SyntaxKind.MethodDeclaration);
    }

    void CheckNode(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodDec) return;

        if (methodDec.ReturnType is not SimpleNameSyntax { Identifier.Text: "Task" or "ValueTask" })
        {
            // if it doesn't return Task or ValueTask then we don't care about it
            return;
        }

        // don't flag things that are async (that's good!) or abstract methods.
        if (methodDec.Modifiers.Any(SyntaxKind.AsyncKeyword) || methodDec.Modifiers.Any(SyntaxKind.AbstractKeyword)) return;
            
        var declaringType = methodDec.Parent;
        while (declaringType is not null && declaringType is not TypeDeclarationSyntax) declaringType = declaringType.Parent;

        if (declaringType is InterfaceDeclarationSyntax) return; // don't flag on interfaces
        
        // exemption for classes implementing IAsyncApiAction
        if (declaringType is ClassDeclarationSyntax classDec)
        {
            var baseTypeList = classDec.BaseList?.ChildNodes().OfType<SimpleBaseTypeSyntax>().SelectMany(baseTypeDec => baseTypeDec.ChildNodes());
            if (baseTypeList != null && baseTypeList.Any(b => b is IdentifierNameSyntax { Identifier.Text: "IAsyncApiAction" })) return;
        }
        
        // exemption for things in Octopus.Server.Extensibility. Note that the reflection based test does this by seeing if the Assembly Name
        // has Octopus.Server.Extensibility somewhere in it. We can't do that so easily; use namespace as an approximation (it wasn't exact anyway)

        if (declaringType != null && declaringType.GetNamespace().Contains("Octopus.Server.Extensibility")) return;
        
        context.ReportDiagnostic(Diagnostic.Create(MethodsReturningTaskMustBeAsync, methodDec.Identifier.GetLocation()));
    }
}