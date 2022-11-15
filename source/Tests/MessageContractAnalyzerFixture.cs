using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using System;
using System.Threading.Tasks;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.RoslynAnalyzers.MessageContractAnalyzers>;

namespace Tests
{
    static class Common
    {
        // Declarations copied verbatim from MessageContracts
        public static readonly string MessageTypeDeclarations = @"
namespace Octopus.Server.MessageContracts.Base
{
  public interface IRequest<TRequest, TResponse> where TRequest : IRequest<TRequest, TResponse> where TResponse : IResponse { }
  public interface ICommand<TCommand, TResponse> where TCommand : ICommand<TCommand, TResponse> where TResponse : IResponse { }
  public interface IResponse { }
}";
    }

    public class MessageContractAnalyzerFixture
    {
        [Test]
        public async Task NoDiagnosticsOnCorrectlyNamedRequest()
        {
            var source = @"
using Octopus.Server.MessageContracts.Base;
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest : IRequest<SimpleRequest, SimpleResponse> { }
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task NoDiagnosticsOnCorrectlyNamedCommand()
        {
            var source = @"
using Octopus.Server.MessageContracts.Base;
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleCommand: ICommand<SimpleCommand, SimpleResponse> { }
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task DiagnosticOnBadlyNamedRequest()
        {
            var source = @"
using Octopus.Server.MessageContracts.Base;
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleCommand: IRequest<SimpleCommand, SimpleResponse> { }
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            var nameResult = new DiagnosticResult(MessageContractAnalyzers.RequestNameRule).WithSpan(5, 18, 5, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }

        [Test]
        public async Task DiagnosticOnBadlyNamedRequest_MultipleInterfaces()
        {
            var source = @"
using Octopus.Server.MessageContracts.Base;
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class RequestSimple: ExtraneousBaseClass, ISomethingElse, IRequest<RequestSimple, SimpleResponse> { }
    public class SimpleResponse : IResponse { }

    public interface ISomethingElse { }
    public abstract class ExtraneousBaseClass { }
}
" + Common.MessageTypeDeclarations;

            var nameResult = new DiagnosticResult(MessageContractAnalyzers.RequestNameRule).WithSpan(5, 18, 5, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }

        [Test]
        public async Task DiagnosticOnBadlyNamedCommand()
        {
            var source = @"
using Octopus.Server.MessageContracts.Base;
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest : ICommand<SimpleRequest, SimpleResponse> { }
    public class SimpleResponse : IResponse { }
}
" + Common.MessageTypeDeclarations;

            var result = new DiagnosticResult(MessageContractAnalyzers.CommandNameRule).WithSpan(5, 18, 5, 31);

            await Verify.VerifyAnalyzerAsync(source, result);
        }

        [Test]
        public async Task DiagnosticOnBadlyNamedRequestResponse()
        {
            var source = @"
using Octopus.Server.MessageContracts.Base;
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResult> { }
    public class SimpleResult : IResponse { }
}
" + Common.MessageTypeDeclarations;

            var nameResult = new DiagnosticResult(MessageContractAnalyzers.RequestResponseNameRule).WithSpan(5, 18, 5, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }

        [Test]
        public async Task DiagnosticOnBadlyNamedCommandResponse()
        {
            var source = @"
using Octopus.Server.MessageContracts.Base;
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleCommand: ICommand<SimpleCommand, SimpleResult>
    { }
    public class SimpleResult : IResponse { }
}
" + Common.MessageTypeDeclarations;

            var nameResult = new DiagnosticResult(MessageContractAnalyzers.CommandResponseNameRule).WithSpan(5, 18, 5, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }
    }
}
