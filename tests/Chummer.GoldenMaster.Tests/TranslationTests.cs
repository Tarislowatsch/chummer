using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Xunit;

namespace Chummer.GoldenMaster.Tests
{
	// The print runs in en-us: only this test sees the translation pass of XmlManager.Load.
	[Collection(AppHost.Collection)]
	public class TranslationTests
	{
		private static readonly UTF8Encoding NoBom = new UTF8Encoding(false);

		private static readonly string[] Languages = { "de", "fr", "jp" };

		public static string Root => Path.Combine(Fixtures.RepoRoot, "tests", "fixtures", "translations");

		public static IEnumerable<object[]> LanguageCases()
		{
			return Languages.Select(language => new object[] { language });
		}

		[Theory]
		[MemberData(nameof(LanguageCases))]
		public void TranslatedLoadMatchesItsBaseline(string language)
		{
			string actual = string.Concat(TranslatedLoad.Project(language, TranslatedFiles(language)).Select(line => line + "\n"));
			string path = Path.Combine(Root, language + ".txt");
			if (Environment.GetEnvironmentVariable(Baselines.AcceptVariable) == "1")
			{
				Directory.CreateDirectory(Root);
				File.WriteAllText(path, actual, NoBom);
				Assert.Fail(Baselines.AcceptVariable + " is set, wrote " + path + ". Review it with git diff, then run without it.");
			}

			if (!File.Exists(path))
				Assert.Fail("No baseline at " + path + ". Run with " + Baselines.AcceptVariable + "=1 to write it.");

			string expected = File.ReadAllText(path, NoBom);
			if (expected != actual)
				Assert.Fail(path + " differs from the translated load. " + Baselines.FirstDifference(expected, actual)
					+ "\nIf the change is intended, run with " + Baselines.AcceptVariable + "=1 and review it with git diff.");
		}

		// A data file shipped for a language the theory skips would go unpinned.
		[Fact]
		public void LanguagesAreExactlyTheShippedDataTranslations()
		{
			string[] shipped = Directory.GetFiles(Path.Combine(AppHost.AppDirectory, "lang"), "*_data.xml")
				.Select(path => Path.GetFileName(path).Replace("_data.xml", ""))
				.OrderBy(name => name, StringComparer.Ordinal)
				.ToArray();
			Assert.Equal(Languages, shipped);
		}

		[Fact]
		public void BaselinesExistForExactlyTheLanguages()
		{
			Assert.Equal(Languages.Select(language => language + ".txt"),
				Directory.GetFiles(Root).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
		}

		// - the projection must see only what the translation pass adds, not the English data
		// - a baseline line can only come from the language switch
		[Fact]
		public void EnglishLoadAddsNothing()
		{
			string[] files = Languages.SelectMany(TranslatedFiles).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToArray();
			Assert.Empty(TranslatedLoad.Project("en-us", files));
		}

		// The files whose translation block Load reads for this language.
		private static IEnumerable<string> TranslatedFiles(string language)
		{
			XmlDocument data = new XmlDocument();
			data.Load(Path.Combine(AppHost.AppDirectory, "lang", language + "_data.xml"));
			return data.SelectNodes("/chummer/chummer/@file").Cast<XmlNode>()
				.Select(file => file.Value)
				.OrderBy(name => name, StringComparer.Ordinal)
				.ToArray();
		}
	}
}
