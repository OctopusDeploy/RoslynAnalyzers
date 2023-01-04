using System.Collections.Generic;
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
public class ApiControllerAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        MustNotHaveSwaggerOperationAttribute,
        MustNotReturnActionResults);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        if(!Debugger.IsAttached) context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(CheckNode, SyntaxKind.ClassDeclaration);
    }

    void CheckNode(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ClassDeclarationSyntax classDec || !IsApiController(context, classDec)) return;
  
        var attrNames = classDec.AttributeLists.SelectMany(al => al.Attributes.Select(a => (a.Name as IdentifierNameSyntax)?.Identifier.Text));
        var isExperimental = attrNames.Any(a => a == "Experimental");

        var actionMethods = new List<MethodDeclarationSyntax>();
        foreach (var methodDec in classDec.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            // only validate action methods
            if(!IsActionMethod(context, methodDec)) continue;
            actionMethods.Add(methodDec);

            if (!isExperimental)
            {
                MustNot_HaveSwaggerOperationAttribute(context, methodDec);
            }

            MustNot_ReturnActionResults(context, methodDec);
        }

        // ControllersAcceptingNewPayloads_MustBeNamedCorrectly();
    }

    static bool MustNot_HaveSwaggerOperationAttribute(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDec)
    {
        var swaggerOperationAttr = methodDec.AttributeLists.SelectMany(al => al.Attributes).FirstOrDefault(al => al.Name is IdentifierNameSyntax { Identifier.Text: "SwaggerOperation" });
        if (swaggerOperationAttr != null)
        {
            context.ReportDiagnostic(Diagnostic.Create(MustNotHaveSwaggerOperationAttribute,
                location: swaggerOperationAttr.GetLocation()));
            return false;
        }

        return true;
    }
    
    static bool MustNot_ReturnActionResults(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDec)
    {
        var returnType = context.SemanticModel.GetTypeInfo(methodDec.ReturnType);
        if (returnType.Type is not INamedTypeSymbol namedTypeSymbol) return true; // not something we care about

        var typeSymbol = namedTypeSymbol.UnwrapTaskOf();

        var actionResultType = context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.IActionResult");
        var convertToActionResultType = context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.Infrastructure.IConvertToActionResult");

        var symCmp = SymbolEqualityComparer.Default;
        if (symCmp.Equals(typeSymbol, actionResultType) || symCmp.Equals(typeSymbol, convertToActionResultType))
        {
            context.ReportDiagnostic(Diagnostic.Create(MustNotReturnActionResults, location: methodDec.ReturnType.GetLocation()));
            return false;
        }
        
        // if they didn't directly return IActionResult, then check the interface hierarchy
        if (typeSymbol.AllInterfaces.Any(a => symCmp.Equals(a, actionResultType) || symCmp.Equals(a, convertToActionResultType)))
        {
            // exemptions for specific types
            if (symCmp.Equals(typeSymbol, context.Compilation.GetTypeByMetadataName("Octopus.Server.Web.Infrastructure.BlobResult"))) return true;
            if (symCmp.Equals(typeSymbol, context.Compilation.GetTypeByMetadataName("Octopus.Server.Web.Infrastructure.CsvFileResult"))) return true;
            if (symCmp.Equals(typeSymbol, context.Compilation.GetTypeByMetadataName("Octopus.Server.Web.Controllers.Telemetry.AmplitudeOkResult"))) return true;
         
            // FileResult plus derived classes FileContentResult and FileStreamResult
            var fileResult = context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.FileResult");
            if (symCmp.Equals(typeSymbol, fileResult) || typeSymbol.InheritsFrom(fileResult)) return true;
            
            // only CreatedResult<T> is allowed, the non-generic one isn't
            if (symCmp.Equals(typeSymbol.OriginalDefinition, context.Compilation.GetTypeByMetadataName("Octopus.Server.Extensibility.Web.Extensions.CreatedResult`1"))) return true; 
            
            context.ReportDiagnostic(Diagnostic.Create(MustNotReturnActionResults, location: methodDec.ReturnType.GetLocation()));
            return false;
        }
        
        return true;
    }

    /// <summary>
    /// Returns a guess at whether a method on a controller is an MVC action method.
    /// </summary>
    static bool IsActionMethod(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax methodDec)
    {
        var isPublic = false;
        foreach (var s in methodDec.Modifiers)
        {
            if (s.IsKind(SyntaxKind.PublicKeyword)) isPublic = true;

            if (s.IsKind(SyntaxKind.StaticKeyword)) return false;
            if (s.IsKind(SyntaxKind.AbstractKeyword)) return false;
            if (s.IsKind(SyntaxKind.ConstructorDeclaration)) return false;
        }

        if (!isPublic) return false;

        var isObviouslyActionMethod = false;
        foreach (var attr in methodDec.AttributeLists.SelectMany(al => al.Attributes))
        {
            if(attr.Name is not IdentifierNameSyntax { Identifier.Text: var identifierText }) continue;

            switch (identifierText)
            {
                case "Route":
                case "HttpGet":
                case "HttpPost":
                case "HttpPut":
                case "HttpDelete":
                    isObviouslyActionMethod = true;
                    break;
            }
        }
        
        // TODO someone could have made a custom subclass of HttpGet, we'd need to walk the inheritance tree here to catch it.
        // TODO that work goes here later.

        return isObviouslyActionMethod;
    }

    static bool IsApiController(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDec)
    {
        var isPublic = false;
        foreach (var s in classDec.Modifiers)
        {
            if (s.IsKind(SyntaxKind.PublicKeyword)) isPublic = true;

            if (s.IsKind(SyntaxKind.StaticKeyword)) return false;
            if (s.IsKind(SyntaxKind.AbstractKeyword)) return false;
        }
        if(!isPublic) return false;
        
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDec);
        var controllerBaseType = context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ControllerBase");
        return symbol.InheritsFrom(controllerBaseType);
    }
}