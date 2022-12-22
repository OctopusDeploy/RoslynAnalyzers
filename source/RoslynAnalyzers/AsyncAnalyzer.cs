using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers;

using static Descriptors;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AsyncAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        MethodsReturningTaskMustBeAsync);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(CheckNode, SyntaxKind.MethodDeclaration);
    }

    void CheckNode(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodDec) return;

        var isAsync = methodDec.Modifiers.Any(SyntaxKind.AsyncKeyword);
        if (isAsync)
        {
            // future analyzers targeting async methods go here
            // such as AsyncMethodsMustNotHaveAsyncSuffix and AsyncMethodsMustTakeACancellationToken
        }
        else
        {
            MethodsReturningTask_MustBeAsync(context, methodDec);
        }
    }

    void MethodsReturningTask_MustBeAsync(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDec)
    {
        // don't flag things that don't return tasks
        if (methodDec.ReturnType is not SimpleNameSyntax { Identifier.Text: "Task" or "ValueTask" }) return;
        
        // don't flag abstract methods. Like interfaces they can't have the async keyword attached to them.
        if (methodDec.Modifiers.Any(SyntaxKind.AbstractKeyword)) return;

        var declaringType = methodDec.GetDeclaringType();

        if (declaringType is InterfaceDeclarationSyntax) return; // don't flag on interfaces, they can't have the async keyword attached to them.

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