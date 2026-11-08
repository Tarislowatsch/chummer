using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using Xunit;

namespace Chummer.GoldenMaster.Tests
{
	public class AppHostTests
	{
		[Fact]
		public void StartupPathIsTheTestOutputDirectory()
		{
			AppHost.Ensure();
			Assert.Equal(AppHost.AppDirectory, Application.StartupPath);
		}

		// Without this content the app silently loads nothing.
		[Theory]
		[InlineData(@"data\gear.xml")]
		[InlineData(@"lang\en-us.xml")]
		[InlineData(@"settings\default.xml")]
		[InlineData(@"data\custom_10mercs_traditions.xml")]
		public void RuntimeContentReachedTheOutputDirectory(string relativePath)
		{
			Assert.True(File.Exists(Path.Combine(AppHost.AppDirectory, relativePath)), relativePath);
		}

		// Every real profile has HKCU\Software: only the sandbox hides it.
		[Fact]
		public void CurrentUserRegistryIsTheEmptySandbox()
		{
			AppHost.Ensure();
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software"))
				Assert.Null(key);
		}

		[Fact]
		public void LanguageIsTheBuiltInDefault()
		{
			AppHost.Ensure();
			Assert.Equal("en-us", GlobalOptions.Instance.Language);
		}
	}
}
