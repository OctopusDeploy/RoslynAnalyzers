using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using System;
using System.Threading.Tasks;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.RoslynAnalyzers.PersistenceAnalyzer>;

namespace Tests
{
    public class PersistenceAnalyzerFixture
    {
        [Test]
        public async Task DiagnosticOnFields()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core {
  public class SomeClass {
    readonly {|#0:IDocumentStore<Event, string>|} readonlyField = null!;
    {|#1:IDocumentStore<Event, string>|} mutableField = null!;
    static readonly {|#2:IDocumentStore<Event, string>|} staticReadonlyField = null!;
     
    readonly {|#3:IDocumentStore<Event, string>|}? readonlyNullableField = null!;
    {|#4:IDocumentStore<Event, string>|}? mutableNullableField = null;
    static readonly {|#5:IDocumentStore<Event, string>|}? staticReadonlyNullableField = null;

    // these are allowed (IReadOnlyDocumentStore)
    readonly IReadOnlyDocumentStore<Event, string> readonlyFieldRODS = null!;
    IReadOnlyDocumentStore<Event, string> mutableFieldRODS = null!;
    static readonly IReadOnlyDocumentStore<Event, string> staticReadonlyFieldRODS = null!;
     
    readonly IReadOnlyDocumentStore<Event, string>? readonlyNullableFieldRODS = null!;
    IReadOnlyDocumentStore<Event, string>? mutableNullableFieldRODS = null;
    static readonly IReadOnlyDocumentStore<Event, string>? staticReadonlyNullableFieldRODS = null;

    // these are allowed (not Event)
    readonly IDocumentStore<Project, string> readonlyFieldNotEvent = null!;
    IDocumentStore<Project, string> mutableFieldNotEvent = null!;
    static readonly IDocumentStore<Project, string> staticReadonlyFieldNotEvent = null!;
     
    readonly IDocumentStore<Project, string>? readonlyNullableFieldNotEvent = null!;
    IDocumentStore<Project, string>? mutableNullableFieldNotEvent = null;
    static readonly IDocumentStore<Project, string>? staticReadonlyNullableFieldNotEvent = null;
  }
}");
            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(Descriptors.DontUseIDocumentStoreOfEvent).WithLocation(0),
                new DiagnosticResult(Descriptors.DontUseIDocumentStoreOfEvent).WithLocation(1),
                new DiagnosticResult(Descriptors.DontUseIDocumentStoreOfEvent).WithLocation(2),
                new DiagnosticResult(Descriptors.DontUseIDocumentStoreOfEvent).WithLocation(3),
                new DiagnosticResult(Descriptors.DontUseIDocumentStoreOfEvent).WithLocation(4),
                new DiagnosticResult(Descriptors.DontUseIDocumentStoreOfEvent).WithLocation(5));
        }
        
        [Test]
        public async Task DiagnosticOnProperties()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core {
  public class SomeClass {
    {|#0:IDocumentStore<Event, string>|} ReadonlyProp {get;} = null!;
    {|#1:IDocumentStore<Event, string>|} MutableProp {get;set;} = null!;
    static {|#2:IDocumentStore<Event, string>|} StaticReadonlyProp {get;set;}= null!;
     
    {|#3:IDocumentStore<Event, string>|}? ReadonlyNullableProp {get;} = null!;
    {|#4:IDocumentStore<Event, string>|}? MutableNullableProp {get;set;} = null;
    static {|#5:IDocumentStore<Event, string>|}? StaticReadonlyNullableProp {get;set;} = null;

    // these are allowed (IReadOnlyDocumentStore)
    IReadOnlyDocumentStore<Event, string> ReadonlyPropRODS {get;} = null!;
    IReadOnlyDocumentStore<Event, string> MutablePropRODS {get;set;} = null!;
    static IReadOnlyDocumentStore<Event, string> StaticReadonlyPropRODS {get;set;} = null!;
     
    IReadOnlyDocumentStore<Event, string>? ReadonlyNullablePropRODS {get;} = null!;
    IReadOnlyDocumentStore<Event, string>? MutableNullablePropRODS {get;set;} = null;
    static IReadOnlyDocumentStore<Event, string>? StaticReadonlyNullablePropRODS {get;set;} = null;

    // these are allowed (not Event)
    IDocumentStore<Project, string> ReadonlyPropNotEvent {get;} = null!;
    IDocumentStore<Project, string> MutablePropNotEvent {get;set;} = null!;
    static IDocumentStore<Project, string> StaticReadonlyPropNotEvent {get;set;} = null!;
     
    IDocumentStore<Project, string>? ReadonlyNullablePropNotEvent {get;} = null!;
    IDocumentStore<Project, string>? MutableNullablePropNotEvent {get;set;} = null;
    static IDocumentStore<Project, string>? StaticReadonlyNullablePropNotEvent {get;set;} = null;
  }
}");
            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(Descriptors.DontUseIDocumentStoreOfEvent).WithLocation(0),
                new DiagnosticResult(Descriptors.DontUseIDocumentStoreOfEvent).WithLocation(1),
                new DiagnosticResult(Descriptors.DontUseIDocumentStoreOfEvent).WithLocation(2),
                new DiagnosticResult(Descriptors.DontUseIDocumentStoreOfEvent).WithLocation(3),
                new DiagnosticResult(Descriptors.DontUseIDocumentStoreOfEvent).WithLocation(4),
                new DiagnosticResult(Descriptors.DontUseIDocumentStoreOfEvent).WithLocation(5));
        }
        
        [Test]
        public async Task DiagnosticOnConstructorParam()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core {
  public class SomeClass {
    public SomeClass(
      {|#0:IDocumentStore<Event, string>|} eventStore,
      {|#1:IDocumentStore<Event, string>|}? nullableEventStore,
      IDocumentStore<Project, string> projectStore,
      IReadOnlyDocumentStore<Event, string> eventStoreRo,
      IReadOnlyDocumentStore<Event, string>? nullableEventStoreRo)
    { }
  }
}");
            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(Descriptors.DontUseIDocumentStoreOfEvent).WithLocation(0),
                new DiagnosticResult(Descriptors.DontUseIDocumentStoreOfEvent).WithLocation(1));
        }

        static readonly string LocalUsings = "using Octopus.Data.Model;using Octopus.Core.Persistence;using Octopus.Core.Model.Events;using Octopus.Core.Model.Projects;";

        static readonly string LocalDeclarations = @"
namespace Octopus.Data.Model {
    public interface IId : IId<string> { }

    public interface IId<out TId> {
        TId Id { get; }
    }
}
namespace Octopus.Core.Persistence {
  public interface IReadOnlyDocumentStore<TDocument, in TKey> where TDocument : class, IId<TKey> { }
  public interface IDocumentStore<TDocument, in TKey> : IReadOnlyDocumentStore<TDocument, TKey> where TDocument : class, IId<TKey> { }
}
namespace Octopus.Core.Model.Events {
  public class Event : IId {
    public string Id {get;} = """"; 
  }
}
namespace Octopus.Core.Model.Projects {
  public class Project : IId {
    public string Id {get;} = """"; 
  }
}";

        static string WithOctopusTypes(string source) => $"{Common.Usings}{LocalUsings}{source}{Common.MessageTypeDeclarations}{LocalDeclarations}";
    }
}