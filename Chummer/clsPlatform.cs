using System;
using System.Diagnostics;
using System.IO;

namespace Chummer
{
	/// <summary>
	/// What the application needs to know about the runtime it is hosted on.
	/// </summary>
	public static class Platform
	{
		/// <summary>
		/// True when running on Mono (Linux, macOS).
		/// </summary>
		public static readonly bool IsMono = Type.GetType("Mono.Runtime") != null;

		/// <summary>
		/// True on macOS.
		/// </summary>
		// - Mono reports PlatformID.Unix on macOS too: the system folder tells them apart
		public static bool IsMacOS
		{
			get { return Environment.OSVersion.Platform == PlatformID.Unix && Directory.Exists("/System/Library/CoreServices"); }
		}

		/// <summary>
		/// Open a file with the desktop's default application (web browser for .htm).
		/// </summary>
		/// <param name="strPath">Absolute path of the file to open.</param>
		public static void OpenWithDefaultApplication(string strPath)
		{
			if (Environment.OSVersion.Platform != PlatformID.Unix)
			{
				Process.Start(strPath);
				return;
			}

			ProcessStartInfo objInfo = new ProcessStartInfo(IsMacOS ? "open" : "xdg-open");
			objInfo.Arguments = "\"" + strPath.Replace("\"", "\\\"") + "\"";
			objInfo.UseShellExecute = false;
			try
			{
				Process.Start(objInfo);
			}
			catch (System.ComponentModel.Win32Exception)
			{
				// No opener installed: show the path instead.
				System.Windows.Forms.MessageBox.Show(strPath, objInfo.FileName + " not found");
			}
		}
	}
}
