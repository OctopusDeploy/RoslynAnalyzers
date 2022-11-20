using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using System;
using System.Linq;
using System.Threading.Tasks;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.RoslynAnalyzers.MessageContractAnalyzers>;

namespace Tests
{
    public class MessageContractAnalyzerFixture
    {
        [Test]
        public async Task NoDiagnosticsOnWellFormedRequest()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class SimpleRequest : IRequest<SimpleRequest, SimpleResponse>
    {
        protected SimpleRequest() { }
        public SimpleRequest(string s, int i) 
        {
            RequiredString = s; 
            RequiredInt = i;
        }

        [Required]
        public string RequiredString { get; set; }

        [Required]
        public int RequiredInt { get; set; }

        [Optional]
        public string? OptionalString { get; set; }

        [Optional]
        public int? OptionalInt { get; set; }

        [Optional]
        public List<string> OptionalStringList { get; set; } = new(); 
    }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
}");

            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task NoDiagnosticsOnWellFormedCommand()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>command</summary>
    public class SimpleCommand : ICommand<SimpleCommand, SimpleResponse>
    {
        protected SimpleCommand() { }
        public SimpleCommand(string requiredString) 
        {
            RequiredString = requiredString; 
        }

        [Required]
        public string RequiredString { get; set; }

        [Optional]
        public string? OptionalString { get; set; }
    }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }

    /// <summary>command v1</summary>
    public class SimpleCommandV1 : ICommand<SimpleCommandV1, SimpleResponseV1> { }
    /// <summary>response v1</summary>
    public class SimpleResponseV1 : IResponse { }
}");

            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task NoDiagnosticsOnWellFormedResponse()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>response</summary>
    public class SimpleResponse : IResponse 
    {
        protected SimpleResponse() { }
        public SimpleResponse(string requiredString) 
        {
            RequiredString = requiredString; 
        }

        [Required]
        public string RequiredString { get; set; }

        [Optional]
        public string? OptionalString { get; set; }
    }
}");

            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task NoDiagnosticsOnWellFormedEvent()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleEvent : IEvent { }
    public class SimpleEventV1 : IEvent { }
}");

            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task EventTypesMustBeNamedCorrectly()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class {|#0:EventWhichIsSimple|}: IEvent { }
}");

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.EventTypesMustBeNamedCorrectly).WithLocation(0));
        }

        [Test]
        public async Task RequestTypesMustBeNamedCorrectly()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class {|#0:SimpleCommand|}: IRequest<SimpleCommand, SimpleResponse> { }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
}");

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.RequestTypesMustBeNamedCorrectly).WithLocation(0));
        }

        [Test]
        public async Task RequestTypesMustBeNamedCorrectly_MultipleInterfaces()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class {|#0:RequestSimple|}: ExtraneousBaseClass, ISomethingElse, IRequest<RequestSimple, SimpleResponse> { }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }

    public interface ISomethingElse { }
    public abstract class ExtraneousBaseClass { }
}");

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.RequestTypesMustBeNamedCorrectly).WithLocation(0));
        }

        [Test]
        public async Task CommandTypesMustBeNamedCorrectly()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class {|#0:SimpleRequest|} : ICommand<SimpleRequest, SimpleResponse> { }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
}");

            await Verify.VerifyAnalyzerAsync(source, new DiagnosticResult(MessageContractAnalyzers.CommandTypesMustBeNamedCorrectly).WithLocation(0));
        }

        [Test]
        public async Task RequestTypesMustHaveCorrectlyNamedResponseTypes()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class {|#0:SimpleRequest|}: IRequest<SimpleRequest, SimpleResult> { }
    /// <summary>response</summary>
    public class SimpleResult : IResponse { }

    /// <summary>requestV1</summary>
    public class {|#1:SimpleRequestV1|}: IRequest<SimpleRequestV1, SimpleResult> { } // requestV1 must have matching responseV1
}");

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.RequestTypesMustHaveCorrectlyNamedResponseTypes).WithLocation(0),
                new DiagnosticResult(MessageContractAnalyzers.RequestTypesMustHaveCorrectlyNamedResponseTypes).WithLocation(1));
        }

        [Test]
        public async Task CommandTypesMustHaveCorrectlyNamedResponseTypes()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>command</summary>
    public class {|#0:SimpleCommand|}: ICommand<SimpleCommand, SimpleResult>
    { }
    /// <summary>result</summary>
    public class SimpleResult : IResponse { }
}");

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.CommandTypesMustHaveCorrectlyNamedResponseTypes).WithLocation(0));
        }

        [Test]
        public async Task PropertiesOnMessageTypesMustBeMutable()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>command</summary>
    public class SimpleCommand: ICommand<SimpleCommand, SimpleResponse> 
    {
        [Optional]
        public string? {|#0:GetOnlyProp|} { get; }
        
        [Optional]
        public string? {|#1:ComputedProp|} => GetOnlyProp?.ToUpperInvariant();
    }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
}");

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.PropertiesOnMessageTypesMustBeMutable).WithLocation(0),
                new DiagnosticResult(MessageContractAnalyzers.PropertiesOnMessageTypesMustBeMutable).WithLocation(1));
        }

        [Test]
        public async Task RequiredPropertiesOnMessageTypesMustNotBeNullable()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        protected SimpleRequest() {}
        public SimpleRequest(string s, int i) { StringProperty = s; IntProperty = i; }

        [Required]
        public string? {|#0:StringProperty|} { get; set; }

        [Required]
        public int? {|#1:IntProperty|} { get; set; }
    }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
}");

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.RequiredPropertiesOnMessageTypesMustNotBeNullable).WithLocation(0),
                new DiagnosticResult(MessageContractAnalyzers.RequiredPropertiesOnMessageTypesMustNotBeNullable).WithLocation(1));
        }

        [Test]
        public async Task OptionalPropertiesOnMessageTypesMustBeNullable() // except collection types
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        [Optional]
        public string {|#0:StringProperty|} { get; set; }

        [Optional]
        public int {|#1:IntProperty|} { get; set; }

        [Optional]
        public string[] StringListProperty { get; set; } = null; // should NOT fire on this because optional nonnull collections are allowed
    }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
}");

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.OptionalPropertiesOnMessageTypesMustBeNullable).WithLocation(0),
                new DiagnosticResult(MessageContractAnalyzers.OptionalPropertiesOnMessageTypesMustBeNullable).WithLocation(1));
        }

        [Test]
        public async Task MessageTypesMustInstantiateCollections()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        protected SimpleRequest() { }
        public SimpleRequest(List<string> requiredStringList)
        {
            RequiredStringListProperty = requiredStringList;
        }

        [Required]
        public List<string> RequiredStringListProperty { get; set; } // should not fire on this; collection required so another check will ensure it's assigned in the constructor

        [Optional]
        public List<string> {|#0:StringListProperty|} { get; set; } // should fire on this; collection is nonnullable

        [Optional]
        public string[] {|#1:StringArrayProperty|} { get; set; } // should fire on this; collection is nonnullable

        [Optional]
        public List<string>? NullableStringListProperty { get; set; } // should not fire on this; collection is nullable

        [Optional]
        public string[]? NullableStringArrayProperty { get; set; } // should not fire on this; collection is nullable

        [Optional]
        public List<string> StringListPropertyInstantiated { get; set; } = new(); // should not fire on this; collection is instantiated

        [Optional]
        public string[] StringArrayPropertyInstantiated { get; set; } = Array.Empty<string>(); // should not fire on this; collection is instantiated
    }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
}");

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.MessageTypesMustInstantiateCollections).WithLocation(0),
                new DiagnosticResult(MessageContractAnalyzers.MessageTypesMustInstantiateCollections).WithLocation(1));
        }

        [Test]
        public async Task PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute() // except collection types
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        public string? {|#0:StringProperty|} { get; set; }

        public int? {|#1:IntProperty|} { get; set; }

        [Optional]
        public int? OptionalIntProperty { get; set; } // should not fire on this
    }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
}");

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute).WithLocation(0),
                new DiagnosticResult(MessageContractAnalyzers.PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute).WithLocation(1));
        }

        [Test]
        public async Task SpaceIdPropertiesOnMessageTypesMustBeOfTypeSpaceId() // except collection types
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        [Optional]
        public SpaceId? SpaceId { get; set; } // should not fire on this
    }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse 
    {
        [Optional]
        public string? {|#0:SpaceId|} { get; set; } // fire!, don't use strings for spaceId
    }
}");

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.SpaceIdPropertiesOnMessageTypesMustBeOfTypeSpaceId).WithLocation(0));
        }

        [Test]
        public async Task IdPropertiesOnMessageTypesMustBeACaseInsensitiveStringTinyType() // except collection types
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        [Optional]
        public string? {|#0:EnvironmentId|} { get; set; } // should fire on this

        [Optional]
        public ProjectId? ProjectId { get; set; } // should not fire on this
    }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
    public class ProjectId : CaseInsensitiveStringTinyType { }
}");

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.IdPropertiesOnMessageTypesMustBeACaseInsensitiveStringTinyType).WithLocation(0));
        }

        [Test]
        public async Task MessageTypesMustHaveXmlDocComments()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class {|#0:UncommentedCommand|}: ICommand<UncommentedCommand, UncommentedResponse> { }
    public class {|#1:UncommentedRequest|}: IRequest<UncommentedRequest, UncommentedResponse> { }
    public class {|#2:UncommentedResponse|} : IResponse { }

    /// <summary>a command</summary>
    public class CommentedCommand: ICommand<CommentedCommand, CommentedResponse> { }
    /// <summary>a request</summary>
    public class CommentedRequest: IRequest<CommentedRequest, CommentedResponse> { }
    /// <summary>a response</summary>
    public class CommentedResponse : IResponse { }
}");

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.MessageTypesMustHaveXmlDocComments).WithLocation(0),
                new DiagnosticResult(MessageContractAnalyzers.MessageTypesMustHaveXmlDocComments).WithLocation(1),
                new DiagnosticResult(MessageContractAnalyzers.MessageTypesMustHaveXmlDocComments).WithLocation(2));
        }

        // ApiContractTypes is broader than just IRequest, ICommand and IResponse,
        // see IsAnApiContractType in MessageContractConventions.cs in the server
        // it also includes IEvent, subclasses of Resource and anything in the Octopus.Server.MessageContracts namespace
        // that last namespace one is a doozy, but we don't have to check it here.
        [Test]
        public async Task ApiContractTypes_MustLiveInTheAppropriateNamespace()
        {
            var source = WithOctopusTypes(@"
// rookie mistake: putting the messagecontracts in the same place as the controller. These should all flag
namespace Octopus.Server.Web.Controllers.Something
{
    /// <summary>a command</summary>
    public class {|#0:SomeCommand|}: ICommand<SomeCommand, SomeResponse> { }
    /// <summary>a request</summary>
    public class {|#1:SomeRequest|}: IRequest<SomeRequest, SomeResponse> { }
    /// <summary>a response</summary>
    public class {|#2:SomeResponse|}: IResponse { }

    public class {|#3:SomeEvent|} : IEvent { }
    public class {|#4:SomeResource|} : Resource { }
}
// rookie mistake #2: putting the messagecontracts in the same place as the handler. These should all flag
namespace Octopus.Core.Features.Something
{
    /// <summary>a command</summary>
    public class {|#10:SomeCommand|}: ICommand<SomeCommand, SomeResponse> { }
    /// <summary>a request</summary>
    public class {|#11:SomeRequest|}: IRequest<SomeRequest, SomeResponse> { }
    /// <summary>a response</summary>
    public class {|#12:SomeResponse|}: IResponse { }

    public class {|#13:SomeEvent|} : IEvent { }
    public class {|#14:SomeResource|} : Resource { }
}
// should not flag on any of these
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>a command</summary>
    public class SomeCommand: ICommand<SomeCommand, SomeResponse> { }
    /// <summary>a request</summary>
    public class SomeRequest: IRequest<SomeRequest, SomeResponse> { }
    /// <summary>a response</summary>
    public class SomeResponse : IResponse { }

    public class SomeEvent : IEvent { }
    public class SomeResource : Resource { }
}
// or these
namespace Octopus.Core.MessageContracts
{
    /// <summary>a command</summary>
    public class SomeCommand: ICommand<SomeCommand, SomeResponse> { }
    /// <summary>a request</summary>
    public class SomeRequest: IRequest<SomeRequest, SomeResponse> { }

    /// <summary>a response</summary>
    public class SomeResponse : IResponse { }
    public class SomeEvent : IEvent { }
    public class SomeResource : Resource { }
}");

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithLocation(0),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithLocation(1),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithLocation(2),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithLocation(3),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithLocation(4),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithLocation(10),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithLocation(11),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithLocation(12),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithLocation(13),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithLocation(14));
        }

        static string WithOctopusTypes(string source) => $"{Common.Usings}{source}{Common.MessageTypeDeclarations}";
    }

    static class Common
    {
        // Declarations copied from MessageContracts. We need the names and structure to match exactly but the implementations
        // are irrelevant so we don't replicate them here (e.g. the fact that the Resource class implements IResource and stuff like that)
        public static readonly string MessageTypeDeclarations = @"
namespace Octopus.Server.MessageContracts
{
  public interface IEvent { }
  public abstract class Resource { }

  namespace Base
  {
    public interface IRequest<TRequest, TResponse> where TRequest : IRequest<TRequest, TResponse> where TResponse : IResponse { }
    public interface ICommand<TCommand, TResponse> where TCommand : ICommand<TCommand, TResponse> where TResponse : IResponse { }
    public interface IResponse { }

    namespace Attributes
    {
      public sealed class OptionalAttribute : ValidationAttribute { } // not quite verbatim for this but it doesn't matter
    }
  }
}
namespace Octopus.Server.MessageContracts.Features.Spaces
{
  public class SpaceId {} // doesn't need any actual behaviour for the analyzer to pass.
}
namespace Octopus.TinyTypes
{
  public class CaseInsensitiveStringTinyType { }
}
";

        // stick these all on a single line to not interfere with diagnostic line location
        public static readonly string Usings = string.Join("",
            new[]
            {
                "System",
                "System.Collections.Generic",
                "System.ComponentModel.DataAnnotations",
                "Octopus.Server.MessageContracts",
                "Octopus.Server.MessageContracts.Base",
                "Octopus.Server.MessageContracts.Base.Attributes",
                "Octopus.Server.MessageContracts.Features.Spaces",
                "Octopus.TinyTypes"
            }.Select(s => $"using {s};"));
    }
}