using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using System;
using System.Linq;
using System.Threading.Tasks;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.RoslynAnalyzers.AsyncAnalyzer>;

namespace Tests
{
    public class AsyncAnalyzerFixture
    {
        [Test]
        public async Task NoDiagnosticsOnWellFormedRequest()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core
{
  static class SomeClass
  {
     static async Task NormalMethodWithNoReturnValue(CancellationToken cancellationToken)
     { } 
  }
}");
            await Verify.VerifyAnalyzerAsync(source);
        }
        
        [Test]
        public async Task MethodsReturningTaskMustBeAsync()
        {
            var source = WithOctopusTypes(@"
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
namespace Octopus.Core
{
  public interface SomeInterface
  {
    Task NonAsyncMethodReturningTask(CancellationToken cancellationToken);
  }

  public abstract class SomeAbstractClass
  {
    public abstract Task NonAsyncMethodReturningTask(CancellationToken cancellationToken); // doesn't flag on this because it's abstract
  }

  public class SomeDerivedClass : SomeAbstractClass
  {
    public override Task {|#0:NonAsyncMethodReturningTask|}(CancellationToken cancellationToken) // we should flag when we implement the abstract method
    { return Task.CompletedTask; }
  }

  public abstract class AsyncApiActionClass : IAsyncApiAction
  {
    public Task NonAsyncMethodReturningTask(CancellationToken cancellationToken) => Task.FromResult(""foo""); // doesn't flag on this because the class implements IAsyncApiAction
    public Task<string> ExecuteAsync(string request) => Task.FromResult(""foo"");
  }

  static class SomeClass
  {
    static int PlainOldMethodReturningInt(CancellationToken cancellationToken) => 0;

    static async Task AsyncMethodReturningTask(CancellationToken cancellationToken)
    { await Task.CompletedTask; }

    static Task {|#1:NonAsyncMethodReturningTask|}(CancellationToken cancellationToken)
    { return Task.FromResult(true); }

    static Task<int> {|#2:NonAsyncMethodReturningTaskOfInt|}(CancellationToken cancellationToken)
    { return Task.FromResult(1); }

    static Task<string> {|#3:NonAsyncMethodReturningTaskOfString|}(CancellationToken cancellationToken)
    { return Task.FromResult(""x""); } 

    static ValueTask {|#4:NonAsyncMethodReturningValueTask|}(CancellationToken cancellationToken)
    { return new ValueTask(); } 

    static ValueTask<string> {|#5:NonAsyncMethodReturningValueTaskOfString|}(CancellationToken cancellationToken)
    { return new ValueTask<string>(""x""); }

    static void ContainerMethod()
    {
        static async Task AsyncMethodReturningTask(CancellationToken cancellationToken)
        { await Task.CompletedTask; }

        // NOTE: NO DIAGNOSTIC on this. We only scan for toplevel methods, not inner methods and lambdas.
        // perhaps we should scan for inner things? At last visit (Dec 2022) the reflection based test didn't catch these
        // and we are aiming for parity with that, so decided to leave that discussion for another day.
        static Task NonAsyncMethodReturningTask(CancellationToken cancellationToken)
        { return Task.FromResult(true); }
    }
  }
}");
            await Verify.VerifyAnalyzerAsync(source, 
                Enumerable.Range(0, 6)
                .Select(i => new DiagnosticResult(Descriptors.MethodsReturningTaskMustBeAsync).WithLocation(i))
                .ToArray()); 
        }
        
        [Test]
        public async Task VoidMethodsMustNotBeAsync()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core
{
  static class SomeClass
  {
    static int PlainOldMethodReturningInt(CancellationToken cancellationToken) => 0;

    static void RegularVoidMethod()
    { }

    static async void {|#0:AsyncVoidMethod|}()
    { await Task.CompletedTask; }
  }
}");
            await Verify.VerifyAnalyzerAsync(source, 
                Enumerable.Range(0, 1)
                .Select(i => new DiagnosticResult(Descriptors.VoidMethodsMustNotBeAsync).WithLocation(i))
                .ToArray()); 
        }

        static readonly string AsyncTestTypeDeclarations = @"
namespace Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api
{
    public interface IAsyncApiAction
    {
        Task<string> ExecuteAsync(string request); // the real IAsyncApiAction uses IOctoRequest and IOctoResponse. The analyzer doesn't care so we can sub them during tests
    }
}
";
        
        static string WithOctopusTypes(string source) => $"{Common.Usings}{source}{Common.MessageTypeDeclarations}{AsyncTestTypeDeclarations}";
    }
}