using System;
using System.Collections.Generic;
using System.Linq;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Localization
{
	public class TranslationReferenceTests
	{
		// - fixing one changes what a translated UI shows: a behaviour change, not a tidy-up
		// - format: <language> <file> <kind>: <key>, copied from the failure output
		private static readonly HashSet<string> KnownDroppedTranslations = new HashSet<string>(StringComparer.Ordinal)
		{
			// - Load compares an advantage by its whole text: one with a <bonus> can never match
			"de martialarts.xml advantage: Arnis De Mano: +1 DV on Blades attacks",
			"de martialarts.xml advantage: Arnis De Mano: +1 DV on Club attacks",
			"de martialarts.xml advantage: Ars Cybernetica: +1 DV on Blades attacks",
			"de martialarts.xml advantage: Boxing: +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Escrima: +1 DV on Blades attacks",
			"de martialarts.xml advantage: Escrima: +1 DV on Club attacks",
			"de martialarts.xml advantage: Eztlitzli: +1 DV on Blades attacks",
			"de martialarts.xml advantage: Hapkido: +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Hwarang-do: +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Kali: +1 DV on Blades attacks",
			"de martialarts.xml advantage: Kali: +1 DV on Club attacks",
			"de martialarts.xml advantage: Karate: +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Kenpo: +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Kiai: +1 on Intimidation Tests",
			"de martialarts.xml advantage: Kickboxing: +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Kung Fu: +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Muay Thai: +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Musti Yudha: +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Ninjutsu: +1 die on Infiltration Tests",
			"de martialarts.xml advantage: Rince an Bhata Uisce Bheatha: +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Sangre y Acero: +1 DV on Blades attacks",
			"de martialarts.xml advantage: Savate (Alternate): +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Savate: +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Soo Bahk Do: +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Tae Kwon Do: +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Tai Chi Ch'uan: +1 on Intimidation Tests",
			"de martialarts.xml advantage: Wildcat: +1 DV on Unarmed Combat attacks",
			"de martialarts.xml advantage: Wushu: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Arnis De Mano: +1 DV on Blades attacks",
			"fr martialarts.xml advantage: Arnis De Mano: +1 DV on Club attacks",
			"fr martialarts.xml advantage: Ars Cybernetica: +1 DV on Blades attacks",
			"fr martialarts.xml advantage: Boxing: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Escrima: +1 DV on Blades attacks",
			"fr martialarts.xml advantage: Escrima: +1 DV on Club attacks",
			"fr martialarts.xml advantage: Eztlitzli: +1 DV on Blades attacks",
			"fr martialarts.xml advantage: Hapkido: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Hwarang-do: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Kali: +1 DV on Blades attacks",
			"fr martialarts.xml advantage: Kali: +1 DV on Club attacks",
			"fr martialarts.xml advantage: Karate: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Kenpo: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Kiai: +1 on Intimidation Tests",
			"fr martialarts.xml advantage: Kickboxing: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Kung Fu: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Muay Thai: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Musti Yudha: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Ninjutsu: +1 die on Infiltration Tests",
			"fr martialarts.xml advantage: Rince an Bhata Uisce Bheatha: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Sangre y Acero: +1 DV on Blades attacks",
			"fr martialarts.xml advantage: Savate (Alternate): +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Savate: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Soo Bahk Do: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Tae Kwon Do: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Tai Chi Ch'uan: +1 on Intimidation Tests",
			"fr martialarts.xml advantage: Wildcat: +1 DV on Unarmed Combat attacks",
			"fr martialarts.xml advantage: Wushu: +1 DV on Unarmed Combat attacks",
		};

		public static IEnumerable<object[]> Languages()
		{
			return TranslationReferences.Languages.Select(language => new object[] { language });
		}

		[Theory]
		[MemberData(nameof(Languages))]
		public void EveryTranslationReachesItsEnglishEntry(string language)
		{
			TranslationReferences.Entry[] unexpected = TranslationReferences.EntriesFor(language)
				.Where(entry => entry.Outcome == TranslationReferences.Outcome.Dropped)
				.Where(entry => !KnownDroppedTranslations.Contains(entry.Display))
				.OrderBy(entry => entry.Display, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNone(
				language + " has " + unexpected.Length + " translation(s) that XmlManager.Load drops",
				unexpected, entry => entry.Display);
		}

		// - no allowlist: a crash takes the whole data file down for that language
		[Fact]
		public void NoTranslationCrashesTheLoad()
		{
			string[] crashing = TranslationReferences.AllEntries
				.Where(entry => entry.Outcome == TranslationReferences.Outcome.Crashes)
				.Select(entry => entry.Display)
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNone("XmlManager.Load throws on these translations", crashing);
		}

		// Same reason the duplicate allowlist has a staleness check: see NameUniquenessTests.
		[Fact]
		public void AllowlistedTranslationsAreAllStillDropped()
		{
			HashSet<string> actual = new HashSet<string>(TranslationReferences.AllEntries
				.Where(entry => entry.Outcome == TranslationReferences.Outcome.Dropped)
				.Select(entry => entry.Display), StringComparer.Ordinal);

			string[] stale = KnownDroppedTranslations.Where(entry => !actual.Contains(entry))
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNoneInFull("These translations apply (or are gone) and must be removed from "
				+ nameof(KnownDroppedTranslations), stale);
		}

		// - updating the hash is only correct after re-reading Load and fixing the port
		private const string PinnedLoadFingerprint = "30CB7871CF8E7CDD";

		[Fact]
		public void TranslationPassUnchangedSinceItsPortWasWritten()
		{
			string actual = SourceMembers.Fingerprints(new[] { "XmlManager.Load" })["XmlManager.Load"];

			Assert.True(actual == PinnedLoadFingerprint,
				"XmlManager.Load now hashes to " + actual + ", pinned as " + PinnedLoadFingerprint
				+ ". Re-read its translation pass, fix TranslationReferences, then update the hash.");
		}

		// - per language and kind: a broken selector reads nothing and passes, a total would hide it
		// - bumping a number is only correct for a deliberate data change
		private static readonly IReadOnlyDictionary<string, int> PinnedEntryCounts =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "de advantage", 147 },
				{ "de file", 23 },
				{ "de item", 6063 },
				{ "de metavariant", 62 },
				{ "de spec", 970 },
				{ "de text", 291 },
				{ "fr advantage", 147 },
				{ "fr file", 25 },
				{ "fr item", 4232 },
				{ "fr metavariant", 62 },
				{ "fr spec", 988 },
				{ "fr text", 283 },
				{ "jp file", 20 },
				{ "jp item", 1989 },
				{ "jp spec", 943 },
				{ "jp text", 195 },
			};

		[Fact]
		public void EveryLanguageAndKindChecksItsMeasuredEntryCount()
		{
			Dictionary<string, int> actual = TranslationReferences.AllEntries
				.GroupBy(entry => entry.Language + " " + entry.Kind.ToString().ToLowerInvariant(), StringComparer.Ordinal)
				.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

			// - unbounded on purpose: after a deliberate change the whole list is copied back
			string[] drifted = actual.Keys.Union(PinnedEntryCounts.Keys)
				.Where(key => !actual.ContainsKey(key) || !PinnedEntryCounts.ContainsKey(key)
					|| actual[key] != PinnedEntryCounts[key])
				.OrderBy(key => key, StringComparer.Ordinal)
				.Select(key => "{ \"" + key + "\", " + (actual.ContainsKey(key) ? actual[key].ToString() : "gone")
					+ " }, // pinned " + (PinnedEntryCounts.ContainsKey(key) ? PinnedEntryCounts[key].ToString() : "none"))
				.ToArray();

			FailureReport.AssertNoneInFull("These languages check a different number of translations than pinned", drifted);
		}
	}
}
