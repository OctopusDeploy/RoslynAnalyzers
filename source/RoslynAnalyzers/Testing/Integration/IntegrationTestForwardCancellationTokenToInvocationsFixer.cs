using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Octopus.RoslynAnalyzers.Testing.Integration
{
    /// <summary>
    /// Provides light-bulb hint to fix diagnostic reported from <see cref="IntegrationTestForwardCancellationTokenToInvocationsAnalyzer"/>.
    /// </summary>
    /// <remarks>
    /// This implementation is based (heavily) on CA2016 implementation. (https://github.com/dotnet/roslyn-analyzers/pull/3641)
    /// </remarks>
    [ExportCodeFixProvider(LanguageNames.CSharp)]
    public sealed class IntegrationTestForwardCancellationTokenToInvocationsFixer : CodeFixProvider
    {
        const string CancellationTokenPropertiesName = "CancellationToken";

        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            Descriptors.Oct2008IntegrationTestForwardCancellationTokenToInvocations().Id
        );

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var contextDocument = context.Document;
            var cancellationToken = context.CancellationToken;
            var contextRootNode = await contextDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            if (contextRootNode is null)
            {
                return;
            }

            var invocationOperation = await GetInvocationOperation(contextDocument, contextRootNode, context.Span, cancellationToken);

            if (invocationOperation is null)
            {
                return;
            }

            var (invocationSyntaxExpression, invocationSyntaxArguments) = GetExpressionAndArguments(invocationOperation.Syntax);

            if (invocationSyntaxExpression is null)
            {
                return;
            }

            var properties = context.Diagnostics[0].Properties;

            if (!properties.TryGetValue(IntegrationTestForwardCancellationTokenToInvocationsAnalyzer.OfferDiagnosticFix, out var offerDiagnosticFix)
                || bool.FalseString.Equals(offerDiagnosticFix))
            {
                return;
            }

            Task<Document> CreateChangedDocument(CancellationToken _)
            {
                var newRoot = UpdatedRootWithCancellationTokenSyntax(
                    contextDocument,
                    contextRootNode,
                    invocationOperation,
                    invocationSyntaxExpression,
                    invocationSyntaxArguments
                );
                var newDocument = contextDocument.WithSyntaxRoot(newRoot);

                return Task.FromResult(newDocument);
            }

            var title = Descriptors.Oct2008IntegrationTestForwardCancellationTokenToInvocations().Title.ToString();
            context.RegisterCodeFix(CodeAction.Create(title, CreateChangedDocument, equivalenceKey: title), context.Diagnostics);
        }

        static async Task<IInvocationOperation?> GetInvocationOperation(Document document, SyntaxNode rootNode, TextSpan span, CancellationToken cancellationToken)
        {
            IInvocationOperation? invocationOperation = null;
            var offendingNode = rootNode.FindNode(span, getInnermostNodeForTie: true);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            if (semanticModel == null)
            {
                return invocationOperation;
            }

            // The analyzer created the diagnostic on the IdentifierNameSyntax, and the parent is the actual invocation
            if (offendingNode.Parent != null)
            {
                SyntaxNode? nodeToRetrieve = null;

                // When method is invoked using nullability, .?Method(), then the node.Parent.Parent is the actual invocation.
                if (offendingNode.Parent.IsKind(SyntaxKind.MemberBindingExpression))
                {
                    nodeToRetrieve = offendingNode.Parent.Parent;
                }

                // Use Parent.Parent for MemberBindingExpression unless if it is null.
                invocationOperation = semanticModel.GetOperation(nodeToRetrieve ?? offendingNode.Parent, cancellationToken) as IInvocationOperation;
            }

            return invocationOperation;
        }

        static (SyntaxNode? syntaxExpression, ImmutableArray<ArgumentSyntax> syntaxArguments) GetExpressionAndArguments(SyntaxNode invocationNode)
        {
            SyntaxNode? syntaxExpression = null;
            var syntaxArguments = ImmutableArray<ArgumentSyntax>.Empty;

            if (invocationNode is InvocationExpressionSyntax invocationExpression)
            {
                syntaxExpression = invocationExpression.Expression;
                syntaxArguments = invocationExpression.ArgumentList.Arguments.ToImmutableArray();
            }

            return (syntaxExpression, syntaxArguments);
        }

        static SyntaxNode UpdatedRootWithCancellationTokenSyntax(
            Document doc,
            SyntaxNode root,
            IOperation invocation,
            SyntaxNode expression,
            ImmutableArray<ArgumentSyntax> currentArguments)
        {
            var generator = SyntaxGenerator.GetGenerator(doc);
            var ctArgumentSyntax = generator.Argument(generator.IdentifierName(CancellationTokenPropertiesName));
            var newArguments = currentArguments.CastArray<SyntaxNode>();
            newArguments = newArguments.Add(ctArgumentSyntax);

            // Insert the new arguments to the new invocation
            var newInvocationWithArguments = generator.InvocationExpression(expression, newArguments).WithTriviaFrom(invocation.Syntax);

            return generator.ReplaceNode(root, invocation.Syntax, newInvocationWithArguments);
        }
    }
}