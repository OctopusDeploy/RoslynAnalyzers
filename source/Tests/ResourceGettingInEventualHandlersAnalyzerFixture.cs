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
        static DiagnosticResult ExpectedViolation => new DiagnosticResult(ResourceGettingInEventualHandlersAnalyzer.Rule).WithLocation(SourceBuilder.OffenderLocation);

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
            var source = new SourceBuilder(QueryType.GetOrNull).WithAssignQueryToVariable(assignQueryToVariable).Build();
            await Verify.VerifyAnalyzerAsync(source);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task AllowsEventualHandlerImplementationWhenGetIsCalledAndErrorHandled(bool assignQueryToVariable)
        {
            var sourceWithErrorHandling = new SourceBuilder(QueryType.Get)
                .WithAssignQueryToVariable(assignQueryToVariable)
                .WithErrorHandling()
                .Build();

            await Verify.VerifyAnalyzerAsync(sourceWithErrorHandling);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task FlagsEventualHandlerImplementationWhenGetIsCalledWithoutErrorHandling(bool assignQueryToVariable)
        {
            var sourceWithoutErrorHandling = new SourceBuilder(QueryType.Get)
                .WithAssignQueryToVariable(assignQueryToVariable)
                .WithErrorHandling(false)
                .Build();

            await Verify.VerifyAnalyzerAsync(sourceWithoutErrorHandling, ExpectedViolation);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task FlagsWhenIReadOnlyDocumentStoreDerivedTypeIsCalledIncorrectly(bool assignQueryToVariable)
        {
            var sourceWithDerivedDocumentStore = new SourceBuilder(QueryType.Get)
                .WithAssignQueryToVariable(assignQueryToVariable)
                .UsingDerivedDocumentStore()
                .Build();

            await Verify.VerifyAnalyzerAsync(sourceWithDerivedDocumentStore, ExpectedViolation);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task FlagsWhenGetOrNullIsCalledIfGetIsStillCalledIncorrectly(bool assignQueryToVariable)
        {
            var sourceWithGetStillCalledIncorrectly = new SourceBuilder(QueryType.GetOrNullThenGet)
                .WithAssignQueryToVariable(assignQueryToVariable)
                .Build();

            await Verify.VerifyAnalyzerAsync(sourceWithGetStillCalledIncorrectly, ExpectedViolation);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task FlagsWhenInvokingGetIncorrectlyOnIReadOnlyDocumentStoreDirectly(bool assignQueryToVariable)
        {
            var sourceWithDirectInvocation = new SourceBuilder(QueryType.Get)
                .WithAssignQueryToVariable(assignQueryToVariable)
                .UsingIReadOnlyDocumentStore()
                .Build();

            await Verify.VerifyAnalyzerAsync(sourceWithDirectInvocation, ExpectedViolation);
        }

        class SourceBuilder
        {
            public const int OffenderLocation = 0;
            readonly QueryType queryType;
            bool assignQueryToVariable;
            bool handleError;
            bool useDerivedDocumentStore;
            bool useIReadOnlyDocumentStore;

            public SourceBuilder(QueryType queryType)
            {
                this.queryType = queryType;
            }

            public SourceBuilder WithAssignQueryToVariable(bool value)
            {
                assignQueryToVariable = value;
                return this;
            }

            public SourceBuilder WithErrorHandling(bool value = true)
            {
                handleError = value;
                return this;
            }

            public SourceBuilder UsingDerivedDocumentStore()
            {
                if (useIReadOnlyDocumentStore)
                {
                    throw new InvalidOperationException(
                        $"Cannot call {nameof(UsingDerivedDocumentStore)} because {nameof(UsingIReadOnlyDocumentStore)} has already been called");
                }

                useDerivedDocumentStore = true;
                return this;
            }

            public SourceBuilder UsingIReadOnlyDocumentStore()
            {
                if (useDerivedDocumentStore)
                {
                    throw new InvalidOperationException(
                        $"Cannot call {nameof(UsingIReadOnlyDocumentStore)} because {nameof(UsingDerivedDocumentStore)} has already been called");
                }

                useIReadOnlyDocumentStore = true;
                return this;
            }

            public string Build() =>
                $@"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TheNamespace
{{
    public class SomeEventualHandler : IEventuallyHandleEvent<SomeEvent>
    {{
        readonly {RenderDocumentStoreType()} documentStore = new DerivedDocumentStore<object, string>();

        public async Task Handle(SomeEvent @event)
        {{
            if (@event is null)
            {{
                throw new ArgumentNullException(nameof(@event));
            }}

            {WrapWithErrorHandling($"{RenderVariableAssignment()}{RenderQuery()};")}
        }}
    }}

    public interface IEventuallyHandleEvent<TEvent>
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

            string RenderQuery(QueryType? explicitQueryType = null) =>
                (explicitQueryType ?? queryType) switch
                {
                    QueryType.Get => $@"documentStore.{{|#{OffenderLocation}:Get|}}(""SomeDocumentId"")",
                    QueryType.GetOrNull => @"documentStore.GetOrNull(""SomeDocumentId"")",
                    QueryType.GetOrNullThenGet => $@"{RenderQuery(QueryType.GetOrNull)};{RenderQuery(QueryType.Get)}",
                    _ => throw new NotImplementedException()
                };

            string RenderDocumentStoreType() =>
                (useDerivedDocumentStore, useIReadOnlyDocumentStore) switch
                {
                    (true, true) => throw new InvalidOperationException(
                        "Cannot render both DerivedDocumentStore and IReadOnlyDocumentStore"),
                    (true, false) => "DerivedDocumentStore<object, string>",
                    (false, true) => "IReadOnlyDocumentStore<object, string>",
                    (false, false) => "SimpleDocumentStore<object, string>"
                };

            string RenderVariableAssignment() => assignQueryToVariable ? "var entity = " : "";

            string WrapWithErrorHandling(string statementToHandle) =>
                handleError ? $@"try {{ {statementToHandle} }} catch (EntityNotFoundException) {{ }}" : statementToHandle;
        }

        enum QueryType
        {
            Get,
            GetOrNull,
            GetOrNullThenGet
        }
    }
}
