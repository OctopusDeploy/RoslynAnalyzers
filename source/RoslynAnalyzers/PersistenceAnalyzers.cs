using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers;

using static Descriptors;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class PersistenceAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        DontUseIDocumentStoreOfEvent);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        if (!Debugger.IsAttached) context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(DontUse_IDocumentStoreOfEvent, SyntaxKind.FieldDeclaration, SyntaxKind.PropertyDeclaration, SyntaxKind.ConstructorDeclaration);
    }

    // this flags declarations of fields or properties referencing IDocumentStore<Event, string>,
    // as well as constructor parameters (assuming someone is importing it from Autofac)
    // it doesn't flag on other things to reduce the effort required to check for this.
    void DontUse_IDocumentStoreOfEvent(SyntaxNodeAnalysisContext context)
    {
        static void CheckForIDocumentStoreOfEvent(SyntaxNodeAnalysisContext context, TypeSyntax typeToCheck)
        {
            GenericNameSyntax genericNameSyntax;
            switch (typeToCheck)
            {
                case GenericNameSyntax gns:
                    genericNameSyntax = gns;
                    break;
                case NullableTypeSyntax { ElementType: GenericNameSyntax gns }:
                    genericNameSyntax = gns;
                    break;
                default:
                    return;
            }

            if (genericNameSyntax.Identifier.Text == "IDocumentStore" && genericNameSyntax.TypeArgumentList.Arguments.Count == 2)
            {
                if (genericNameSyntax.TypeArgumentList.Arguments[0] is IdentifierNameSyntax i && i.Identifier.Text == "Event")
                {
                    context.ReportDiagnostic(Diagnostic.Create(DontUseIDocumentStoreOfEvent, genericNameSyntax.GetLocation()));
                }
            }
        }
        
        ;
        switch (context.Node)
        {
            case PropertyDeclarationSyntax propDec:
                CheckForIDocumentStoreOfEvent(context, propDec.Type);
                break;
            case FieldDeclarationSyntax fieldDec:
                CheckForIDocumentStoreOfEvent(context, fieldDec.Declaration.Type);
                break;
            case ConstructorDeclarationSyntax ctorDec:
                foreach (var p in ctorDec.ParameterList.Parameters)
                {
                    if(p.Type is { } typeToCheck) CheckForIDocumentStoreOfEvent(context, typeToCheck);
                }
                break;
            default:
                return;
        }

        
    }
}