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

		// - only one translation per English node can show
		// - exact: a removed repeat must leave this list in the same change
		private static readonly IReadOnlyDictionary<string, string> KnownRepeatedTranslations =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				// - English name twice: the second item can never get its translation
				{ "de gear.xml item: gears/gear: Hardening", "2 entries, 2 translations" },
				{ "de gear.xml item: gears/gear: Microphone", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Optimization", "2 entries, 2 translations" },
				{ "de gear.xml item: gears/gear: Overdrive", "2 entries, 2 translations" },
				{ "de gear.xml item: gears/gear: Targeting", "3 entries, 2 translations" },
				{ "fr gear.xml item: gears/gear: Hardening", "2 entries, 2 translations" },
				{ "fr gear.xml item: gears/gear: Microphone", "2 entries, 2 translations" },
				{ "fr gear.xml item: gears/gear: Optimization", "2 entries, 2 translations" },
				{ "fr gear.xml item: gears/gear: Overdrive", "2 entries, 2 translations" },
				{ "fr gear.xml item: gears/gear: Targeting", "2 entries, 2 translations" },
				{ "jp gear.xml item: gears/gear: Microphone", "2 entries, 1 translation" },
				// - same text, other page: every reader takes the first <altpage>
				{ "de critterpowers.xml item: powers/power: Info Sortilege", "2 entries, 2 translations" },
				{ "de critterpowers.xml item: powers/power: Siphon", "2 entries, 2 translations" },
				{ "de critterpowers.xml item: powers/power: Sparky", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Courier Sprite", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Crack Sprite", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Data Sprite", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Eastern Dragon", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Fault Sprite", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Feathered Serpent", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Great Eastern Dragon", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Great Feathered Serpent", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Great Leviathan", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Great Western Dragon", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Leviathan", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Machine Sprite", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Spirit of Air", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Spirit of Air, Greater Form", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Spirit of Beasts", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Spirit of Beasts, Greater Form", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Spirit of Earth", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Spirit of Earth, Greater Form", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Spirit of Fire", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Spirit of Fire, Greater Form", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Spirit of Man", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Spirit of Man, Greater Form", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Spirit of Water", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Spirit of Water, Greater Form", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Watcher Spirit", "2 entries, 2 translations" },
				{ "de critters.xml item: metatypes/metatype: Western Dragon", "2 entries, 2 translations" },
				{ "de weapons.xml item: weapons/weapon: Jupiter Taser Club", "2 entries, 2 translations" },
				{ "fr weapons.xml item: weapons/weapon: Jupiter Taser Club", "2 entries, 2 translations" },
				// - exact copies: nothing is lost, the second is dead weight
				{ "de gear.xml item: gears/gear: Adaptability", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Agent", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Analyze", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Anti-Aircraft Assist", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Armor", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Attack", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Biofeedback Filter", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Black Hammer", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Blackout", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Browse", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Cascading", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Chaser", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Clearsight", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Command", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Corrupt", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Covert Ops", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Data Bomb", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Decrypt", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Defense", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Defuse", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Disarm", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: ECCM", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Edit", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Electronic Warfare", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Encrypt", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Expert Defense", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Exploit", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Headjammer", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Homeground", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: IC", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Jammer, Area", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Jammer, Directional", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: MRSI Software", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Maneuver", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Medic", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Micro-Transceiver", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Nuke", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Pilot", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Profession", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Purge", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Reality Filter", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Replicate", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Scan", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Security Tags", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Sensor Tags", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Sniffer", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Spoof", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Standard RFID Tags", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Stealth", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Stealth Tags", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Suite: Basic User", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Suite: Basic+", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Suite: Pro User", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: System", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Tactical Satellite Mapping Software", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Tag Eraser", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Track", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: Trailblazer", "2 entries, 1 translation" },
				{ "de gear.xml item: gears/gear: White Noise Generator", "2 entries, 1 translation" },
				{ "fr spells.xml item: spells/spell: Napalm Wall", "2 entries, 1 translation" },
				{ "fr spells.xml item: spells/spell: Nauseate", "2 entries, 1 translation" },
				{ "fr spells.xml item: spells/spell: Radiation Shield", "2 entries, 1 translation" },
			};

		[Fact]
		public void EachEnglishEntryIsTranslatedOnlyOncePerLanguage()
		{
			Dictionary<string, string> actual = TranslationReferences.RepeatedTargets(TranslationReferences.AllEntries)
				.ToDictionary(repeat => repeat.Key, repeat => repeat.Description, StringComparer.Ordinal);

			PinReport.AssertEqual("Repeated translations", KnownRepeatedTranslations, actual);
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
