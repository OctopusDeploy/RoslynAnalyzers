using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Verify = Tests.CSharpVerifier<Octopus.RoslynAnalyzers.Testing.Integration.IntegrationTestForwardCancellationTokenToInvocationsAnalyzer>;

namespace Tests.Testing.Integration
{
    public class IntegrationTestForwardCancellationTokenToInvocationsAnalyzerFixture
    {
        [TestCase]
        public async Task No_Diagnostic_ClassesThatHaveNothingToDoWithThisAnalyser()
        {
            const string container = @"
public class JustAClassSittingAroundDoingItsThing
{
    async Task M()
    {
        await {|#0:MethodAsync|}();        
    }

    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;

}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }
        
        [TestCase]
        public async Task No_Diagnostic_NoCancellationTokenSupport()
        {
            const string container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        await {|#0:MethodAsync|}();
    }

    Task MethodAsync() => Task.CompletedTask;
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task No_Diagnostic_CancellationTokenIsAlreadyForwarded()
        {
            const string container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        await {|#0:MethodAsync|}(CancellationToken);
    }
    
    Task MethodAsync(CancellationToken ct) => Task.CompletedTask;
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task No_Diagnostic_ExplicitDefaultCancellationToken()
        {
            const string container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        await {|#0:MethodAsync|}(default);
    }
    
    Task MethodAsync(CancellationToken ct) => Task.CompletedTask;
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task No_Diagnostic_ExplicitNoneCancellationToken()
        {
            const string container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        await {|#0:MethodAsync|}(CancellationToken.None);
    }
    
    Task MethodAsync(CancellationToken ct) => Task.CompletedTask;
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task No_Diagnostic_OverloadWithMultipleCancellationTokenParameters()
        {
            const string container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        await {|#0:MethodAsync|}();
    }
    
    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken ct1, CancellationToken ct2) => Task.CompletedTask;
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }

        [TestCase]
        public async Task No_Diagnostic_CancellationTokenIsNotLastParameterOnOverload()
        {
            const string container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        await {|#0:MethodAsync|}(""any"", ""thing"");
    }

    Task MethodAsync(string a, string b) => Task.CompletedTask;
    Task MethodAsync(string a, CancellationToken ct, string b) => Task.CompletedTask;
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }
        
        [TestCase]
        public async Task No_Diagnostic_OverloadShouldHaveExactParameterSequencePlusCancellationToken()
        {
            const string container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        await {|#0:MethodAsync|}(""any"");
    }

    Task MethodAsync(string a) => Task.CompletedTask;
    Task MethodAsync(string a, string b, CancellationToken ct) => Task.CompletedTask;
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }
        
        [TestCase]
        public async Task No_Diagnostic_OverloadShouldHaveExactParameterTypePlusCancellationToken()
        {
            const string container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        await {|#0:MethodAsync|}(""any"");
    }

    Task MethodAsync(string a) => Task.CompletedTask;
    Task MethodAsync(int a, CancellationToken ct) => Task.CompletedTask;
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }
        
        [TestCase]
        public async Task No_Diagnostic_TokenForwardedWithUnorderedNamedParameter()
        {
            const string container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        await {|#0:MethodAsync|}(ct: CancellationToken, a: ""any"");
    }

    Task MethodAsync(string a) => Task.CompletedTask;
    Task MethodAsync(string a, CancellationToken ct) => Task.CompletedTask;
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }
        
        [TestCase]
        public async Task No_Diagnostic_ExtensionMethodTakesTokenAsync()
        {
            const string container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        await {|#0:new MyClass()|}.MyMethod(""any"");
    }
}
public class MyClass
{
    public Task MyMethod(string a) => Task.CompletedTask;
}
public static class Extensions
{
    public static Task MyMethod(this MyClass mc, CancellationToken c) => Task.CompletedTask;
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }
        
        [TestCase]
        public async Task No_Diagnostic_StaticMethodBoundary()
        {
            const string container = @"
public class TestClass : IntegrationTest
{
    public static async Task M()
    {
        await InnerMethod();                

        Task InnerMethod(CancellationToken ct = default) => Task.CompletedTask;
    }
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }
        
        [TestCase]
        public async Task No_Diagnostic_OverloadReturnTypeDiffers()
        {
            const string container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        await {|#0:Method|}(a: ""any"");
    }

    Task Method(string a) => Task.CompletedTask;
    int Method(string a, CancellationToken ct) { throw new NotImplementedException(); }
}
";
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes());
        }
        
        [TestCase]
        public async Task Diagnostic_MethodWithCancellationTokenOverload()
        {
            var container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        await {|#0:MethodAsync|}();        
    }

    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;

}
";
            var result = new DiagnosticResult(Descriptors.Oct2008IntegrationTestForwardCancellationTokenToInvocations()).WithLocation(0);
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(), result);
        }
        
        [TestCase]
        public async Task Diagnostic_TaskStoredAsVariable()
        {
            var container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        Task t = {|#0:MethodAsync|}();
        await t;        
    }

    Task MethodAsync() => Task.CompletedTask;
    Task MethodAsync(CancellationToken c) => Task.CompletedTask;

}
";
            var result = new DiagnosticResult(Descriptors.Oct2008IntegrationTestForwardCancellationTokenToInvocations()).WithLocation(0);
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(), result);
        }

        [TestCase]
        public async Task Diagnostic_ExternalMethodWithCancellationTokenOverload()
        {
            var container = @"
public class ExternalReference
{
    public Task MethodAsync() => Task.CompletedTask;
    public Task MethodAsync(CancellationToken c) => Task.CompletedTask;
}

public class TestClass : IntegrationTest
{
    async Task M()
    {
        var ext = new ExternalReference();
        await {|#0:ext.MethodAsync|}();        
    }
}
";
            var result = new DiagnosticResult(Descriptors.Oct2008IntegrationTestForwardCancellationTokenToInvocations()).WithLocation(0);
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(), result);
        }

        [TestCase]
        public async Task Diagnostic_OverloadForOptionalParameters()
        {
            const string container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        await {|#0:MethodAsync|}(""any"");
        await {|#1:MethodAsync|}(""any"", ""thing"", ""again"");
    }

    Task MethodAsync(string a, string b = null, Object c = null) => Task.CompletedTask;
    Task MethodAsync(string a, CancellationToken ct) => Task.CompletedTask;    
    Task MethodAsync(string a, string b, Object c, CancellationToken ct) => Task.CompletedTask;
}
";
            var result0 = new DiagnosticResult(Descriptors.Oct2008IntegrationTestForwardCancellationTokenToInvocations()).WithLocation(0);
            var result1 = new DiagnosticResult(Descriptors.Oct2008IntegrationTestForwardCancellationTokenToInvocations()).WithLocation(1);
            
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(), result0, result1);
        }
        
        [TestCase]
        public async Task Diagnostic_UnorderedNamedParameters()
        {
            var container = @"
public class TestClass : IntegrationTest
{
    async Task M()
    {
        await {|#0:MethodAsync|}(z: ""Hello world"", x: 5, y: true);            
    }

    Task MethodAsync(int x, bool y = default, string z = """") => Task.CompletedTask;
    Task MethodAsync(int x, bool y = default, string z = """", CancellationToken c = default) => Task.CompletedTask;
}
";
            var result = new DiagnosticResult(Descriptors.Oct2008IntegrationTestForwardCancellationTokenToInvocations()).WithLocation(0);
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(), result);
        }
        
        [TestCase]
        public async Task Diagnostic_LocalStaticMethod()
        {
            // This will be a surprise, as directly passing CancellationToken (from IntegrationTest) is not a valid code (static context).
            // The intention is to notify user and user should fix by adding CancellationToken parameter on the static method. 
            var container = @"
public class TestClass : IntegrationTest
{
    public static Task MethodAsync(int i, CancellationToken c = default) => Task.CompletedTask;
    public async Task M()
    {
        await LocalStaticMethod();
        static Task LocalStaticMethod()
        {
            return {|#0:MethodAsync|}(5);
        }
    }
}
";
            var result = new DiagnosticResult(Descriptors.Oct2008IntegrationTestForwardCancellationTokenToInvocations()).WithLocation(0);
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(), result);
        }
        
        [TestCase]
        public async Task Diagnostic_ExternalGenericTypeMethod()
        {
            // This will be a surprise, as directly passing CancellationToken (from IntegrationTest) is not a valid code (static context).
            // The intention is to notify user and user should fix by adding CancellationToken parameter on the static method. 
            var container = @"

public class AccountResource {}
public class BasicRepository<TResource> where TResource : class
{
    public async Task<TResource> Get(string idOrHref) { throw new NotImplementedException(); } 
    public async Task<TResource> Get(string idOrHref, CancellationToken cancellationToken) { throw new NotImplementedException(); }
}

public class TestClass : IntegrationTest
{
    public async Task M(BasicRepository<AccountResource> repository)
    {
        var accountResource = await {|#0:repository.Get|}(""id""); 
    }
}
";
            var result = new DiagnosticResult(Descriptors.Oct2008IntegrationTestForwardCancellationTokenToInvocations()).WithLocation(0);
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(), result);
        }
        
        [TestCase]
        public async Task Diagnostic_GenericWhereTypeIsErased()
        {
            // This will be a surprise, as directly passing CancellationToken (from IntegrationTest) is not a valid code (static context).
            // The intention is to notify user and user should fix by adding CancellationToken parameter on the static method. 
            var container = @"

public class Reader
{
    public Task<T> Read<T>(int i) => Task.FromResult(default(T));
    public Task<T> Read<T>(int i, CancellationToken ct = default) => Task.FromResult(default(T));
}

public class TestClass : IntegrationTest
{
    public async Task<int> M(Reader reader)
    {
        return await {|#0:reader.Read<int>|}(1); 
    }
}
";
            var result = new DiagnosticResult(Descriptors.Oct2008IntegrationTestForwardCancellationTokenToInvocations()).WithLocation(0);
            await Verify.VerifyAnalyzerAsync(container.WithTestingTypes(), result);
        }
    }
}