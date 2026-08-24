using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dadstart.Labs.MediaForge.Analyzers;
using Dadstart.Labs.MediaForge.Cmdlets;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Dadstart.Labs.MediaForge.Tests.Analyzers;

public sealed class CmdletCatchBlockWriteErrorAnalyzerTests
{
    private static readonly string _cmdletUsings =
        """
        using System;
        using System.Management.Automation;
        using Microsoft.Extensions.Logging;
        """;

    [Fact]
    public void CatchBlock_WithLogErrorAndWriteError_ReportsNoDiagnostic()
    {
        var source =
            _cmdletUsings +
            """
            public class TestCmdlet : PSCmdlet
            {
                private ILogger Logger { get; } = null!;

                protected override void ProcessRecord()
                {
                    try
                    {
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "failed");
                        WriteError(new ErrorRecord(ex, "Failed", ErrorCategory.OperationStopped, null));
                    }
                }
            }
            """;

        Assert.Empty(GetDiagnostics(source));
    }

    [Fact]
    public void CatchBlock_WithLogErrorAndRethrow_ReportsNoDiagnostic()
    {
        var source =
            _cmdletUsings +
            """
            public class TestCmdlet : PSCmdlet
            {
                private ILogger Logger { get; } = null!;

                protected override void ProcessRecord()
                {
                    try
                    {
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "failed");
                        throw;
                    }
                }
            }
            """;

        Assert.Empty(GetDiagnostics(source));
    }

    [Fact]
    public void CatchBlock_WithLogErrorOnly_ReportsDiagnostic()
    {
        var source =
            _cmdletUsings +
            """
            public class TestCmdlet : PSCmdlet
            {
                private ILogger Logger { get; } = null!;

                protected override void ProcessRecord()
                {
                    try
                    {
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "failed");
                        WriteWarning(ex.Message);
                    }
                }
            }
            """;

        var diagnostic = Assert.Single(GetDiagnostics(source));
        Assert.Equal(CmdletCatchBlockWriteErrorAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Fact]
    public void CatchBlock_WithFilteredException_ReportsNoDiagnostic()
    {
        var source =
            _cmdletUsings +
            """
            public class TestCmdlet : PSCmdlet
            {
                private ILogger Logger { get; } = null!;

                protected override void ProcessRecord()
                {
                    try
                    {
                    }
                    catch (Exception ex) when (ex is InvalidOperationException)
                    {
                        Logger.LogError(ex, "failed");
                    }
                }
            }
            """;

        Assert.Empty(GetDiagnostics(source));
    }

    [Fact]
    public void CatchBlock_WithLogWarningOnly_ReportsNoDiagnostic()
    {
        var source =
            _cmdletUsings +
            """
            public class TestCmdlet : PSCmdlet
            {
                private ILogger Logger { get; } = null!;

                protected override void ProcessRecord()
                {
                    try
                    {
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "non-fatal");
                    }
                }
            }
            """;

        Assert.Empty(GetDiagnostics(source));
    }

    [Fact]
    public async Task MediaForgePS_CmdletSources_ComplyWithWriteErrorRule()
    {
        var repoRoot = FindRepoRoot();
        var cmdletsDirectory = Path.Combine(repoRoot, "src", "MediaForgePS", "Cmdlets");
        Assert.True(Directory.Exists(cmdletsDirectory), $"Cmdlet directory not found: {cmdletsDirectory}");

        var syntaxTrees = Directory.EnumerateFiles(cmdletsDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            "MediaForgePS.Cmdlets.Analysis",
            syntaxTrees,
            CreateCmdletReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = await RunAnalyzerAsync(compilation);
        Assert.Empty(diagnostics);
    }

    private static ImmutableArray<Diagnostic> GetDiagnostics(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "CmdletCatchBlockAnalyzerTests",
            new[] { syntaxTree },
            CreateSnippetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return RunAnalyzerAsync(compilation).GetAwaiter().GetResult();
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(Compilation compilation)
    {
        var analyzer = new CmdletCatchBlockWriteErrorAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        return diagnostics.Where(d => d.Id == CmdletCatchBlockWriteErrorAnalyzer.DiagnosticId).ToImmutableArray();
    }

    private static MetadataReference[] CreateSnippetReferences() => CreateCmdletReferences();

    private static MetadataReference[] CreateCmdletReferences()
    {
        var mediaForgeAssembly = typeof(CmdletBase).Assembly;
        var references = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            typeof(object).Assembly.Location,
            typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location,
            mediaForgeAssembly.Location
        };

        foreach (var reference in mediaForgeAssembly.GetReferencedAssemblies())
        {
            try
            {
                references.Add(System.Reflection.Assembly.Load(reference).Location);
            }
            catch
            {
            }
        }

        return references.Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MediaForgePS.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
