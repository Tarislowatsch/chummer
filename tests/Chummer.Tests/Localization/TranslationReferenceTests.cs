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
		private const string PinnedLoadFingerprint = "53B0D0A073F3437E";

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
