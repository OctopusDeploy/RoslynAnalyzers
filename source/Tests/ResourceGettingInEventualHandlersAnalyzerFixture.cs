using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.RoslynAnalyzers.ResourceGettingInEventualHandlersAnalyzer>;

namespace Tests
{
    public class ResourceGettingInEventualHandlersAnalyzerFixture
    {
        [Test]
        public async Task DoesNotAnalyseClassNotImplementingEventualHandlerInterface()
        {
            const string source = @"
using System;

namespace TheNamespace
{
    public class SomeClass
    {
        public void DoNothing()
        {
            // does nothing
        }
    }
}";

            await Verify.VerifyAnalyzerAsync(source);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task AllowsEventualHandlerWhenGetOrNullIsCalled(bool assignQueryToVariable)
        {
            await Verify.VerifyAnalyzerAsync(GetSource(QueryType.GetOrNull, assignQueryToVariable));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task AllowsEventualHandlerImplementationWhenGetIsCalledAndErrorHandled(bool assignQueryToVariable)
        {
            var sourceWithErrorHandling = GetSource(QueryType.Get, assignQueryToVariable, handleError: true);
            await Verify.VerifyAnalyzerAsync(sourceWithErrorHandling);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task FlagsEventualHandlerImplementationWhenGetIsCalledWithoutErrorHandling(bool assignQueryToVariable)
        {
            var sourceWithoutErrorHandling = GetSource(QueryType.Get, assignQueryToVariable, handleError: false);
            await Verify.VerifyAnalyzerAsync(sourceWithoutErrorHandling, new DiagnosticResult(ResourceGettingInEventualHandlersAnalyzer.Rule).WithLocation(0));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task FlagsWhenIReadOnlyDocumentStoreDerivedTypeIsCalledIncorrectly(bool assignQueryToVariable)
        {
            var sourceWithDerivedDocumentStore = GetSource(QueryType.Get, assignQueryToVariable, useDerivedDocumentStore: true);
            await Verify.VerifyAnalyzerAsync(sourceWithDerivedDocumentStore, new DiagnosticResult(ResourceGettingInEventualHandlersAnalyzer.Rule).WithLocation(0));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task FlagsWhenGetOrNullIsCalledIfGetIsStillCalledIncorrectly(bool assignQueryToVariable)
        {
            var sourceWithGetStillCalledIncorrectly = GetSource(QueryType.GetOrNullThenGet, assignQueryToVariable);
            await Verify.VerifyAnalyzerAsync(sourceWithGetStillCalledIncorrectly, new DiagnosticResult(ResourceGettingInEventualHandlersAnalyzer.Rule).WithLocation(0));
        }

        static string GetSource(
            QueryType queryType,
            bool assignQueryToVariable,
            bool handleError = false,
            bool useDerivedDocumentStore = false) =>
            $@"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TheNamespace
{{
    public class SomeEventualHandler : IEventuallyHandle<SomeEvent>
    {{
        public async Task Handle(SomeEvent @event)
        {{
            if (@event is null)
            {{
                throw new ArgumentNullException(nameof(@event));
            }}

            {WrapWithErrorHandling(
                $"{RenderVariableAssignment(assignQueryToVariable)}{RenderQuery(queryType, useDerivedDocumentStore)};",
                handleError)};
        }}
    }}

    public interface IEventuallyHandle<TEvent>
    {{
        Task Handle(TEvent @event);
    }}

    public class SomeEvent {{ }}

    public interface IReadOnlyDocumentStore<TDocument, TKey>
    {{
        TDocument Get(TKey id);
        TDocument? GetOrNull(TKey id);
        Task<TDocument> Get(TKey id, CancellationToken cancellationToken);
        Task<TDocument?> GetOrNull(TKey id, CancellationToken cancellationToken);
    }}

    public class SimpleDocumentStore<TDocument, TKey> : IReadOnlyDocumentStore<TDocument, TKey>
    {{
        public TDocument Get(TKey id)
        {{
            throw new NotImplementedException();
        }}

        public TDocument? GetOrNull(TKey id)
        {{
            throw new NotImplementedException();
        }}

        public Task<TDocument> Get(TKey id, CancellationToken cancellationToken)
        {{
            throw new NotImplementedException();
        }}

        public Task<TDocument?> GetOrNull(TKey id, CancellationToken cancellationToken)
        {{
            throw new NotImplementedException();
        }}
    }}

    public class DerivedDocumentStore<TDocument, TKey> : SimpleDocumentStore<TDocument, TKey>
    {{
        public new TDocument Get(TKey id)
        {{
            throw new NotImplementedException();
        }}

        public new Task<TDocument> Get(TKey id, CancellationToken cancellationToken)
        {{
            throw new NotImplementedException();
        }}
    }}

    public class EntityNotFoundException : Exception {{ }}
}}
";

        static string RenderQuery(QueryType queryType, bool useDerivedDocumentStore) =>
            queryType switch
            {
                QueryType.Get => $@"new {RenderDocumentStore(useDerivedDocumentStore)}<object, string>().{{|#0:Get|}}(""SomeDocumentId"")",
                QueryType.GetOrNull => $@"new {RenderDocumentStore(useDerivedDocumentStore)}<object, string>().GetOrNull(""SomeDocumentId"")",
                QueryType.GetOrNullThenGet => $@"{RenderQuery(QueryType.GetOrNull, useDerivedDocumentStore)};{RenderQuery(QueryType.Get, useDerivedDocumentStore)}",
                _ => throw new NotImplementedException()
            };

        static string RenderDocumentStore(bool useDerivedDocumentStore) =>
            useDerivedDocumentStore ? "DerivedDocumentStore" : "SimpleDocumentStore";

        static string RenderVariableAssignment(bool assignQueryToVariable) =>
            assignQueryToVariable ? "var entity = " : "";

        static string WrapWithErrorHandling(string statementToHandle, bool handleError) =>
            handleError ? $@"try {{ {statementToHandle} }} catch (EntityNotFoundException) {{ }}" : statementToHandle;

        enum QueryType
        {
            Get,
            GetOrNull,
            GetOrNullThenGet
        }
    }
}
