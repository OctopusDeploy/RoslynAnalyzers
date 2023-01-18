using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ResourceGettingInEventualHandlersAnalyzer : DiagnosticAnalyzer
{
    internal static readonly DiagnosticDescriptor Rule = new(
        id: "Octopus_ResourceGettingInEventualHandlers",
        title: "Eventual handler should not assume resource exists",
        description: "This implementation of `IEventualHandler<TEvent>.Handle` assumes a resource identified by the `TEvent` event to exist. Either catch the possible `EntityNotFoundException` that `IReadOnlyDocumentStore.Get` can throw, or call `IReadOnlyDocumentStore.GetOrNull` and handle the possible `null` return value.",
        messageFormat: "This implementation of `IEventualHandler<TEvent>.Handle` assumes a resource identified by the `TEvent` event to exist. Either catch the possible `EntityNotFoundException` that `IReadOnlyDocumentStore.Get` can throw, or call `IReadOnlyDocumentStore.GetOrNull` and handle the possible `null` return value.",
        category: "Octopus",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(CheckForAssumptionsThatResourceExists, SyntaxKind.ClassDeclaration);
    }

    static void CheckForAssumptionsThatResourceExists(SyntaxNodeAnalysisContext context)
    {
        foreach (var violation in FindViolations(context))
        {
            context.ReportDiagnostic(From(violation));
        }
    }

    static IEnumerable<MemberAccessExpressionSyntax> FindViolations(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDeclaration || ClassIsNotEventualHandler(classDeclaration))
        {
            return Enumerable.Empty<MemberAccessExpressionSyntax>();
        }

        var handlerMethod = FindHandlerMethod(classDeclaration);

        var invocationsOfGet = handlerMethod == null
            ? Enumerable.Empty<MemberAccessExpressionSyntax>()
            : InvocationsOfGet(handlerMethod);

        return invocationsOfGet
            .Where(SymbolShowsInvocationIsOnIReadOnlyDocumentStore(context))
            .Where(PossibleEntityNotFoundExceptionIsUnhandled);
    }

    static bool ClassIsNotEventualHandler(ClassDeclarationSyntax @class) =>
        @class.BaseList?.Types
            .Select(t => t.Type)
            .OfType<GenericNameSyntax>()
            .All(type => type.Identifier.ValueText != "IEventuallyHandleEvent")
        ?? true;

    static MethodDeclarationSyntax? FindHandlerMethod(ClassDeclarationSyntax @class) =>
        @class.Members
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(method => method.Identifier.ValueText == "Handle");

    static IEnumerable<MemberAccessExpressionSyntax> InvocationsOfGet(MethodDeclarationSyntax method) =>
        method.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(access => access.Name.Identifier.ValueText == "Get");

    static Func<MemberAccessExpressionSyntax, bool> SymbolShowsInvocationIsOnIReadOnlyDocumentStore(SyntaxNodeAnalysisContext context) =>
        invocation =>
        {
            var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol;
            return symbol is not null && InvocationIsOnIReadOnlyDocumentStore(symbol);
        };

    static bool InvocationIsOnIReadOnlyDocumentStore(ISymbol invocationSymbol) =>
        invocationSymbol.ContainingType.Name == "IReadOnlyDocumentStore" ||
        invocationSymbol.ContainingType.AllInterfaces.Any(@interface => @interface.Name == "IReadOnlyDocumentStore");

    static bool PossibleEntityNotFoundExceptionIsUnhandled(MemberAccessExpressionSyntax invocation)
    {
        var tryStatement = invocation.FirstAncestorOrSelf<TryStatementSyntax>();
        return tryStatement?.Catches
                .Select(@catch => @catch.Declaration?.Type)
                .OfType<IdentifierNameSyntax>()
                .All(name => name.Identifier.ValueText != "EntityNotFoundException")
            ?? true;
    }

    static Diagnostic From(MemberAccessExpressionSyntax violation) =>
        Diagnostic.Create(Rule, violation.Name.GetLocation());
}
