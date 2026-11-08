using System;
using System.Collections.Generic;
using System.Linq;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Consumption.Fields
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
			PinReport.AssertEqual("Element names no code literal spells", KnownUnreadNames,
				FieldReaders.UnreadNames(FieldReaders.AllElementNames, FieldReaders.LiteralIdentifiers)
					.ToDictionary(name => name.Entry, name => name.Count, StringComparer.Ordinal));
		}

		[Fact]
		public void EveryRequirementHasAConsumerThatReadsIt()
		{
			PinReport.AssertEqual("Requirements no consumer reads", KnownUnreadRequirements,
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
			PinReport.AssertEqual("Tags read per consumer", PinnedTagCounts,
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

		// - key "file handler/field": the field has no effect on the character
		// - addattribute: CreateImprovements reads only name, to enable MAG or RES
		private static readonly IReadOnlyDictionary<string, int> KnownUnreadHandlerFields =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "critters.xml addattribute/aug", 312 },
				{ "critters.xml addattribute/max", 312 },
				{ "critters.xml addattribute/min", 312 },
				{ "critters.xml addattribute/val", 312 },
				{ "custom content/Dumpshock Datahaven Issue 1/custom_datahaven1_critters.xml addattribute/aug", 1 },
				{ "custom content/Dumpshock Datahaven Issue 1/custom_datahaven1_critters.xml addattribute/max", 1 },
				{ "custom content/Dumpshock Datahaven Issue 1/custom_datahaven1_critters.xml addattribute/min", 1 },
				{ "custom content/Dumpshock Datahaven Issue 1/custom_datahaven1_critters.xml addattribute/val", 1 },
				{ "metatypes.xml addattribute/aug", 65 },
				{ "metatypes.xml addattribute/max", 65 },
				{ "metatypes.xml addattribute/min", 65 },
				{ "metatypes.xml addattribute/val", 65 },
				{ "qualities.xml addattribute/aug", 31 },
				{ "qualities.xml addattribute/max", 31 },
				{ "qualities.xml addattribute/min", 31 },
				{ "qualities.xml addattribute/val", 31 },
			};

		[Fact]
		public void EveryHandlerFieldIsReadByItsBranch()
		{
			PinReport.AssertEqual("Handler fields no branch reads", KnownUnreadHandlerFields,
				FieldReaders.UnreadHandlerFields(FieldReaders.AllHandlerFields, FieldReaders.HandlerBranches)
					.GroupBy(field => field.Entry, StringComparer.Ordinal)
					.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
		}

		// - a missing field fails loudly, an over-reaching extractor would hide one
		// - swapskillattribute's one rule block is empty: only custom improvements fill it
		private static readonly IReadOnlyDictionary<string, string> MeasuredHandlerFields =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "addattribute", "name" },
				{ "conditionmonitor", "overflow physical stun threshold thresholdoffset" },
				{ "livingpersona", "biofeedback firewall response signal system" },
				{ "selectattribute", "affectbase attribute aug excludeattribute max min val" },
				{ "selectskill", "applytorating max val" },
				{ "selectskillgroup", "applytorating bonus" },
				{ "swapskillattribute", "attribute" },
			};

		[Fact]
		public void EachIndexedHandlerReadsTheMeasuredFields()
		{
			PinReport.AssertEqual("Fields read per handler", MeasuredHandlerFields,
				FieldReaders.HandlerBranches.ToDictionary(branch => branch.Handler, branch => Sorted(branch.Fields),
					StringComparer.Ordinal));
		}

		private static readonly IReadOnlyDictionary<string, string> MeasuredHandlerDispatch =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "addattribute/name", "MAG RES" },
				{ "selectskill/applytorating", "yes" },
				{ "selectskillgroup/applytorating", "yes" },
			};

		[Fact]
		public void EachIndexedHandlerDispatchesOnTheMeasuredValues()
		{
			PinReport.AssertEqual("Compared values per handler field", MeasuredHandlerDispatch,
				FieldReaders.HandlerBranches
					.SelectMany(branch => branch.Dispatch.Select(entry => (Key: branch.Handler + "/" + entry.Key, entry.Value)))
					.ToDictionary(entry => entry.Key, entry => Sorted(entry.Value), StringComparer.Ordinal));
		}

		private static string Sorted(IEnumerable<string> values)
		{
			return string.Join(" ", values.OrderBy(value => value, StringComparer.Ordinal));
		}

		// - deliberately no allowlist: every compared value matches today
		[Fact]
		public void EveryDispatchedHandlerValueMatchesAComparison()
		{
			string[] unhandled = FieldReaders.UnhandledHandlerValues(FieldReaders.AllHandlerFields,
					FieldReaders.HandlerBranches)
				.GroupBy(field => field.Entry + " = \"" + field.Value + "\"", StringComparer.Ordinal)
				.Select(group => group.Key + " ×" + group.Count())
				.OrderBy(line => line, StringComparer.Ordinal)
				.ToArray();

			Assert.True(unhandled.Length == 0, "No comparison in the branch matches:\n  " + string.Join("\n  ", unhandled));
		}

		// - key "file text": a line number would break on every unrelated edit above it
		// - Commlink prints its matrix condition monitor under the bonus handler's name
		private static readonly IReadOnlyDictionary<string, int> KnownForeignHandlerLiterals =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "clsEquipment.cs \"conditionmonitor\"", 1 },
			};

		[Fact]
		public void NoLiteralOutsideItsBranchNamesAnIndexedHandler()
		{
			PinReport.AssertEqual("Handler literals outside their branch (extend the extraction?)",
				KnownForeignHandlerLiterals,
				FieldReaders.HandlerBranches
					.SelectMany(branch => FieldReaders.HandlerLiteralsOutsideBranch(NameLookups.AllLiterals, branch))
					.GroupBy(literal => literal.File + " \"" + literal.Text + "\"", StringComparer.Ordinal)
					.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
		}

		[Fact]
		public void EveryFieldLiteralInABranchIsExtracted()
		{
			string[] missed = FieldReaders.HandlerBranches
				.SelectMany(branch => FieldReaders.UnextractedFields(branch, FieldReaders.AllHandlerFields
						.Where(field => field.Handler == branch.Handler).Select(field => field.Tag))
					.Select(field => branch.Handler + "/" + field))
				.ToArray();

			Assert.True(missed.Length == 0, "The indexer pattern misses:\n  " + string.Join("\n  ", missed));
		}

		// - top-level files plus every pack file Load would merge
		// - bumping the number is only correct for a deliberately added or removed data file
		[Fact]
		public void ScansTheMeasuredDataFiles()
		{
			Assert.Equal(50, FieldReaders.DataFiles.Count);
		}
	}
}
