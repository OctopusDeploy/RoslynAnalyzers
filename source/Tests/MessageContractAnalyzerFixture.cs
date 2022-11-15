using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using System;
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
        public SimpleRequest(string requiredString) 
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
        public async Task BadlyNamedRequest()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleCommand: IRequest<SimpleCommand, SimpleResponse> { }
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            var nameResult = new DiagnosticResult(MessageContractAnalyzers.RequestNameRule).WithSpan(4, 18, 4, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }

        [Test]
        public async Task BadlyNamedRequest_MultipleInterfaces()
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

            var nameResult = new DiagnosticResult(MessageContractAnalyzers.RequestNameRule).WithSpan(4, 18, 4, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }

        [Test]
        public async Task BadlyNamedCommand()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest : ICommand<SimpleRequest, SimpleResponse> { }
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            var result = new DiagnosticResult(MessageContractAnalyzers.CommandNameRule).WithSpan(4, 18, 4, 31);

            await Verify.VerifyAnalyzerAsync(source, result);
        }

        [Test]
        public async Task BadlyNamedRequestResponse()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResult> { }
    public class SimpleResult : IResponse { }
}
" + Common.MessageTypeDeclarations;

            var nameResult = new DiagnosticResult(MessageContractAnalyzers.RequestResponseNameRule).WithSpan(4, 18, 4, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }

        [Test]
        public async Task NamedCommandResponse()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleCommand: ICommand<SimpleCommand, SimpleResult>
    { }
    public class SimpleResult : IResponse { }
}
" + Common.MessageTypeDeclarations;

            var nameResult = new DiagnosticResult(MessageContractAnalyzers.CommandResponseNameRule).WithSpan(4, 18, 4, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }

        [Test]
        public async Task RequestWithImmutableProperty()
        {
            var source = Common.Usings + @"
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResponse> 
    {
        public string GetOnlyProp { get; }
    }
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            var nameResult = new DiagnosticResult(MessageContractAnalyzers.RequestNameRule).WithSpan(4, 18, 4, 31);

            await Verify.VerifyAnalyzerAsync(source); // TODO
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
}";
        // stick these all on a single line to not interfere with diagnostic line location
        public static readonly string Usings = @"using Octopus.Server.MessageContracts.Base;using Octopus.Server.MessageContracts.Base.Attributes;using System.ComponentModel.DataAnnotations;";
    }

}