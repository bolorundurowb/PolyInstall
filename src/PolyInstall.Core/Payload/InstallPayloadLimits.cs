namespace PolyInstall.Payload;

/// <summary>
/// Fail-closed resource limits for payload decompression and zip extraction. Installer
/// payloads are bundled by the (trusted) build pipeline, but an installer binary is itself
/// untrusted input to the machine running it — caps keep malformed or hostile bundles from
/// consuming unbounded memory or disk.
/// </summary>
public static class InstallPayloadLimits
{
    /// <summary>Maximum cumulative decompressed payload size (16 GiB).</summary>
    public const long MaxDecompressedPayloadBytes = 16L * 1024 * 1024 * 1024;

    /// <summary>Maximum number of entries in a payload zip archive.</summary>
    public const int MaxZipEntries = 200_000;

    /// <summary>
    /// Copies <paramref name="source"/> to <paramref name="destination"/>, throwing
    /// <see cref="InvalidDataException"/> once more than <paramref name="maxBytes"/> have
    /// been written. Returns the number of bytes copied.
    /// </summary>
    internal static long CopyWithLimit(
        Stream source,
        Stream destination,
        long maxBytes,
        CancellationToken ct = default)
    {
        var buffer = new byte[1024 * 128];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            total += read;
            if (total > maxBytes)
                throw new InvalidDataException(
                    $"Payload exceeds the maximum supported decompressed size of {maxBytes:N0} bytes.");
            destination.Write(buffer, 0, read);
        }

        return total;
    }
}
