using System.Diagnostics;

namespace PolyInstall.Pal;

internal interface ICommandRunner
{
    int Run(string fileName, IEnumerable<string> arguments, bool throwOnError = true);
}

internal sealed class CommandRunner : ICommandRunner
{
    public int Run(string fileName, IEnumerable<string> arguments, bool throwOnError = true)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)
                            ?? throw new InvalidOperationException($"Failed to start '{fileName}'.");
        process.WaitForExit();

        if (throwOnError && process.ExitCode != 0)
        {
            var command = string.Join(" ", new[] { fileName }.Concat(arguments));
            throw new InvalidOperationException($"Command failed with exit code {process.ExitCode}: {command}");
        }

        return process.ExitCode;
    }
}
