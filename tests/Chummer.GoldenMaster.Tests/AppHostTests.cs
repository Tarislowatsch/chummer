using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using Xunit;

namespace Chummer.GoldenMaster.Tests
{
	[Collection(AppHost.Collection)]
	public class AppHostTests
	{
		[Fact]
		public void StartupPathIsTheTestOutputDirectory()
		{
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
			using (RegistryKey key = Registry.CurrentUser.OpenSubKey("Software"))
				Assert.Null(key);
		}

		[Fact]
		public void LanguageIsTheBuiltInDefault()
		{
			Assert.Equal("en-us", GlobalOptions.Instance.Language);
		}

		// A class outside the collection may touch Chummer before the host exists.
		[Fact]
		public void EveryTestClassJoinsTheAppHostCollection()
		{
			// xUnit also runs static classes (abstract sealed) and non-public facts.
			List<Type> testClasses = typeof(AppHost).Assembly.GetTypes()
				.Where(type => type.IsClass && (!type.IsAbstract || type.IsSealed))
				.Where(type => type.GetMethods(AnyMethod).Any(method => method.IsDefined(typeof(FactAttribute), true)))
				.ToList();

			// An empty scan would pass vacuously.
			Assert.Contains(typeof(AppHostTests), testClasses);
			Assert.Contains(typeof(PrintTests), testClasses);
			List<string> outside = testClasses
				.Where(type => CollectionOf(type) != AppHost.Collection)
				.Select(type => type.FullName)
				.ToList();
			Assert.Empty(outside);
		}

		private const BindingFlags AnyMethod = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

		private static string CollectionOf(Type type)
		{
			CustomAttributeData collection = type.GetCustomAttributesData()
				.SingleOrDefault(data => data.AttributeType == typeof(CollectionAttribute));
			return collection == null ? null : collection.ConstructorArguments[0].Value as string;
		}
	}
}
