using System;
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
        if (context.Node is not ClassDeclarationSyntax classDeclaration)
        {
            return;
        }

        if (!ClassIsEventualHandler(classDeclaration))
        {
            return;
        }

        var handlerMethod = classDeclaration.Members
            .OfType<MethodDeclarationSyntax>()
            .First(method => method.Identifier.ValueText == "Handle");

        var invocationsOfGet = handlerMethod.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Where(access => access.Name.Identifier.ValueText == "Get");

        foreach (var invocation in invocationsOfGet)
        {
            var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol;
            var isInvokedOnReadOnlyDocumentStore = symbol?.ContainingType.Name == "IReadOnlyDocumentStore";
            isInvokedOnReadOnlyDocumentStore = isInvokedOnReadOnlyDocumentStore || (symbol?.ContainingType.AllInterfaces.Any(@interface => @interface.Name == "IReadOnlyDocumentStore") ?? false);

            if (isInvokedOnReadOnlyDocumentStore)
            {
                var tryStatement = invocation.FirstAncestorOrSelf<TryStatementSyntax>();

                var entityNotFoundExceptionIsUnhandled = tryStatement?.Catches.Select(@catch => @catch.Declaration?.Type).OfType<IdentifierNameSyntax>().All(name => name.Identifier.ValueText != "EntityNotFoundException") ?? true;

                if (entityNotFoundExceptionIsUnhandled)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Name.GetLocation()));
                }
            }
        }
    }

    static bool ClassIsEventualHandler(ClassDeclarationSyntax @class)
    {
        return @class.BaseList?.Types.Select(t => t.Type).OfType<GenericNameSyntax>().Any(type => type.Identifier.ValueText == "IEventuallyHandleEvent") ?? false;
    }
}
