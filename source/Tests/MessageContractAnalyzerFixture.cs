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
            var source = Common.Usings + @"
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
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task NoDiagnosticsOnWellFormedCommand()
        {
            var source = Common.Usings + @"
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
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task NoDiagnosticsOnWellFormedResponse()
        {
            var source = Common.Usings + @"
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
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source);
        }
        
        [Test]
        public async Task NoDiagnosticsOnWellFormedEvent()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleEvent : IEvent { }
    public class SimpleEventV1 : IEvent { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source);
        }
        
        [Test]
        public async Task EventTypesMustBeNamedCorrectly()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class EventWhichIsSimple: IEvent { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source, 
                new DiagnosticResult(MessageContractAnalyzers.EventTypesMustBeNamedCorrectly).WithSpan(4, 18, 4, 36));
        }

        [Test]
        public async Task RequestTypesMustBeNamedCorrectly()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class SimpleCommand: IRequest<SimpleCommand, SimpleResponse> { }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source, 
                new DiagnosticResult(MessageContractAnalyzers.RequestTypesMustBeNamedCorrectly).WithSpan(5, 18, 5, 31));
        }

        [Test]
        public async Task RequestTypesMustBeNamedCorrectly_MultipleInterfaces()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class RequestSimple: ExtraneousBaseClass, ISomethingElse, IRequest<RequestSimple, SimpleResponse> { }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }

    public interface ISomethingElse { }
    public abstract class ExtraneousBaseClass { }
}
" + Common.MessageTypeDeclarations;

            var nameResult = new DiagnosticResult(MessageContractAnalyzers.RequestTypesMustBeNamedCorrectly).WithSpan(5, 18, 5, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }

        [Test]
        public async Task CommandTypesMustBeNamedCorrectly()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class SimpleRequest : ICommand<SimpleRequest, SimpleResponse> { }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            var result = new DiagnosticResult(MessageContractAnalyzers.CommandTypesMustBeNamedCorrectly).WithSpan(5, 18, 5, 31);

            await Verify.VerifyAnalyzerAsync(source, result);
        }

        [Test]
        public async Task RequestTypesMustHaveCorrectlyNamedResponseTypes()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResult> { }
    /// <summary>response</summary>
    public class SimpleResult : IResponse { }

    /// <summary>requestV1</summary>
    public class SimpleRequestV1: IRequest<SimpleRequestV1, SimpleResult> { } // requestV1 must have matching responseV1
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source, 
                new DiagnosticResult(MessageContractAnalyzers.RequestTypesMustHaveCorrectlyNamedResponseTypes).WithSpan(5, 18, 5, 31),
                new DiagnosticResult(MessageContractAnalyzers.RequestTypesMustHaveCorrectlyNamedResponseTypes).WithSpan(10, 18, 10, 33));
        }

        [Test]
        public async Task CommandTypesMustHaveCorrectlyNamedResponseTypes()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>command</summary>
    public class SimpleCommand: ICommand<SimpleCommand, SimpleResult>
    { }
    /// <summary>result</summary>
    public class SimpleResult : IResponse { }
}
" + Common.MessageTypeDeclarations;


            await Verify.VerifyAnalyzerAsync(source, 
                new DiagnosticResult(MessageContractAnalyzers.CommandTypesMustHaveCorrectlyNamedResponseTypes).WithSpan(5, 18, 5, 31));
        }
        
        [Test]
        public async Task PropertiesOnMessageTypesMustBeMutable()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>command</summary>
    public class SimpleCommand: ICommand<SimpleCommand, SimpleResponse> 
    {
        [Optional]
        public string? GetOnlyProp { get; }
        
        [Optional]
        public string? ComputedProp => GetOnlyProp?.ToUpperInvariant();
    }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.PropertiesOnMessageTypesMustBeMutable).WithSpan(11, 24, 11, 36),
                new DiagnosticResult(MessageContractAnalyzers.PropertiesOnMessageTypesMustBeMutable).WithSpan(8, 24, 8, 35));
        }

        [Test]
        public async Task RequiredPropertiesOnMessageTypesMustNotBeNullable()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        protected SimpleRequest() {}
        public SimpleRequest(string s, int i) { StringProperty = s; IntProperty = i; }

        [Required]
        public string? StringProperty { get; set; }

        [Required]
        public int? IntProperty { get; set; }
    }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.RequiredPropertiesOnMessageTypesMustNotBeNullable).WithSpan(11, 24, 11, 38),
                new DiagnosticResult(MessageContractAnalyzers.RequiredPropertiesOnMessageTypesMustNotBeNullable).WithSpan(14, 21, 14, 32));
        }

        [Test]
        public async Task OptionalPropertiesOnMessageTypesMustBeNullable() // except collection types
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        [Optional]
        public string StringProperty { get; set; }

        [Optional]
        public int IntProperty { get; set; }

        [Optional]
        public string[] StringListProperty { get; set; } = null; // should NOT fire on this because optional nonnull collections are allowed
    }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.OptionalPropertiesOnMessageTypesMustBeNullable).WithSpan(8, 23, 8, 37),
                new DiagnosticResult(MessageContractAnalyzers.OptionalPropertiesOnMessageTypesMustBeNullable).WithSpan(11, 20, 11, 31));
        }

        [Test]
        public async Task MessageTypesMustInstantiateCollections()
        {
            var source = Common.Usings + @"
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
        public List<string> StringListProperty { get; set; } // should fire on this; collection is nonnullable

        [Optional]
        public string[] StringArrayProperty { get; set; } // should fire on this; collection is nonnullable

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
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.MessageTypesMustInstantiateCollections).WithSpan(17, 29, 17, 47),
                new DiagnosticResult(MessageContractAnalyzers.MessageTypesMustInstantiateCollections).WithSpan(20, 25, 20, 44));
        }

        [Test]
        public async Task PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute() // except collection types
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        public string? StringProperty { get; set; }

        public int? IntProperty { get; set; }

        [Optional]
        public int? OptionalIntProperty { get; set; } // should not fire on this
    }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute).WithSpan(7, 24, 7, 38),
                new DiagnosticResult(MessageContractAnalyzers.PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute).WithSpan(9, 21, 9, 32));
        }
        
        [Test]
        public async Task SpaceIdPropertiesOnMessageTypesMustBeOfTypeSpaceId() // except collection types
        {
            var source = Common.Usings + @"
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
        public string? SpaceId { get; set; } // fire!, don't use strings for spaceId
    }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.SpaceIdPropertiesOnMessageTypesMustBeOfTypeSpaceId).WithSpan(14, 24, 14, 31));
        }
        
        [Test]
        public async Task IdPropertiesOnMessageTypesMustBeACaseInsensitiveStringTinyType() // except collection types
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    /// <summary>request</summary>
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        [Optional]
        public string? EnvironmentId { get; set; } // should fire on this

        [Optional]
        public ProjectId? ProjectId { get; set; } // should not fire on this
    }
    /// <summary>response</summary>
    public class SimpleResponse : IResponse { }
    public class ProjectId : CaseInsensitiveStringTinyType { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.IdPropertiesOnMessageTypesMustBeACaseInsensitiveStringTinyType).WithSpan(8, 24, 8, 37));
        }
        
        [Test]
        public async Task MessageTypesMustHaveXmlDocComments()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class UncommentedCommand: ICommand<UncommentedCommand, UncommentedResponse> { }
    public class UncommentedRequest: IRequest<UncommentedRequest, UncommentedResponse> { }
    public class UncommentedResponse : IResponse { }

    /// <summary>a command</summary>
    public class CommentedCommand: ICommand<CommentedCommand, CommentedResponse> { }
    /// <summary>a request</summary>
    public class CommentedRequest: IRequest<CommentedRequest, CommentedResponse> { }
    /// <summary>a response</summary>
    public class CommentedResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.MessageTypesMustHaveXmlDocComments).WithSpan(4, 18, 4, 36),
                new DiagnosticResult(MessageContractAnalyzers.MessageTypesMustHaveXmlDocComments).WithSpan(5, 18, 5, 36),
                new DiagnosticResult(MessageContractAnalyzers.MessageTypesMustHaveXmlDocComments).WithSpan(6, 18, 6, 37));
        }

        // ApiContractTypes is broader than just IRequest, ICommand and IResponse,
        // see IsAnApiContractType in MessageContractConventions.cs in the server
        // it also includes IEvent, subclasses of Resource and anything in the Octopus.Server.MessageContracts namespace
        // that last namespace one is a doozy, but we don't have to check it here.
        [Test]
        public async Task ApiContractTypes_MustLiveInTheAppropriateNamespace()
        {
            var source = Common.Usings + @"
// rookie mistake: putting the messagecontracts in the same place as the controller. These should all flag
namespace Octopus.Server.Web.Controllers.Something
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
// rookie mistake #2: putting the messagecontracts in the same place as the handler. These should all flag
namespace Octopus.Core.Features.Something
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
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithSpan(6, 18, 6, 29),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithSpan(8, 18, 8, 29),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithSpan(10, 18, 10, 30),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithSpan(12, 18, 12, 27),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithSpan(13, 18, 13, 30),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithSpan(19, 18, 19, 29),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithSpan(21, 18, 21, 29),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithSpan(23, 18, 23, 30),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithSpan(25, 18, 25, 27),
                new DiagnosticResult(MessageContractAnalyzers.ApiContractTypesMustLiveInTheAppropriateNamespace).WithSpan(26, 18, 26, 30));
        }
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