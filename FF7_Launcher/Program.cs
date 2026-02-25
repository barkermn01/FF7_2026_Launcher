using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace FF7_Launcher;

internal static class Program
{
	private static Mutex InvalidationMultipleExecution;

	public static string Title => "FINAL FANTASY VII LAUNCHER";

	public static bool IsAlreadyRunning()
	{
		InvalidationMultipleExecution = new Mutex(initiallyOwned: false, "FF7_LauncherCS");
		ComponentResourceManager componentResourceManager = new ComponentResourceManager(typeof(FF7Launcher));
		if (!InvalidationMultipleExecution.WaitOne(0, exitContext: false))
		{
			MessageBox.Show(componentResourceManager.GetString("AlreadyRunning"), Title);
			return true;
		}
		return false;
	}

	[STAThread]
	private static void Main()
	{
		try
		{
			Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
			if (!IsAlreadyRunning())
			{
				string[] args = Environment.GetCommandLineArgs();
				bool disableSkip = args.Any(a => a.Equals("--disable-skip", StringComparison.OrdinalIgnoreCase));
				FF7ConfigFile tempConfig = new FF7ConfigFile();
				tempConfig.Read("config.txt");
				if (!disableSkip && tempConfig.BypassLauncher == 1)
				{
					string seventhHeavenPath = Get7thHeavenPath();
					if (tempConfig.Launch7thHeaven == 1 && seventhHeavenPath != null)
					{
						Process.Start(new ProcessStartInfo(seventhHeavenPath) { UseShellExecute = true });
					}
					else
					{
						Process.Start(new ProcessStartInfo("FFVII.exe") { UseShellExecute = true });
					}
					return;
				}
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(defaultValue: false);
				Application.Run(new FF7Launcher());
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show(ex.ToString(), Title);
		}
	}

	private static string Get7thHeavenPath()
	{
		try
		{
			using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{E66AE545-C285-4B8C-8BD0-67282E160BF4}_is1"))
			{
				if (key != null)
				{
					string installLocation = key.GetValue("InstallLocation") as string;
					if (!string.IsNullOrEmpty(installLocation))
					{
						string exePath = Path.Combine(installLocation, "7th Heaven.exe");
						if (File.Exists(exePath))
						{
							return exePath;
						}
					}
				}
			}
		}
		catch { }
		return File.Exists("7thHeaven.exe") ? "7thHeaven.exe" : null;
	}
}
