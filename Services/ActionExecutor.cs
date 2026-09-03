using System;
using System.Diagnostics;
using System.IO;
using Quick_Sab.Models;

namespace Quick_Sab.Services
{
    /// <summary>Executes an ActionItem according to its type.</summary>
    public static class ActionExecutor
    {
        public static void Execute(ActionItem item)
        {
            if (item == null) return;
            var config = ConfigService.Current;
            var value = VariableResolver.Resolve(item.Value, config);
            var workDir = VariableResolver.Resolve(item.WorkingDirectory, config);

            switch (item.Type)
            {
                case ActionType.Share:
                    OpenShare(value);
                    break;

                case ActionType.Web:
                    OpenWeb(value);
                    break;

                case ActionType.Command:
                    RunCommand(value, workDir, item.KeepWindowOpen);
                    break;

                case ActionType.Script:
                    RunScript(value, workDir, item.KeepWindowOpen, item.ScriptShell);
                    break;
            }
        }

        private static void OpenShare(string path)
        {
            path = Environment.ExpandEnvironmentVariables(path.Trim());
            if (string.IsNullOrWhiteSpace(path)) return;

            if (File.Exists(path))
            {
                // A file: select it in Explorer
                Process.Start("explorer.exe", "/select,\"" + path + "\"");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + path + "\"",
                UseShellExecute = true
            });
        }

        private static void OpenWeb(string url)
        {
            url = url.Trim();
            if (string.IsNullOrWhiteSpace(url)) return;

            if (!url.Contains("://"))
                url = "https://" + url;

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        private static void RunCommand(string command, string workDir, bool keepOpen)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = (keepOpen ? "/k " : "/c ") + command,
                UseShellExecute = true
            };

            workDir = Environment.ExpandEnvironmentVariables(workDir ?? "").Trim();
            if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir))
                psi.WorkingDirectory = workDir;

            Process.Start(psi);
        }

        /// <summary>Writes the (already variable-resolved) script to a temporary .bat / .ps1 file and runs it.</summary>
        private static void RunScript(string content, string workDir, bool keepOpen, ScriptShell shell)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var ext = shell == ScriptShell.PowerShell ? ".ps1" : ".bat";
            var file = Path.Combine(Path.GetTempPath(), "quick_sab_script_" + Guid.NewGuid().ToString("N") + ext);
            var encoding = shell == ScriptShell.PowerShell
                ? (System.Text.Encoding)new System.Text.UTF8Encoding(true) // BOM so PowerShell reads accents correctly
                : System.Text.Encoding.Default;
            File.WriteAllText(file, content, encoding);

            ProcessStartInfo psi;
            if (shell == ScriptShell.PowerShell)
            {
                psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-ExecutionPolicy Bypass -NoLogo " + (keepOpen ? "-NoExit " : "") + "-File \"" + file + "\"",
                    UseShellExecute = true
                };
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = (keepOpen ? "/k " : "/c ") + "\"" + file + "\"",
                    UseShellExecute = true
                };
            }

            workDir = Environment.ExpandEnvironmentVariables(workDir ?? "").Trim();
            if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir))
                psi.WorkingDirectory = workDir;

            Process.Start(psi);
        }
    }
}
