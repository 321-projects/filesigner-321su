using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace FileSigner321
{
    internal sealed class SignedFile
    {
        public SignedFile(string path, Func<string> status)
        {
            Path = path;
            Status = status;
        }

        public string Path { get; private set; }
        public Func<string> Status { get; private set; }
    }

    internal static class FileSigning
    {
        private static readonly HashSet<string> Extensions = new HashSet<string>(
            new[] { ".ps1", ".psm1", ".psd1", ".exe", ".dll", ".msi", ".cab", ".cat", ".rdp" },
            StringComparer.OrdinalIgnoreCase);

        public static bool Supports(string path)
        {
            return Extensions.Contains(Path.GetExtension(path));
        }

        public static SignedFile SignCopy(string source, X509Certificate2 certificate)
        {
            if (!Supports(source)) throw new NotSupportedException("Этот тип файла не поддерживается.");
            var stagingDirectory = Path.Combine(Path.GetTempPath(), "321su-FileSigner-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stagingDirectory);
            string staged = Path.Combine(stagingDirectory, Path.GetFileName(source));
            try
            {
                CopyForSigning(source, staged, false);
                Func<string> status = Path.GetExtension(source).Equals(".rdp", StringComparison.OrdinalIgnoreCase)
                    ? SignRdp(source, staged, certificate)
                    : NativeSigning.SignAuthenticode(staged, certificate);
                string destination = NextOutputPath(source);
                File.Move(staged, destination);
                return new SignedFile(destination, status);
            }
            finally
            {
                // The staging directory belongs to this single operation.
                if (File.Exists(staged)) File.Delete(staged);
                Directory.Delete(stagingDirectory);
            }
        }

        private static string NextOutputPath(string original)
        {
            string folder = Path.GetDirectoryName(original);
            string stem = Path.GetFileNameWithoutExtension(original);
            string extension = Path.GetExtension(original);
            for (int index = 1; ; index++)
            {
                string suffix = index == 1 ? "-signed" : "-signed-" + index;
                string candidate = Path.Combine(folder, stem + suffix + extension);
                if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
            }
        }

        private static void CopyForSigning(string source, string target, bool overwrite)
        {
            File.Copy(source, target, overwrite);
            var file = new FileInfo(target);
            if (file.IsReadOnly) file.IsReadOnly = false;
        }

        private static Func<string> SignRdp(string source, string target, X509Certificate2 certificate)
        {
            string executable = Path.Combine(Environment.SystemDirectory, "rdpsign.exe");
            if (!File.Exists(executable)) throw new FileNotFoundException("Windows не нашла rdpsign.exe.", executable);
            string sha256Thumbprint;
            using (var sha256 = SHA256.Create())
                sha256Thumbprint = BitConverter.ToString(sha256.ComputeHash(certificate.RawData)).Replace("-", "");

            // Use the usual Windows store thumbprint; fall back to the SHA-256 certificate digest.
            var errors = new List<string>();
            foreach (string thumbprint in new[] { certificate.Thumbprint, sha256Thumbprint }.Distinct())
            {
                CopyForSigning(source, target, true);
                var arguments = string.Format("/sha256 {0} /v \"{1}\"", thumbprint, target);
                using (var tool = Process.Start(new ProcessStartInfo(executable, arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }))
                {
                    if (tool == null) throw new InvalidOperationException("Не удалось запустить rdpsign.exe.");
                    Task<string> output = tool.StandardOutput.ReadToEndAsync();
                    Task<string> error = tool.StandardError.ReadToEndAsync();
                    if (!tool.WaitForExit(45000))
                    {
                        if (!tool.HasExited) { tool.Kill(); tool.WaitForExit(); }
                        throw new TimeoutException("rdpsign.exe не завершился за 45 секунд.");
                    }
                    Task.WaitAll(output, error);
                    if (tool.ExitCode == 0 && File.ReadLines(target).Any(line =>
                        line.StartsWith("signature:s:", StringComparison.OrdinalIgnoreCase) && line.Length > 12))
                        return () => UiText.Get("Подписан через rdpsign");
                    errors.Add(string.Format("rdpsign ({0}): {1} {2}", tool.ExitCode, output.Result.Trim(), error.Result.Trim()));
                }
            }
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }
}
