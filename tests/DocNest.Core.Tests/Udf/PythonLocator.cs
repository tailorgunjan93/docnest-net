using System;
using System.Diagnostics;

namespace DocNest.Tests.Udf;

/// <summary>Best-effort locator + runner for a host Python, used by the guarded cross-runtime test.</summary>
internal static class PythonLocator
{
    public static string? Find()
    {
        foreach (var candidate in new[] { "py", "python", "python3" })
        {
            try
            {
                var psi = new ProcessStartInfo(candidate, "--version")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(psi);
                if (process is null)
                {
                    continue;
                }
                if (!process.WaitForExit(5000))
                {
                    TryKill(process);
                    continue;
                }
                if (process.ExitCode == 0)
                {
                    return candidate;
                }
            }
            catch (Exception)
            {
                // Not installed / not on PATH — try the next candidate.
            }
        }
        return null;
    }

    public static (bool Ok, string Output) TryRun(string exe, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in args)
            {
                psi.ArgumentList.Add(a);
            }

            using var process = Process.Start(psi);
            if (process is null)
            {
                return (false, "could not start python");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(30000))
            {
                TryKill(process);
                return (false, "python timed out");
            }

            return process.ExitCode == 0 ? (true, stdout) : (false, stderr);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static void TryKill(Process process)
    {
        try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
    }
}
