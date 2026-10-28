using System;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
internal static class Launcher {
 [STAThread] static void Main() {
  try {
   string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QuestlioDesktop");
   if (!Directory.Exists(root)) root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "desktop");
   string version = File.ReadAllText(Path.Combine(root, "latest.txt")).Trim();
   if (!System.Text.RegularExpressions.Regex.IsMatch(version, @"^release-[0-9]+(\.[0-9]+)*$")) throw new Exception("Invalid release name.");
   string executable = Path.Combine(root, version, "Questlio.exe");
   if (!File.Exists(executable)) throw new FileNotFoundException("Questlio executable was not found.", executable);
   Process.Start(new ProcessStartInfo(executable) { Arguments = Array.IndexOf(Environment.GetCommandLineArgs(), "--background") >= 0 ? "--background" : "", WorkingDirectory = Path.GetDirectoryName(executable), UseShellExecute = true });
  } catch (Exception ex) { MessageBox.Show(ex.Message, "Questlio", MessageBoxButtons.OK, MessageBoxIcon.Error); }
 }
}
