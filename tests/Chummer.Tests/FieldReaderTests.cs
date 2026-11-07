using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	public class FieldReaderTests
	{
		// - key "file name": the name appears in no literal of the code
		// - vehicles.xml: a mod's body and pilot limits are never checked
		// - powers.xml: Enhanced Perception's INT cap on levels is never enforced
		private static readonly IReadOnlyDictionary<string, int> KnownUnreadNames =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "powers.xml cap", 1 },
				{ "vehicles.xml bodymax", 2 },
				{ "vehicles.xml bodymin", 6 },
				{ "vehicles.xml pilotmin", 2 },
			};

		// - key "file | item | group/tag": the item stays selectable without the requirement
		// - metamagic.xml: frmSelectMetamagic reads required/allof only
		// - spells.xml and vehicles.xml: no consumer reads required at all
		private static readonly IReadOnlyDictionary<string, int> KnownUnreadRequirements =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "metamagic.xml | Home Advantage | required/oneof/metamagic", 2 },
				{ "metamagic.xml | Patronage | required/oneof/metamagic", 2 },
				{ "spells.xml | Geopathic Connection | required/allof/metamagic", 2 },
				{ "spells.xml | Ley Sight | required/allof/metamagic", 1 },
				{ "spells.xml | Manifest Sha | required/allof/metamagic", 2 },
				{ "spells.xml | Yang Zhai | required/allof/metamagic", 2 },
				{ "vehicles.xml | Drone Rack, Large Landing | required/bodymin", 1 },
				{ "vehicles.xml | Drone Rack, Large | required/bodymin", 1 },
				{ "vehicles.xml | Drone Rack, Mini | required/bodymin", 1 },
				{ "vehicles.xml | Drone Rack, Multilaunch | required/bodymin", 1 },
				{ "vehicles.xml | Drone Rack, Small Landing | required/bodymin", 1 },
				{ "vehicles.xml | Drone Rack, Small | required/bodymin", 1 },
				{ "vehicles.xml | Flying Fox Gliding System | required/bodymax", 1 },
				{ "vehicles.xml | Fuzzy Logic | required/pilotmin", 1 },
				{ "vehicles.xml | Lighter Than Air | required/bodymax", 1 },
				{ "vehicles.xml | Smart Tires | required/pilotmin", 1 },
			};

		[Fact]
		public void EveryElementNameIsSpelledBySomeCodeLiteral()
		{
			AssertCounts("Element names no code literal spells", KnownUnreadNames,
				FieldReaders.UnreadNames(FieldReaders.AllElementNames, FieldReaders.LiteralIdentifiers)
					.ToDictionary(name => name.Entry, name => name.Count, StringComparer.Ordinal));
		}

		[Fact]
		public void EveryRequirementHasAConsumerThatReadsIt()
		{
			AssertCounts("Requirements no consumer reads", KnownUnreadRequirements,
				FieldReaders.UnreadRequirements(FieldReaders.AllRequirementUses, FieldReaders.Readers,
						FieldReaders.TagsReadBy)
					.GroupBy(use => use.Entry, StringComparer.Ordinal)
					.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
		}

		// - key "file group": tags the consumer source reads
		// - a missing tag fails loudly, an over-reaching extractor would hide one
		// - bumping a number is only correct for a deliberately added or removed check
		private static readonly IReadOnlyDictionary<string, int> PinnedTagCounts =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "echoes.xml required/allof", 4 },
				{ "metamagic.xml required/allof", 4 },
				{ "powers.xml required/allof", 1 },
				{ "powers.xml required/oneof", 1 },
				{ "qualities.xml forbidden/oneof", 5 },
				{ "qualities.xml required/allof", 14 },
				{ "qualities.xml required/oneof", 14 },
			};

		[Fact]
		public void EachReaderReadsTheMeasuredTags()
		{
			AssertCounts("Tags read per consumer", PinnedTagCounts,
				FieldReaders.Readers.ToDictionary(reader => reader.Key,
					reader => FieldReaders.TagsReadBy(reader).Count, StringComparer.Ordinal));
		}

		// - a mistyped item path or group matches no data: its reader would vouch for nothing
		[Fact]
		public void EveryReaderMatchesARequirementInTheData()
		{
			string[] vacant = FieldReaders.Readers
				.Where(reader => !FieldReaders.AllRequirementUses.Any(use => use.BaseFile == reader.File
					&& use.ItemPath == reader.ItemPath && use.Group == reader.Group))
				.Select(reader => reader.Key + " (" + reader.Consumer + ")")
				.ToArray();

			Assert.True(vacant.Length == 0, "These readers match nothing:\n  " + string.Join("\n  ", vacant));
		}

		// - a new consumer would leave the pinned findings standing while the code already reads them
		[Fact]
		public void EveryRequirementLiteralBelongsToAListedReader()
		{
			string[] unlisted = FieldReaders.UnlistedRequirementLiterals(NameLookups.AllLiterals,
					FieldReaders.Readers, FieldReaders.KnownExtraReaders)
				.Select(literal => literal.Site + " \"" + literal.Text + "\"")
				.ToArray();

			Assert.True(unlisted.Length == 0, "Add a reader or a known extra reader for:\n  "
				+ string.Join("\n  ", unlisted));
		}

		[Fact]
		public void EveryKnownExtraReaderStillReadsItsTag()
		{
			HashSet<string> read = new HashSet<string>(NameLookups.AllLiterals
				.SelectMany(literal => FieldReaders.RequirementMentionsIn(literal.Text)
					.Select(mention => literal.File + " " + mention.Display)), StringComparer.Ordinal);
			string[] stale = FieldReaders.KnownExtraReaders.Keys.Where(key => !read.Contains(key)).ToArray();

			Assert.True(stale.Length == 0, "Remove from KnownExtraReaders:\n  " + string.Join("\n  ", stale));
		}

		[Fact]
		public void EveryConsumerLineStillSelectsItsGroup()
		{
			string[] moved = FieldReaders.Readers
				.Where(reader => !FieldReaders.ConsumerLineSelectsGroup(reader, NameLookups.AllLiterals))
				.Select(reader => reader.Key + ": " + reader.Consumer)
				.ToArray();

			Assert.True(moved.Length == 0, "Re-read these consumers and fix the line:\n  " + string.Join("\n  ", moved));
		}

		// - top-level files plus every pack file Load would merge
		// - bumping the number is only correct for a deliberately added or removed data file
		[Fact]
		public void ScansTheMeasuredDataFiles()
		{
			Assert.Equal(50, FieldReaders.DataFiles.Count);
		}

		// - one line per differing key: Assert.Equal truncates a dictionary after a few entries
		// - "expected 0" is a new finding, "found 0" an entry that is fixed and must go
		private static void AssertCounts(string headline, IReadOnlyDictionary<string, int> expected,
			IReadOnlyDictionary<string, int> actual)
		{
			string[] differences = expected.Keys.Union(actual.Keys, StringComparer.Ordinal)
				.Select(key => new
				{
					Key = key,
					Expected = expected.TryGetValue(key, out int wanted) ? wanted : 0,
					Actual = actual.TryGetValue(key, out int found) ? found : 0,
				})
				.Where(entry => entry.Expected != entry.Actual)
				.OrderBy(entry => entry.Key, StringComparer.Ordinal)
				.Select(entry => entry.Key + ": expected " + entry.Expected + ", found " + entry.Actual)
				.ToArray();

			if (differences.Length > 0)
				Assert.Fail(FailureReport.Build(headline + " differ from the pinned list", differences, line => line));
		}
	}
}
