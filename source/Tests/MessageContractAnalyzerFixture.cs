using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.RoslynAnalyzers.CommandAndRequestTypesMustBeNamedCorrectlyAnalyzer>;

namespace Tests
{
    static class Common
    {
        // Declarations copied verbatim from MessageContracts
        public static readonly string IRequestIResponseDeclarations = @"
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
            string source = @"
using Octopus.Server.MessageContracts.Base;
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest : IRequest<SimpleRequest, SimpleResponse> { }
    public class SimpleResponse : IResponse { }
}
" + Common.IRequestIResponseDeclarations;

            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task NoDiagnosticsOnCorrectlyNamedCommand()
        {
            string source = @"
using Octopus.Server.MessageContracts.Base;
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleCommand: ICommand<SimpleCommand, SimpleResponse> { }
    public class SimpleResponse : IResponse { }
}
" + Common.IRequestIResponseDeclarations;

            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task DiagnosticOnBadlyNamedRequest()
        {
            string source = @"
using Octopus.Server.MessageContracts.Base;
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleCommand: IRequest<SimpleCommand, SimpleResponse> { }
    public class SimpleResponse : IResponse { }
}
" + Common.IRequestIResponseDeclarations;

            var nameResult = new DiagnosticResult(CommandAndRequestTypesMustBeNamedCorrectlyAnalyzer.RequestNameRule).WithSpan(5, 18, 5, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }

        [Test]
        public async Task DiagnosticOnBadlyNamedCommand()
        {
            string source = @"
using Octopus.Server.MessageContracts.Base;
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest : ICommand<SimpleRequest, SimpleResponse> { }
    public class SimpleResponse : IResponse { }
}
" + Common.IRequestIResponseDeclarations;

            var result = new DiagnosticResult(CommandAndRequestTypesMustBeNamedCorrectlyAnalyzer.CommandNameRule).WithSpan(5, 18, 5, 31);

            await Verify.VerifyAnalyzerAsync(source, result);
        }

        [Test]
        public async Task DiagnosticOnBadlyNamedRequestResponse()
        {
            string source = @"
using Octopus.Server.MessageContracts.Base;
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleRequest: IRequest<SimpleRequest, SimpleResult> { }
    public class SimpleResult : IResponse { }
}
" + Common.IRequestIResponseDeclarations;

            var nameResult = new DiagnosticResult(CommandAndRequestTypesMustBeNamedCorrectlyAnalyzer.RequestResponseNameRule).WithSpan(5, 18, 5, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }

        [Test]
        public async Task DiagnosticOnBadlyNamedCommandResponse()
        {
            string source = @"
using Octopus.Server.MessageContracts.Base;
namespace Octopus.Core.Features.ServerTasks.MessageContracts
{
    public class SimpleCommand: ICommand<SimpleCommand, SimpleResult>
    { }
    public class SimpleResult : IResponse { }
}
" + Common.IRequestIResponseDeclarations;

            var nameResult = new DiagnosticResult(CommandAndRequestTypesMustBeNamedCorrectlyAnalyzer.CommandResponseNameRule).WithSpan(5, 18, 5, 31);

            await Verify.VerifyAnalyzerAsync(source, nameResult);
        }
    }
}
