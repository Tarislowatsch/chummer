using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace Chummer
{
	// - leftovers of removed features: no code reads them any more
	// - omaepassword holds the Omae password in plain Base64
	public static class ObsoleteRegistryValues
	{
		private const string ChummerKey = "Software\\Chummer";

		public static IReadOnlyList<string> Names { get; } = new[]
		{
			"omaeusername",
			"omaepassword",
			"omaeautologin",
			"autoupdate",
			"localisedupdatesonly",
		};

		// - OpenSubKey, not CreateSubKey: a user without settings must not gain a key
		// - a failed cleanup must not stop the app from starting
		public static void DeleteFromCurrentUser()
		{
			try
			{
				using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ChummerKey, true))
				{
					if (key != null)
						DeleteFrom(key);
				}
			}
			catch (Exception ex) when (ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException)
			{
			}
		}

		public static void DeleteFrom(RegistryKey key)
		{
			foreach (string name in Names)
				key.DeleteValue(name, false);
		}
	}
}
