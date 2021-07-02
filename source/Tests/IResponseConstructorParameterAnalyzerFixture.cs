using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.NUnit.AnalyzerVerifier<Octopus.RoslynAnalyzers.IResponseConstructorParameterAnalyzer>;

namespace Tests
{
    public class IResponseConstructorParameterAnalyzerFixture
    {
        [Test]
        public async Task DetectsClassesThatImplementIResponseAndPassModelTypeToCtor()
        {
            var source = GetInvalidSource();
            var result = new[]
            {
                new DiagnosticResult(IResponseConstructorParameterAnalyzer.Rule).WithLocation(0),
                new DiagnosticResult(IResponseConstructorParameterAnalyzer.Rule).WithLocation(1)
            };
            await Verify.VerifyAnalyzerAsync(source, result);
        }

        [Test]
        public async Task NonRelevantCodeIsIgnore()
        {
            var source = GetNonRelevantSource();
            await Verify.VerifyAnalyzerAsync(source);
        }

        [Test]
        public async Task IgnoresValidConstructorParameters()
        {
            var source = GetValidSource();
            await Verify.VerifyAnalyzerAsync(source);
        }

        static string GetInvalidSource()
        {
            return @"
using System;

namespace Octopus.Core.Model 
{
    class SomeModel
    {
        public SomeModel() { }
    }
}

namespace TheNamespace
{
    using Octopus.Core.Model;

    public interface IResponse { }

    class TheType : IResponse
    {
        public TheType(string param1, {|#0:SomeModel model|}, int params3, {|#1:SomeModel modelTwo|}) 
        {     
        }
    }        
}
";
        }

        static string GetValidSource()
        {
            return @"
using System;

namespace Octopus 
{
    class SomeType
    {
        public SomeType() { }
    }
}

namespace TheNamespace
{

    public interface IResponse { }

    class TheType : IResponse
    {
        public TheType(string param1, Octopus.SomeType someType, int param3)
        {     
        }
       
    }        
}
";
        }

        static string GetNonRelevantSource()
        {
            return @"
using System;

namespace Octopus 
{
    class SomeType
    {
        public SomeType() { }
    }
}

namespace TheNamespace
{
    class TheType
    {
        public TheType(Octopus.SomeType SomeType) 
        {     
        }
       
    }     
    
    class OtherType
    {
    }
   
}
";
        }
    }
}