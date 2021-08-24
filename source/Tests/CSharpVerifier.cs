using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace Tests
{
    public class CSharpVerifier<TAnalyzer> where TAnalyzer : DiagnosticAnalyzer, new()
    {
        public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
        {
            var test = new Test<TAnalyzer>
            {
                TestCode = source
            };

            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync();
        }
    }

    public class Test<TAnalyzer> : CSharpCodeFixTest<TAnalyzer, EmptyCodeFixProvider, NUnitVerifier> where TAnalyzer : DiagnosticAnalyzer, new()
    {
        public Test()
        {
            ReferenceAssemblies = ReferenceAssemblies.Default.AddPackages(
                ImmutableArray.Create(
                    new PackageIdentity("xunit.core", "2.4.1")
                )
            );

            TestBehaviors |= TestBehaviors.SkipGeneratedCodeCheck;
        }
    }
}