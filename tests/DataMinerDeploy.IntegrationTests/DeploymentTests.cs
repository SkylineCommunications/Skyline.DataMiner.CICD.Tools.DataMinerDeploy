namespace DataMinerDeploy.IntegrationTests;

using System.Diagnostics;
using System.Text;

using Skyline.DataMiner.CICD.Tools.WinEncryptedKeys.Lib;

[TestClass]
[TestCategory("IntegrationTest")]
public sealed class DeploymentTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task PowerShell_FromArtifact_InstallsBundledDmappOnNet10()
    {
        string directory = Path.GetDirectoryName(typeof(DeploymentTests).Assembly.Location)!;
        string results = Path.Combine(
            Environment.GetEnvironmentVariable("DATAMINER_DEPLOY_REGRESSION_RESULTS") ?? TestContext.TestResultsDirectory!,
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(results);
        string script = Path.Combine(directory, "Invoke-DeploymentRegression.ps1");
        string log = Path.Combine(results, "powershell.log");
        string summary = Path.Combine(results, "summary.json");
        string username = RetrieveCredential("QAOpsDataMinerUser");
        string password = RetrieveCredential("QAOpsDataMinerPassword");
        var output = new StringBuilder();
        var gate = new object();
        using var writer = new StreamWriter(log, false, new UTF8Encoding(false)) { AutoFlush = true };
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"System32\WindowsPowerShell\v1.0\powershell.exe"),
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{script}\" -ResultsDirectory \"{results}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = directory,
            },
        };
        // Credentials stay in the child process environment, never its command line or machine settings.
        process.StartInfo.EnvironmentVariables["DATAMINER_DEPLOY_USER"] = username;
        process.StartInfo.EnvironmentVariables["DATAMINER_DEPLOY_PASSWORD"] = password;
        // The QAOps host can inherit PowerShell 7 module paths, which break Windows PowerShell's built-in cmdlets.
        process.StartInfo.EnvironmentVariables["PSModulePath"] =
            Path.Combine(Path.GetDirectoryName(process.StartInfo.FileName)!, "Modules");
        void Capture(object sender, DataReceivedEventArgs args)
        {
            if (args.Data == null)
            {
                return;
            }

            string line = args.Data.Replace(password, "[REDACTED]").Replace(username, "[REDACTED]");
            lock (gate)
            {
                writer.WriteLine(line);
                output.AppendLine(line);
            }
        }

        process.OutputDataReceived += Capture;
        process.ErrorDataReceived += Capture;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        bool exited = await Task.Run(() => process.WaitForExit(25 * 60 * 1000));
        if (!exited)
        {
            process.Kill();
        }

        process.WaitForExit();
        writer.Flush();
        string diagnostics = output.ToString();
        TestContext.WriteLine(diagnostics);
        foreach (string file in Directory.GetFiles(results))
        {
            TestContext.AddResultFile(file);
        }

        Assert.IsTrue(exited, "Deployment PowerShell exceeded 25 minutes.\n" + diagnostics);
        Assert.AreEqual(0, process.ExitCode, "Deployment failed.\n" + diagnostics);
        Assert.IsTrue(File.Exists(summary), "Deployment did not write its verified result.\n" + diagnostics);
    }

    private static string RetrieveCredential(string name)
    {
        Assert.IsTrue(Keys.TryRetrieveKey(name, out string value), $"QAOps encrypted credential '{name}' is missing.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(value), $"QAOps encrypted credential '{name}' is empty.");
        return value;
    }
}
