using System.CommandLine;
using Crap4DotNet.Cli.Commands;

namespace Crap4DotNet.Cli;

internal sealed class Program
{
    static int Main(string[] args)
    {
        var rootCommand = new RootCommand("CRAP metric analysis tool for .NET")
        {
            AnalyzeCommand.Create(),
            DiffCommand.Create()
        };

        rootCommand.Invoke(args);
        return Environment.ExitCode;
    }
}
