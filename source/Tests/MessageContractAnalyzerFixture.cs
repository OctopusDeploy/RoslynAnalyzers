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

    public class SimpleResponse : IResponse { }
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
        public async Task RequestTypesMustBeNamedCorrectly()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleCommand: IRequest<SimpleCommand, SimpleResponse> { }
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            var nameResult = new DiagnosticResult(MessageContractAnalyzers.RequestTypesMustBeNamedCorrectly).WithSpan(4, 18, 4, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }

        [Test]
        public async Task RequestTypesMustBeNamedCorrectly_MultipleInterfaces()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class RequestSimple: ExtraneousBaseClass, ISomethingElse, IRequest<RequestSimple, SimpleResponse> { }
    public class SimpleResponse : IResponse { }

    public interface ISomethingElse { }
    public abstract class ExtraneousBaseClass { }
}
" + Common.MessageTypeDeclarations;

            var nameResult = new DiagnosticResult(MessageContractAnalyzers.RequestTypesMustBeNamedCorrectly).WithSpan(4, 18, 4, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }

        [Test]
        public async Task CommandTypesMustBeNamedCorrectly()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest : ICommand<SimpleRequest, SimpleResponse> { }
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            var result = new DiagnosticResult(MessageContractAnalyzers.CommandTypesMustBeNamedCorrectly).WithSpan(4, 18, 4, 31);

            await Verify.VerifyAnalyzerAsync(source, result);
        }

        [Test]
        public async Task RequestTypesMustHaveCorrectlyNamedResponseTypes()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResult> { }
    public class SimpleResult : IResponse { }
}
" + Common.MessageTypeDeclarations;

            var nameResult = new DiagnosticResult(MessageContractAnalyzers.RequestTypesMustHaveCorrectlyNamedResponseTypes).WithSpan(4, 18, 4, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }

        [Test]
        public async Task CommandTypesMustHaveCorrectlyNamedResponseTypes()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleCommand: ICommand<SimpleCommand, SimpleResult>
    { }
    public class SimpleResult : IResponse { }
}
" + Common.MessageTypeDeclarations;

            var nameResult = new DiagnosticResult(MessageContractAnalyzers.CommandTypesMustHaveCorrectlyNamedResponseTypes).WithSpan(4, 18, 4, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }
        
        [Test]
        public async Task PropertiesOnMessageTypesMustBeMutable()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleCommand: ICommand<SimpleCommand, SimpleResponse> 
    {
        [Optional]
        public string? GetOnlyProp { get; }
        
        [Optional]
        public string? ComputedProp => GetOnlyProp?.ToUpperInvariant();
    }
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.PropertiesOnMessageTypesMustBeMutable).WithSpan(10, 24, 10, 36),
                new DiagnosticResult(MessageContractAnalyzers.PropertiesOnMessageTypesMustBeMutable).WithSpan(7, 24, 7, 35));
        }

        [Test]
        public async Task RequiredPropertiesOnMessageTypesMustNotBeNullable()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        protected SimpleRequest() {}
        public SimpleRequest(string s, int i) { StringProperty = s; IntProperty = i; }

        [Required]
        public string? StringProperty { get; set; }

        [Required]
        public int? IntProperty { get; set; }
    }
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.RequiredPropertiesOnMessageTypesMustNotBeNullable).WithSpan(10, 24, 10, 38),
                new DiagnosticResult(MessageContractAnalyzers.RequiredPropertiesOnMessageTypesMustNotBeNullable).WithSpan(13, 21, 13, 32));
        }

        [Test]
        public async Task OptionalPropertiesOnMessageTypesMustBeNullable() // except collection types
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        [Optional]
        public string StringProperty { get; set; }

        [Optional]
        public int IntProperty { get; set; }

        [Optional]
        public string[] StringListProperty { get; set; } = null; // should NOT fire on this because optional nonnull collections are allowed
    }
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.OptionalPropertiesOnMessageTypesMustBeNullable).WithSpan(7, 23, 7, 37),
                new DiagnosticResult(MessageContractAnalyzers.OptionalPropertiesOnMessageTypesMustBeNullable).WithSpan(10, 20, 10, 31));
        }

        [Test]
        public async Task MessageTypesMustInstantiateCollections()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
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
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.MessageTypesMustInstantiateCollections).WithSpan(16, 29, 16, 47),
                new DiagnosticResult(MessageContractAnalyzers.MessageTypesMustInstantiateCollections).WithSpan(19, 25, 19, 44));
        }

        [Test]
        public async Task PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute() // except collection types
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        public string? StringProperty { get; set; }

        public int? IntProperty { get; set; }

        [Optional]
        public int? OptionalIntProperty { get; set; } // should not fire on this
    }
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute).WithSpan(6, 24, 6, 38),
                new DiagnosticResult(MessageContractAnalyzers.PropertiesOnMessageTypesMustHaveAtLeastOneValidationAttribute).WithSpan(8, 21, 8, 32));
        }
        
        [Test]
        public async Task SpaceIdPropertiesOnMessageTypesMustBeOfTypeSpaceId() // except collection types
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        [Optional]
        public SpaceId? SpaceId { get; set; } // should not fire on this
    }
    public class SimpleResponse : IResponse 
    {
        [Optional]
        public string? SpaceId { get; set; } // fire!, don't use strings for spaceId
    }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(MessageContractAnalyzers.SpaceIdPropertiesOnMessageTypesMustBeOfTypeSpaceId).WithSpan(12, 24, 12, 31));
        }
    }

    static class Common
    {
        // Declarations copied verbatim from MessageContracts
        public static readonly string MessageTypeDeclarations = @"
namespace Octopus.Server.MessageContracts.Base
{
  public interface IRequest<TRequest, TResponse> where TRequest : IRequest<TRequest, TResponse> where TResponse : IResponse { }
  public interface ICommand<TCommand, TResponse> where TCommand : ICommand<TCommand, TResponse> where TResponse : IResponse { }
  public interface IResponse { }

  namespace Attributes
  {
    public sealed class OptionalAttribute : ValidationAttribute { } // not quite verbatim for this but it doesn't matter
  }
}
namespace Octopus.Server.MessageContracts.Features.Spaces
{
    public class SpaceId {} // doesn't need any actual behaviour for the analyzer to pass.
}
";
        // stick these all on a single line to not interfere with diagnostic line location
        public static readonly string Usings = string.Join("",
            new[]
            {
                "System",
                "System.Collections.Generic",
                "System.ComponentModel.DataAnnotations",
                "Octopus.Server.MessageContracts.Base",
                "Octopus.Server.MessageContracts.Base.Attributes",
                "Octopus.Server.MessageContracts.Features.Spaces"
            }.Select(s => $"using {s};"));
    }
}