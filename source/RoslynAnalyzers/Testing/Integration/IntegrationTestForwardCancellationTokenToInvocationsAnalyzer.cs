using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Octopus.RoslynAnalyzers.Testing.Integration
{
    /// <summary>
    /// Specific analyzer for Octopus Integration Test container classes.
    /// This will show "information" hint when method call can forward CancellationToken, but hasn't.
    /// For Octopus Integration Test, CancellationToken is defined in the base class.
    /// The existing dotnet analyzer, CA2016, handle cases where CancellationToken is found on the method context / containing class, but
    /// not when definition is in parent class. (IE: IntegrationTest)
    /// This implementation is based (heavily) on CA2016 implementation. (https://github.com/dotnet/roslyn-analyzers/pull/3641)
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class IntegrationTestForwardCancellationTokenToInvocationsAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            Descriptors.Oct2008IntegrationTestForwardCancellationTokenToInvocations()
        );

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            analysisContext.EnableConcurrentExecution();
            analysisContext.RegisterCompilationStartAction(AnalyzeCompilationStart);
        }

        void AnalyzeCompilationStart(CompilationStartAnalysisContext compilationStartAnalysisContext)
        {
            compilationStartAnalysisContext.RegisterOperationAction(
                context =>
                {
                    if (!IsOperationOfInterest(context))
                    {
                        return;
                    }

                    var (cancellationTokenType, genericTaskType, genericValueTaskType) = GetWellKnownTypes(context);
                    if (cancellationTokenType == null)
                    {
                        // Only ct type matters, for the generic type it is ok to be null (edge case).
                        return;
                    }

                    var invocation = (IInvocationOperation)context.Operation;
                    var method = invocation.TargetMethod;

                    // Verify that the current invocation is not passing an explicit token already
                    if (IsCancellationTokenAlreadyForwarded(invocation.Arguments, cancellationTokenType))
                    {
                        return;
                    }

                    // Verify method has optional ct, warn user to be explicit rather than relying on default.
                    if (InvocationMethodTakesAToken(method, invocation.Arguments, cancellationTokenType) ||

                        // Verify method has ct overload as last parameter.
                        InvocationMethodHasCancellationTokenOverload(method, cancellationTokenType, genericTaskType, genericValueTaskType))
                    {
                        SyntaxNode? nodeToDiagnose = GetInvocationMethodNameNode(context.Operation.Syntax) ?? context.Operation.Syntax;
                        context.ReportDiagnostic(Diagnostic.Create(Descriptors.Oct2008IntegrationTestForwardCancellationTokenToInvocations(), nodeToDiagnose.GetLocation()));
                    }
                },
                OperationKind.Invocation
            );
        }

        static bool IsOperationOfInterest(OperationAnalysisContext context)
        {
            return context.ContainingSymbol is IMethodSymbol containingMethod &&

                // Only interested in IntegrationTestType classes.
                containingMethod.ContainingType.IsAssignableTo(new OctopusTestingContext(context.Compilation).IntegrationTestType) &&

                // No diagnostic on static method, this is handled by CA2016 and Octopus async-cancellation-token convention.
                !containingMethod.IsStatic;
        }

        static (INamedTypeSymbol? cancellationTokenType, INamedTypeSymbol? genericTaskType, INamedTypeSymbol? genericValueTaskType) GetWellKnownTypes(OperationAnalysisContext context)
        {
            INamedTypeSymbol? cancellationTokenType = context.Compilation.GetTypeByMetadataName(Constants.Types.SystemThreadingCancellationToken);
            INamedTypeSymbol? genericTaskType = context.Compilation.GetTypeByMetadataName(Constants.Types.SystemThreadingTasksTask1);
            INamedTypeSymbol? genericValueTaskType = context.Compilation.GetTypeByMetadataName(Constants.Types.SystemThreadingTasksValueTask1);

            return (cancellationTokenType, genericTaskType, genericValueTaskType);
        }

        static bool AnyArgument(ImmutableArray<IArgumentOperation> arguments, Func<IArgumentOperation, bool> predicate)
        {
            for (int i = arguments.Length - 1; i >= 0; i--)
            {
                if (predicate(arguments[i]))
                {
                    return true;
                }
            }

            return false;
        }

        static bool IsCancellationTokenAlreadyForwarded(ImmutableArray<IArgumentOperation> invocationArguments, INamedTypeSymbol? cancellationTokenType)
        {
            // Scanning argument for explicitly declared cancellation token type. We want to show diagnostic for implicit (default / optional) case.
            return AnyArgument(invocationArguments, argument => SymbolEqualityComparer.Default.Equals(argument.Parameter.Type, cancellationTokenType) && !argument.IsImplicit);
        }

        // Checks if the invocation has an optional ct argument at the end.
        static bool InvocationMethodTakesAToken(IMethodSymbol method, ImmutableArray<IArgumentOperation> arguments, ISymbol cancellationTokenType)
        {
            return
                !method.Parameters.IsEmpty &&
                method.Parameters[method.Parameters.Length - 1] is { } lastParameter &&
                InvocationIgnoresOptionalCancellationToken(lastParameter, arguments, cancellationTokenType);
        }

        // Check if the currently used overload is the one that takes the ct, but is utilizing the default value offered in the method signature.
        // We want to offer a diagnostic for this case, so the user explicitly passes the ancestor's ct.
        static bool InvocationIgnoresOptionalCancellationToken(
            IParameterSymbol parameter,
            ImmutableArray<IArgumentOperation> arguments,
            ISymbol cancellationTokenType)
        {
            if (SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationTokenType) &&
                parameter.IsOptional) // Has a default value being used
            {
                // Find out if the ct argument is using the default value
                // Need to check among all arguments in case the user is passing them named and unordered (despite the ct being defined as the last parameter)
                return AnyArgument(
                    arguments,
                    a => SymbolEqualityComparer.Default.Equals(a.Parameter.Type, cancellationTokenType) && a.ArgumentKind == ArgumentKind.DefaultValue
                );
            }

            return false;
        }

        // Check if there's a method overload with the same parameters as this one, in the same order, plus a ct at the end.
        static bool InvocationMethodHasCancellationTokenOverload(
            IMethodSymbol method,
            ISymbol cancellationTokenType,
            INamedTypeSymbol? genericTask,
            INamedTypeSymbol? genericValueTask)
        {
            var overload = method.ContainingType
                .GetMembers(method.Name)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(
                    methodToCompare => HasSameParametersPlusCancellationToken(
                        cancellationTokenType,
                        genericTask,
                        genericValueTask,
                        method,
                        methodToCompare
                    )
                );

            return overload != null;

            // Checks if the parameters of the two passed methods only differ in a ct.
            static bool HasSameParametersPlusCancellationToken(
                ISymbol cancellationTokenType,
                INamedTypeSymbol? genericTask,
                INamedTypeSymbol? genericValueTask,
                IMethodSymbol originalMethod,
                IMethodSymbol methodToCompare)
            {
                // Avoid comparing to itself, or when there is no ct parameter, or when the last parameter is not a ct
                if (SymbolEqualityComparer.Default.Equals(originalMethod, methodToCompare) ||
                    methodToCompare.Parameters.Count(p => SymbolEqualityComparer.Default.Equals(p.Type, cancellationTokenType)) != 1 ||
                    !SymbolEqualityComparer.Default.Equals(methodToCompare.Parameters[methodToCompare.Parameters.Length - 1].Type, cancellationTokenType))
                {
                    return false;
                }

                IMethodSymbol originalMethodWithAllParameters = (originalMethod.ReducedFrom ?? originalMethod).OriginalDefinition;
                IMethodSymbol methodToCompareWithAllParameters = (methodToCompare.ReducedFrom ?? methodToCompare).OriginalDefinition;

                // Ensure parameters only differ by one - the ct
                if (originalMethodWithAllParameters.Parameters.Length != methodToCompareWithAllParameters.Parameters.Length - 1)
                {
                    return false;
                }

                // Now compare the types of all parameters before the ct
                // The largest i is the number of parameters in the method that has fewer parameters
                for (int i = 0; i < originalMethodWithAllParameters.Parameters.Length; i++)
                {
                    IParameterSymbol originalParameter = originalMethodWithAllParameters.Parameters[i];
                    IParameterSymbol comparedParameter = methodToCompareWithAllParameters.Parameters[i];
                    if (!SymbolEqualityComparer.Default.Equals(originalParameter.Type, comparedParameter.Type))
                    {
                        return false;
                    }
                }

                // Overload is valid if its return type is implicitly convertable
                var toCompareReturnType = methodToCompareWithAllParameters.ReturnType;
                var originalReturnType = originalMethodWithAllParameters.ReturnType;
                if (!toCompareReturnType.IsAssignableTo(originalReturnType))
                {
                    // Generic Task-like types are special since awaiting them essentially erases the task-like type.
                    // If both types are Task-like we will warn if their generic arguments are convertable to each other.
                    if (IsTaskLikeType(originalReturnType) && IsTaskLikeType(toCompareReturnType) &&
                        originalReturnType is INamedTypeSymbol originalNamedType &&
                        toCompareReturnType is INamedTypeSymbol toCompareNamedType &&
                        TypeArgumentsAreConvertable(originalNamedType, toCompareNamedType))
                    {
                        return true;
                    }

                    return false;
                }

                return true;

                bool IsTaskLikeType(ITypeSymbol typeSymbol)
                {
                    if (genericTask != null &&
                        SymbolEqualityComparer.Default.Equals(typeSymbol.OriginalDefinition, genericTask))
                    {
                        return true;
                    }

                    if (genericValueTask != null &&
                        SymbolEqualityComparer.Default.Equals(typeSymbol.OriginalDefinition, genericValueTask))
                    {
                        return true;
                    }

                    return false;
                }

                bool TypeArgumentsAreConvertable(INamedTypeSymbol left, INamedTypeSymbol right)
                {
                    // left and right return type should be consistent.
                    if (left.Arity != 1 ||
                        right.Arity != 1 ||
                        left.Arity != right.Arity)
                    {
                        return false;
                    }

                    var leftTypeArgument = left.TypeArguments.FirstOrDefault();
                    var rightTypeArgument = right.TypeArguments.FirstOrDefault();
                    return leftTypeArgument != null && rightTypeArgument != null && leftTypeArgument.GetType().IsInstanceOfType(rightTypeArgument);
                }
            }
        }

        static SyntaxNode? GetInvocationMethodNameNode(SyntaxNode invocationNode)
        {
            if (!(invocationNode is InvocationExpressionSyntax invocationExpression))
                return null;

            if (invocationExpression.Expression is MemberBindingExpressionSyntax memberBindingExpression)
            {
                // When using nullability features, specifically attempting to dereference possible null references,
                // the dot becomes part of the member invocation expression, so we need to return just the name,
                // so that the diagnostic gets properly returned in the method name only.
                return memberBindingExpression.Name;
            }

            return invocationExpression.Expression;
        }
    }
}