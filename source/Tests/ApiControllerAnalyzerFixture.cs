﻿using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using System;
using System.Threading.Tasks;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.RoslynAnalyzers.ApiControllerAnalyzer>;

namespace Tests
{
    public class ApiControllerAnalyzerFixture
    {
        [Test]
        public async Task NoDiagnosticsOnWellFormedRequest()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core
{
  public class GetFooController : ControllerBase {
    public GetFooResponse GetFoo(GetFooRequest request) => new();
  }
}");
            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task MustNotHaveSwaggerOperationAttribute()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.Core
{
  namespace _SwaggerOperation {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      [{|#0:SwaggerOperation(""some swagger thing"")|}]
      public GetFooResponse GetFoo(GetFooRequest request) => new();
    }
  }

  namespace _Experimental {
    [Experimental] // should not fire on experimental
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      [SwaggerOperation(""some swagger thing"")]
      public GetFooResponse GetFoo(GetFooRequest request) => new();
    }
  }

  namespace _NonPublic {
    // should not fire on nonpublic method
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      [SwaggerOperation(""some swagger thing"")]
      GetFooResponse GetFoo(GetFooRequest request) => new();
    }
  }
}");
            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(Descriptors.MustNotHaveSwaggerOperationAttribute).WithLocation(0));
        }

        [Test]
        public async Task MustNotReturnActionResults()
        {
            var source = WithOctopusTypes(@"
namespace Octopus.ApiControllerTests.Good {
  namespace _String {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public string GetFoo(GetFooRequest request) => ""some string"";
    }
  }
  namespace _Task_String {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public async Task<string> GetFoo(GetFooRequest request) {
        await Task.CompletedTask;
        return ""some string"";
      }
    }
  }
}

namespace Octopus.ApiControllerTests.Violations
{
  namespace _IActionResult {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public {|#0:IActionResult|} GetFoo(GetFooRequest request) => new ObjectResult();
    }
  }
  namespace _Task_IActionResult {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public async {|#1:Task<IActionResult>|} GetFoo(GetFooRequest request) {
        await Task.CompletedTask;
        return new ObjectResult();
      }
    }
  }

  namespace _ContentResult {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public {|#2:ContentResult|} GetFoo(GetFooRequest request) => new ContentResult();
    }
  }
    
  namespace _CreatedResult {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public {|#3:CreatedResult|} GetFoo(GetFooRequest request) => new CreatedResult();
    }
  }

  namespace _ConvertibleToActionResult {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public {|#4:ConvertibleToActionResult|} GetFoo(GetFooRequest request) => new ConvertibleToActionResult();
    }
  }
  namespace _Task_ConvertibleToActionResult {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public async {|#5:Task<ConvertibleToActionResult>|} GetFoo(GetFooRequest request) {
        await Task.CompletedTask;
        return new ConvertibleToActionResult();
      }
    }
  }
}

namespace Octopus.ApiControllerTests.Exempt {
  using Octopus.Server.Extensibility.Web.Extensions; // for CreatedResult<T>
  namespace _CreatedResult_T {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public CreatedResult<string> GetFoo(GetFooRequest request) => new CreatedResult<string>();
    }
  }
  namespace _Task_CreatedResult_T {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public Task<CreatedResult<string>> GetFoo(GetFooRequest request) => Task.FromResult(new CreatedResult<string>());
    }
  }

  namespace _AmplitudeOkResult {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public Octopus.Server.Web.Controllers.Telemetry.AmplitudeOkResult GetFoo(GetFooRequest request) => new();
    }
  }

  namespace _BlobResult {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public BlobResult GetFoo(GetFooRequest request) => new();
    }
  }

  namespace _CsvFileResult {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public CsvFileResult GetFoo(GetFooRequest request) => new();
    }
  }

  namespace _FileResult {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public FileResult GetFoo(GetFooRequest request) => new();
    }
  }

  namespace _FileContentResult {
    public class GetFooController : ControllerBase {
      [HttpGet(""/api/foos"")]
      public FileContentResult GetFoo(GetFooRequest request) => new();
    }
  }
}");
           
            await Verify.VerifyAnalyzerAsync(source,
                new DiagnosticResult(Descriptors.MustNotReturnActionResults).WithLocation(0),
                new DiagnosticResult(Descriptors.MustNotReturnActionResults).WithLocation(1),
                new DiagnosticResult(Descriptors.MustNotReturnActionResults).WithLocation(2),
                new DiagnosticResult(Descriptors.MustNotReturnActionResults).WithLocation(3),
                new DiagnosticResult(Descriptors.MustNotReturnActionResults).WithLocation(4),
                new DiagnosticResult(Descriptors.MustNotReturnActionResults).WithLocation(5)
            );
        }

        static readonly string ApiControllerTypeUsings = "using Microsoft.AspNetCore.Mvc;using Swashbuckle.AspNetCore.Annotations;using Octopus.Server.Web.Infrastructure;";

        static readonly string ApiControllerTypeDeclarations = @"
public class GetFooRequest : IRequest<GetFooRequest, GetFooResponse> { }
public class GetFooResponse : IResponse { }

namespace Microsoft.AspNetCore.Mvc {
  public interface IActionResult { }
  public abstract class ControllerBase { }

  public abstract class HttpMethodAttribute : Attribute { }
  public class HttpGetAttribute : HttpMethodAttribute {
    public HttpGetAttribute(string route) { } 
  }

  public abstract class ActionResult : IActionResult { }
  public class ContentResult : ActionResult { }
  public class ObjectResult : ActionResult { }
  public class CreatedResult : ObjectResult { }
  public class FileResult : ActionResult { }
  public class FileContentResult : FileResult { }

  namespace Infrastructure {
    public interface IConvertToActionResult { }
  }
}
namespace Octopus.Server.Web.Infrastructure {
  public class CsvFileResult : ActionResult { }
  public class BlobResult : FileResult { }

  public class ConvertibleToActionResult : Microsoft.AspNetCore.Mvc.Infrastructure.IConvertToActionResult { }
}
namespace Octopus.Server.Extensibility.Web.Extensions {
  public class CreatedResult<TResponse> : Microsoft.AspNetCore.Mvc.CreatedResult { }
}
namespace Octopus.Server.Web.Controllers.Telemetry {
  public class AmplitudeOkResult : ContentResult { }
}
namespace Swashbuckle.AspNetCore.Annotations {
  public class SwaggerOperationAttribute : Attribute 
  {
    public SwaggerOperationAttribute(string summary) : base() { }
  }
}
";

        static string WithOctopusTypes(string source) => $"{Common.Usings}{ApiControllerTypeUsings}{source}{Common.MessageTypeDeclarations}{ApiControllerTypeDeclarations}";
    }
}