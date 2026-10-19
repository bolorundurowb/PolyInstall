using System.Runtime.InteropServices;
using PolyInstall.Hosting;

namespace PolyInstall.Runtime.Silent;

internal static class Program
{
    private static int Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        PosixSignalRegistration? sigTerm = null;
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => cts.Cancel());
            }
            catch
            {
                // Best-effort; CancelKeyPress still covers interactive runs.
            }
        }

        try
        {
            using var session = InstallerHost.Open();
            return InstallerHost.RunSilent(session, args, cts.Token);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return SilentExitCodes.UnexpectedError;
        }
        finally
        {
            sigTerm?.Dispose();
        }
    }
}
