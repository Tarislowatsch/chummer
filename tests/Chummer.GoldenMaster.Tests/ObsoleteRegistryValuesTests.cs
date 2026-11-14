using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Xunit;

namespace Chummer.GoldenMaster.Tests
{
	[Collection(AppHost.Collection)]
	public class ObsoleteRegistryValuesTests
	{
		// The key GlobalOptions reads, spelled out: the helper must not choose it.
		private const string ChummerKey = @"Software\Chummer";

		// Values the app still reads: the cleanup must leave them alone.
		private static readonly string[] LiveNames = { "language", "mru0", "pdfapppath" };

		[Fact]
		public void DeleteFromCurrentUserRemovesOnlyTheObsoleteValues()
		{
			AppHost.CreateVolatileCurrentUserKey(ChummerKey);
			try
			{
				using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ChummerKey, true))
				{
					foreach (string name in ObsoleteRegistryValues.Names.Concat(LiveNames))
						key.SetValue(name, "x");
				}

				ObsoleteRegistryValues.DeleteFromCurrentUser();

				using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ChummerKey))
					Assert.Equal(LiveNames.OrderBy(name => name), key.GetValueNames().OrderBy(name => name));
			}
			finally
			{
				Registry.CurrentUser.DeleteSubKeyTree("Software", false);
			}
		}

		// - a user who never changed a setting has no Chummer key
		// - blind to a CreateSubKey regression: the catch hides the sandbox's refusal
		[Fact]
		public void DeleteFromCurrentUserToleratesAMissingKey()
		{
			ObsoleteRegistryValues.DeleteFromCurrentUser();

			using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software"))
				Assert.Null(key);
		}

		// - a listed name that code still reads would wipe a live setting on every start
		// - raw text, comments included: a commented-out read fails too
		[Fact]
		public void NoOtherSourceFileMentionsAnObsoleteName()
		{
			string chummerDir = Path.Combine(Fixtures.RepoRoot, "Chummer");
			string[] files = Directory.GetFiles(chummerDir, "*.cs", SearchOption.AllDirectories)
				.Where(path => Path.GetFileName(path) != "clsObsoleteRegistryValues.cs")
				.ToArray();

			// An empty scan would pass vacuously.
			Assert.Contains(files, path => Path.GetFileName(path) == "clsOptions.cs");
			string[] mentions = files
				.Select(path => new { Name = Path.GetFileName(path), Text = File.ReadAllText(path) })
				.SelectMany(file => ObsoleteRegistryValues.Names
					.Where(name => file.Text.IndexOf("\"" + name + "\"", StringComparison.OrdinalIgnoreCase) >= 0)
					.Select(name => file.Name + ": " + name))
				.ToArray();
			Assert.Empty(mentions);
		}

		// A second start finds nothing to delete.
		[Fact]
		public void DeleteFromToleratesMissingValues()
		{
			AppHost.CreateVolatileCurrentUserKey(ChummerKey);
			try
			{
				using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ChummerKey, true))
				{
					ObsoleteRegistryValues.DeleteFrom(key);
					Assert.Empty(key.GetValueNames());
				}
			}
			finally
			{
				Registry.CurrentUser.DeleteSubKeyTree("Software", false);
			}
		}
	}
}
