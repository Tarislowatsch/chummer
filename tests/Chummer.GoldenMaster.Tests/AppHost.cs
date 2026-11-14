using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Chummer.GoldenMaster.Tests
{
	// - xUnit builds this before the first test class of the collection runs
	// - GlobalOptions reads the registry and StartupPath once, in its static constructor
	public sealed class AppHost
	{
		public const string Collection = "AppHost";
		public const string SandboxKey = @"Software\Chummer.GoldenMaster.Sandbox";

		public static string AppDirectory => AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

		public AppHost()
		{
			IsolateCurrentUserRegistry();
			RedirectStartupPath();
		}

		// - StartupPath is the test host's directory inside the dotnet SDK
		// - net48 is frozen: this private field cannot move
		private static void RedirectStartupPath()
		{
			FieldInfo field = typeof(Application).GetField("startupPath", BindingFlags.NonPublic | BindingFlags.Static);
			if (field == null)
				throw new InvalidOperationException("System.Windows.Forms.Application has no static field 'startupPath'.");
			field.SetValue(null, AppDirectory);
		}

		// - HKCU maps to an empty volatile key for this process only
		// - GlobalOptions' CreateSubKey fails there: a volatile key takes no non-volatile child
		// - the sandbox key itself stays in the real HKCU until logoff
		private static void IsolateCurrentUserRegistry()
		{
			IntPtr sandbox;
			int disposition;
			int rc = RegCreateKeyEx(HKEY_CURRENT_USER, SandboxKey, 0, null, REG_OPTION_VOLATILE, KEY_ALL_ACCESS, IntPtr.Zero, out sandbox, out disposition);
			if (rc != 0)
				throw new InvalidOperationException("RegCreateKeyEx failed with " + rc + ".");
			rc = RegOverridePredefKey(HKEY_CURRENT_USER, sandbox);
			if (rc != 0)
				throw new InvalidOperationException("RegOverridePredefKey failed with " + rc + ".");
		}

		// - the only way to give the sandbox a child key: see IsolateCurrentUserRegistry
		// - intermediate keys are created volatile as well
		public static void CreateVolatileCurrentUserKey(string subKey)
		{
			IntPtr key;
			int disposition;
			int rc = RegCreateKeyEx(HKEY_CURRENT_USER, subKey, 0, null, REG_OPTION_VOLATILE, KEY_ALL_ACCESS, IntPtr.Zero, out key, out disposition);
			if (rc != 0)
				throw new InvalidOperationException("RegCreateKeyEx failed with " + rc + ".");
			RegCloseKey(key);
		}

		private static readonly UIntPtr HKEY_CURRENT_USER = new UIntPtr(0x80000001u);
		private const int REG_OPTION_VOLATILE = 1;
		private const int KEY_ALL_ACCESS = 0xF003F;

		[DllImport("advapi32.dll")]
		private static extern int RegOverridePredefKey(UIntPtr hKey, IntPtr hNewHKey);

		[DllImport("advapi32.dll")]
		private static extern int RegCloseKey(IntPtr hKey);

		[DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
		private static extern int RegCreateKeyEx(UIntPtr hKey, string lpSubKey, int reserved, string lpClass, int dwOptions, int samDesired, IntPtr lpSecurityAttributes, out IntPtr phkResult, out int lpdwDisposition);
	}
}
