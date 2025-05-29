using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using Octopus.RoslynAnalyzers;
using Verify = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<Octopus.RoslynAnalyzers.LongRunningTaskWithAsyncWorkloadAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Tests
{
    public class LongRunningTaskWithAsyncWorkloadAnalyzerFixture
    {
        [Test]
        public async Task DetectsAsyncLambda()
        {
            var result = new DiagnosticResult(LongRunningTaskWithAsyncWorkloadAnalyzer.Rule)
                .WithSpan(11, 19, 11, 99);
            await Verify.VerifyAnalyzerAsync(AsyncLambda, result);
        }
        
        [Test]
        public async Task DetectsNonAsyncLambda()
        {
            var result = new DiagnosticResult(LongRunningTaskWithAsyncWorkloadAnalyzer.Rule)
                .WithSpan(11, 19, 11, 87);
            await Verify.VerifyAnalyzerAsync(NonAsyncLambda, result);
        }

        [Test]
        public async Task DetectsDelegate()
        {
            var result = new DiagnosticResult(LongRunningTaskWithAsyncWorkloadAnalyzer.Rule)
                .WithSpan(11, 19, 11, 79);
            await Verify.VerifyAnalyzerAsync(Delegate, result);
        }

        [Test]
        public async Task DetectsWithMultipleOptions()
        {
            var result = new DiagnosticResult(LongRunningTaskWithAsyncWorkloadAnalyzer.Rule)
                .WithSpan(11, 19, 11, 173);
            await Verify.VerifyAnalyzerAsync(MultipleOptions, result);
        }

        [Test]
        public async Task DetectsWithAliasedTypeNames()
        {
            var result = new DiagnosticResult(LongRunningTaskWithAsyncWorkloadAnalyzer.Rule)
                .WithSpan(12, 19, 12, 84);
            await Verify.VerifyAnalyzerAsync(AliasedTypeNames, result);
        }

        [Test]
        public async Task DetectsWithOptionsAsConstant()
        {
            var result = new DiagnosticResult(LongRunningTaskWithAsyncWorkloadAnalyzer.Rule)
                .WithSpan(12, 19, 12, 75);
            await Verify.VerifyAnalyzerAsync(OptionsAsConstant, result);
        }

        [Test]
        public async Task DetectsWithOverloadWithTaskScheduler()
        {
            var result = new DiagnosticResult(LongRunningTaskWithAsyncWorkloadAnalyzer.Rule)
                .WithSpan(12, 19, 12, 141);
            await Verify.VerifyAnalyzerAsync(OverloadWithTaskScheduler, result);
        }

        [Test]
        public async Task DetectsWithOverloadWithState()
        {
            var result = new DiagnosticResult(LongRunningTaskWithAsyncWorkloadAnalyzer.Rule)
                .WithSpan(11, 19, 11, 113);
            await Verify.VerifyAnalyzerAsync(OverloadWithState, result);
        }

        [Test]
        public async Task DetectsWithOverloadWithStateAndTaskScheduler()
        {
            var result = new DiagnosticResult(LongRunningTaskWithAsyncWorkloadAnalyzer.Rule)
                .WithSpan(12, 19, 12, 155);
            await Verify.VerifyAnalyzerAsync(OverloadWithStateAndTaskScheduler, result);
        }

        [Test]
        public async Task DoesNotReportWithOptionsAsVariable()
        {
            await Verify.VerifyAnalyzerAsync(OptionsAsVariable);
        }

        [Test]
        public async Task DoesNotReportWhenNotLongRunning()
        {
            await Verify.VerifyAnalyzerAsync(NotLongRunning);
        }

        [Test]
        public async Task DoesNotReportGoodUsage()
        {
            await Verify.VerifyAnalyzerAsync(GoodUsage);
        }

        [Test]
        public async Task DoesNotReportIrrelevantMethods()
        {
            await Verify.VerifyAnalyzerAsync(SomeOtherMethodCalledStartNew);
        }

        const string AsyncLambda = @"
using System;
using System.Threading.Tasks;

namespace TheNamespace
{
    public class FooBar
    {
        public async Task Baz()
        {
            await Task.Factory.StartNew(async () => await Blah(), TaskCreationOptions.LongRunning);
        }

        async Task Blah()
        {
            await Task.CompletedTask;
        }
    }
}
";

        const string NonAsyncLambda = @"
using System;
using System.Threading.Tasks;

namespace TheNamespace
{
    public class FooBar
    {
        public async Task Baz()
        {
            await Task.Factory.StartNew(() => Blah(), TaskCreationOptions.LongRunning);
        }

        async Task Blah()
        {
            await Task.CompletedTask;
        }
    }
}
";

        const string Delegate = @"
using System;
using System.Threading.Tasks;

namespace TheNamespace
{
    public class FooBar
    {
        public async Task Baz()
        {
            await Task.Factory.StartNew(Blah, TaskCreationOptions.LongRunning);
        }

        async Task Blah()
        {
            await Task.CompletedTask;
        }
    }
}
";

        const string MultipleOptions = @"
using System;
using System.Threading.Tasks;

namespace TheNamespace
{
    public class FooBar
    {
        public async Task Baz()
        {
            await Task.Factory.StartNew(async () => await Blah(), TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach | TaskCreationOptions.HideScheduler);
        }

        async Task Blah()
        {
            await Task.CompletedTask;
        }
    }
}
";

        const string AliasedTypeNames = @"
using System;
using Tasky = System.Threading.Tasks.Task;
using TCO = System.Threading.Tasks.TaskCreationOptions;

namespace TheNamespace
{
    public class FooBar
    {
        public async Tasky Baz()
        {
            await Tasky.Factory.StartNew(async () => await Blah(), TCO.LongRunning);
        }

        async Tasky Blah()
        {
            await Tasky.CompletedTask;
        }
    }
}
";

        const string OptionsAsConstant = @"
using System;
using System.Threading.Tasks;

namespace TheNamespace
{
    public class FooBar
    {
        public async Task Baz()
        {
            const TaskCreationOptions options = TaskCreationOptions.LongRunning;
            await Task.Factory.StartNew(async () => await Blah(), options);
        }

        async Task Blah()
        {
            await Task.CompletedTask;
        }
    }
}
";

        const string OptionsAsVariable = @"
using System;
using System.Threading.Tasks;

namespace TheNamespace
{
    public class FooBar
    {
        public async Task Baz()
        {
            var options = TaskCreationOptions.LongRunning;
            await Task.Factory.StartNew(async () => await Blah(), options);
        }

        async Task Blah()
        {
            await Task.CompletedTask;
        }
    }
}
";

        const string OverloadWithTaskScheduler = @"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TheNamespace
{
    public class FooBar
    {
        public async Task Baz(CancellationToken cancellationToken)
        {
            await Task.Factory.StartNew(async () => await Blah(), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        async Task Blah()
        {
            await Task.CompletedTask;
        }
    }
}
";

        const string OverloadWithState = @"
using System;
using System.Threading.Tasks;

namespace TheNamespace
{
    public class FooBar
    {
        public async Task Baz()
        {
            await Task.Factory.StartNew(async x => await Blah(x), new object(), TaskCreationOptions.LongRunning);
        }

        async Task Blah(object state)
        {
            await Task.CompletedTask;
        }
    }
}
";

        const string OverloadWithStateAndTaskScheduler = @"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TheNamespace
{
    public class FooBar
    {
        public async Task Baz(CancellationToken cancellationToken)
        {
            await Task.Factory.StartNew(async x => await Blah(x), new object(), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        async Task Blah(object state)
        {
            await Task.CompletedTask;
        }
    }
}
";

        const string NotLongRunning = @"
using System;
using System.Threading.Tasks;

namespace TheNamespace
{
    public class FooBar
    {
        public async Task Baz()
        {
            await Task.Factory.StartNew(async () => await Blah(), TaskCreationOptions.DenyChildAttach);
        }

        async Task Blah()
        {
            await Task.CompletedTask;
        }
    }
}
";

        const string GoodUsage = @"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TheNamespace
{
    public class FooBar
    {
        public async Task Baz()
        {
            await Task.Factory.StartNew(() => Blah(), TaskCreationOptions.LongRunning);
        }

        void Blah()
        {
            Thread.Sleep(10000);
        }
    }
}
";

        const string SomeOtherMethodCalledStartNew = @"
using System;

namespace TheNamespace
{
    public class TaskFactory
    {
        public void StartNew()
        {
        }
    }

    public class FooBar
    {
        public void Baz()
        {
            var factory = new TaskFactory();
            factory.StartNew();
        }
    }
}
";
    }
}