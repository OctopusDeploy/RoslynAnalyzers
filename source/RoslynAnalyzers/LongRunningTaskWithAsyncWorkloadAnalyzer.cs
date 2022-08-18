using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Octopus.RoslynAnalyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class LongRunningTaskWithAsyncWorkloadAnalyzer : DiagnosticAnalyzer
    {
        const string DiagnosticId = "Octopus_LongRunningTaskWithAsyncWorkload";
        const string Category = "Octopus";
        const string Title = "Creating long-running Tasks with an async workload";
        const string Description = "Creating Tasks the LongRunning otions and an async workload can result in a new thread being created to run the task, which may exit unexpectedly. This can result in difficult to diagnose bugs. Use Task.Run instead.";
        const string MessageFormat = "Creating long-running Tasks with an async workload can result in unexpected thread exits. Use Task.Run instead.";

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Error,
            true,
            Description);

        const int LongRunningFlag = 2;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(CheckForLongRunningTaskWithAsyncWorkload, SyntaxKind.InvocationExpression);
        }

        void CheckForLongRunningTaskWithAsyncWorkload(SyntaxNodeAnalysisContext context)
        {
            if (context.Compilation == null)
                return;

            var invocationNode = (InvocationExpressionSyntax)context.Node;

            if (!(invocationNode.Expression is MemberAccessExpressionSyntax memberAccessExpression) ||
                memberAccessExpression.Name.ToString() != nameof(Task.Factory.StartNew))
            {
                return;
            }

            if (!(context.SemanticModel.GetSymbolInfo(memberAccessExpression).Symbol is IMethodSymbol calledSymbol))
            {
                return;
            }

            var taskFactorySymbol = context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.TaskFactory")!;
            var taskCreationOptionsType = context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.TaskCreationOptions")!;
            var startNewMethods = from method in taskFactorySymbol.GetMembers(nameof(Task.Factory.StartNew)).OfType<IMethodSymbol>()
                let optionsParam = method.Parameters.SingleOrDefault(p => p.Type.Equals(taskCreationOptionsType, SymbolEqualityComparer.Default))
                where optionsParam != null && method.IsGenericMethod
                select (method, method.Parameters.IndexOf(optionsParam));
            var (calledMethod, optionsParamPos) = startNewMethods.FirstOrDefault(m => m.method.Equals(calledSymbol.ConstructedFrom, SymbolEqualityComparer.Default));
            if (calledMethod == null)
            {
                return;
            }

            var argumentSyntax = invocationNode.ArgumentList.Arguments[optionsParamPos];
            var argumentSymbol = context.SemanticModel.GetSymbolInfo(argumentSyntax.Expression).Symbol;
            if (!IsLongRunningFlagSpecified(argumentSyntax, argumentSymbol))
            {
                return;
            }

            var workloadExpression = invocationNode.ArgumentList.Arguments[0].Expression;
            if (context.SemanticModel.GetSymbolInfo(workloadExpression).Symbol is IMethodSymbol workloadSymbol && ReturnsATask(workloadSymbol))
            {
                var diagnostic = Diagnostic.Create(Rule, context.Node.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }

            static bool IsLongRunningFlagSpecified(ArgumentSyntax argumentSyntax, ISymbol? argumentSymbol)
            {
                if (argumentSymbol is ILocalSymbol { HasConstantValue: true } localSymbol)
                    return ((int)(localSymbol.ConstantValue ?? 0) & LongRunningFlag) != 0;

                return argumentSyntax.Expression.ToString().Contains("LongRunning");
            }

            bool ReturnsATask(IMethodSymbol methodSymbol)
            {
                var taskSymbol = context.Compilation!.GetTypeByMetadataName("System.Threading.Tasks.Task");
                var taskOfTSymbol = context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");

                return methodSymbol.ReturnType.Equals(taskSymbol, SymbolEqualityComparer.IncludeNullability) ||
                    methodSymbol.ReturnType.Equals(taskOfTSymbol, SymbolEqualityComparer.IncludeNullability);
            }
        }
    }
}