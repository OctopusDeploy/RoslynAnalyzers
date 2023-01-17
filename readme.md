# Roslyn analyzers

## Purpose

This repository stores roslyn analyzers we use at Octopus Deploy which are shared across multiple projects.

For analyzers targeted at the Octopus Server codebase, please do not add them here. Rather, refer to OctopusDeploy/source/Octopus.Server.RoslynAnalyzers

## Doco

Each analyzer provides it's own documentation that can be found here:

- [Unwanted Method Calls Analyzer](source/UnwantedMethodCallsAnalyzer/Readme.md)

## Creating your own Analyzer

1. Follow the instructions in [this blog post](https://devblogs.microsoft.com/dotnet/how-to-write-a-roslyn-analyzer/) to setup your Visual Studio.
   - [Roslyn analyzers documentation](https://github.com/dotnet/roslyn/tree/master/docs/analyzers)
   - [Example analyzers](https://github.com/dotnet/roslyn-sdk/tree/master/samples/CSharp)
2. Test your nuget package analyzer against a project locally
3. Create a build pipeline to publish your analyzer nuget package
4. Update this readme to point to your analyzers root readme file
5. Let others know it exists!
