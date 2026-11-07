using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	public class BonusValueTests
	{
		// - code defect, not data: ValueToInt prints in the thread culture, see BonusValues.LocalCulture
		// - fixing ValueToInt empties this list, the data stays as it is
		private static readonly IReadOnlyDictionary<string, string> KnownFailures =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "cyberware.xml | Boosted Reflexes | initiativepass = Rating div 2", "de-DE: 5 passes at rating 1, 15 at 3" },
				{ "cyberware.xml | Boosted Reflexes (2050) | initiativepass = Rating div 2", "de-DE: 5 passes at rating 1, 15 at 3, 25 at 5" },
			};

		[Fact]
		public void EveryBonusValuePassesValueToInt()
		{
			BonusValues.BonusValueFailure[] unexpected = BonusValues.Corpus.Failures
				.Where(failure => !KnownFailures.ContainsKey(failure.Key))
				.ToArray();

			if (unexpected.Length > 0)
			{
				Assert.Fail(FailureReport.Build(unexpected.Length + " bonus value(s) ValueToInt or its branch fails on",
					unexpected, failure => failure.Key + " at rating " + failure.Rating + ": " + failure.Error));
			}
		}

		[Fact]
		public void EveryKnownFailureStillFails()
		{
			Assert.Empty(KnownFailures.Keys.Except(
				BonusValues.Corpus.Failures.Select(failure => failure.Key), StringComparer.Ordinal));
		}

		// - "branch | arguments as written" -> number of calls
		// - a new key is a new read: port it in BonusValues, then pin it here
		private static readonly IReadOnlyDictionary<string, int> PinnedCalls =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "adeptpowerpoints | nodBonus[\"adeptpowerpoints\"].InnerText, intRating", 1 },
				{ "armor | objXmlArmor[\"b\"].InnerText, intRating", 1 },
				{ "armor | objXmlArmor[\"i\"].InnerText, intRating", 1 },
				{ "armorencumbrancepenalty | nodBonus[\"armorencumbrancepenalty\"].InnerText, intRating", 1 },
				{ "basicbiowareessmultiplier | nodBonus[\"basicbiowareessmultiplier\"].InnerText, intRating", 1 },
				{ "basiclifestylecost | nodBonus[\"basiclifestylecost\"].InnerText, intRating", 1 },
				{ "biowareessmultiplier | nodBonus[\"biowareessmultiplier\"].InnerText, intRating", 1 },
				{ "complexformlimit | nodBonus[\"complexformlimit\"].InnerText, intRating", 1 },
				{ "composure | nodBonus[\"composure\"].InnerText, intRating", 1 },
				{ "concealability | nodBonus[\"concealability\"].InnerText, intRating", 1 },
				{ "conditionmonitor | nodBonus.SelectSingleNode(\"conditionmonitor\")[\"overflow\"].InnerText, intRating", 1 },
				{ "conditionmonitor | nodBonus.SelectSingleNode(\"conditionmonitor\")[\"physical\"].InnerText, intRating", 1 },
				{ "conditionmonitor | nodBonus.SelectSingleNode(\"conditionmonitor\")[\"stun\"].InnerText, intRating", 1 },
				{ "conditionmonitor | nodBonus.SelectSingleNode(\"conditionmonitor\")[\"threshold\"].InnerText, intRating", 1 },
				{ "conditionmonitor | nodBonus.SelectSingleNode(\"conditionmonitor\")[\"thresholdoffset\"].InnerText, intRating", 1 },
				{ "cyberwareessmultiplier | nodBonus[\"cyberwareessmultiplier\"].InnerText, intRating", 1 },
				{ "damageresistance | nodBonus[\"damageresistance\"].InnerText, intRating", 1 },
				{ "drainresist | nodBonus[\"drainresist\"].InnerText, intRating", 1 },
				{ "essencemax | nodBonus[\"essencemax\"].InnerText, intRating", 1 },
				{ "fadingresist | nodBonus[\"fadingresist\"].InnerText, intRating", 1 },
				{ "flypercent | nodBonus[\"flypercent\"].InnerText, intRating", 1 },
				{ "flyspeed | nodBonus[\"flyspeed\"].InnerText, intRating", 1 },
				{ "freenegativequalities | nodBonus[\"freenegativequalities\"].InnerText, intRating", 1 },
				{ "freepositivequalities | nodBonus[\"freepositivequalities\"].InnerText, intRating", 1 },
				{ "freespiritpowerpoints | nodBonus[\"freespiritpowerpoints\"].InnerText, intRating", 1 },
				{ "genetechcostmultiplier | nodBonus[\"genetechcostmultiplier\"].InnerText, intRating", 1 },
				{ "initiation | nodBonus[\"initiation\"].InnerText, intRating", 2 },
				{ "initiative | nodBonus[\"initiative\"].InnerText, intRating", 1 },
				{ "initiativepass | nodBonus[\"initiativepass\"].InnerText, intRating", 1 },
				{ "initiativepassadd | nodBonus[\"initiativepassadd\"].InnerText, intRating", 1 },
				{ "judgeintentions | nodBonus[\"judgeintentions\"].InnerText, intRating", 1 },
				{ "lifestylecost | nodBonus[\"lifestylecost\"].InnerText, intRating", 1 },
				{ "liftandcarry | nodBonus[\"liftandcarry\"].InnerText, intRating", 1 },
				{ "livingpersona | nodBonus.SelectSingleNode(\"livingpersona\")[\"biofeedback\"].InnerText, intRating", 1 },
				{ "livingpersona | nodBonus.SelectSingleNode(\"livingpersona\")[\"firewall\"].InnerText, intRating", 1 },
				{ "livingpersona | nodBonus.SelectSingleNode(\"livingpersona\")[\"response\"].InnerText, intRating", 1 },
				{ "livingpersona | nodBonus.SelectSingleNode(\"livingpersona\")[\"signal\"].InnerText, intRating", 1 },
				{ "livingpersona | nodBonus.SelectSingleNode(\"livingpersona\")[\"system\"].InnerText, intRating", 1 },
				{ "matrixinitiative | nodBonus[\"matrixinitiative\"].InnerText, intRating", 1 },
				{ "matrixinitiativepass | nodBonus[\"matrixinitiativepass\"].InnerText, intRating", 1 },
				{ "matrixinitiativepassadd | nodBonus[\"matrixinitiativepassadd\"].InnerText, intRating", 1 },
				{ "memory | nodBonus[\"memory\"].InnerText, intRating", 1 },
				{ "movementpercent | nodBonus[\"movementpercent\"].InnerText, intRating", 1 },
				{ "notoriety | nodBonus[\"notoriety\"].InnerText, intRating", 1 },
				{ "nuyenamt | nodBonus[\"nuyenamt\"].InnerText, intRating", 1 },
				{ "nuyenmaxbp | nodBonus[\"nuyenmaxbp\"].InnerText, intRating", 1 },
				{ "reach | nodBonus[\"reach\"].InnerText, intRating", 1 },
				{ "restricteditemcount | nodBonus[\"restricteditemcount\"].InnerText, intRating", 1 },
				{ "selectattribute | nodBonus[\"selectattribute\"][\"aug\"].InnerXml, intRating", 1 },
				{ "selectattribute | nodBonus[\"selectattribute\"][\"max\"].InnerXml, intRating", 1 },
				{ "selectattribute | nodBonus[\"selectattribute\"][\"min\"].InnerXml, intRating", 1 },
				{ "selectattribute | nodBonus[\"selectattribute\"][\"val\"].InnerXml, intRating", 1 },
				{ "selectskill | nodBonus[\"selectskill\"][\"max\"].InnerText, intRating", 2 },
				{ "selectskill | nodBonus[\"selectskill\"][\"val\"].InnerText, intRating", 2 },
				{ "selectskillgroup | nodBonus[\"selectskillgroup\"][\"bonus\"].InnerText, intRating", 1 },
				{ "skillarticulation | objXmlSkill[\"bonus\"].InnerText, intRating", 1 },
				{ "skillattribute | objXmlSkill[\"bonus\"].InnerXml, intRating", 2 },
				{ "skillcategory | objXmlSkill[\"bonus\"].InnerXml, intRating", 2 },
				{ "skillgroup | objXmlSkill[\"bonus\"].InnerXml, intRating", 2 },
				{ "skillwire | nodBonus[\"skillwire\"].InnerText, intRating", 1 },
				{ "specificattribute | objXmlAttribute[\"aug\"].InnerXml, intRating", 1 },
				{ "specificattribute | objXmlAttribute[\"max\"].InnerXml, intRating", 1 },
				{ "specificattribute | objXmlAttribute[\"min\"].InnerXml, intRating", 1 },
				{ "specificattribute | objXmlAttribute[\"val\"].InnerXml, intRating", 1 },
				{ "specificskill | objXmlSkill[\"bonus\"].InnerXml, intRating", 1 },
				{ "specificskill | objXmlSkill[\"max\"].InnerText, intRating", 1 },
				{ "spellcategory | nodSpellCategory[\"val\"].InnerText, intRating", 1 },
				{ "spelllimit | nodBonus[\"spelllimit\"].InnerText, intRating", 1 },
				{ "submersion | nodBonus[\"submersion\"].InnerText, intRating", 2 },
				{ "swimpercent | nodBonus[\"swimpercent\"].InnerText, intRating", 1 },
				{ "throwrange | nodBonus[\"throwrange\"].InnerText, intRating", 1 },
				{ "throwstr | nodBonus[\"throwstr\"].InnerText, intRating", 1 },
				{ "transgenicsgenetechcost | nodBonus[\"transgenicsgenetechcost\"].InnerText, intRating", 1 },
				{ "unarmedap | nodBonus[\"unarmedap\"].InnerText, intRating", 1 },
				{ "unarmeddv | nodBonus[\"unarmeddv\"].InnerText, intRating", 1 },
				{ "weaponcategorydv | objXmlCategory[\"bonus\"].InnerXml, intRating", 1 },
			};

		[Fact]
		public void EveryValueToIntCallIsPinned()
		{
			CountReport.AssertEqual("ValueToInt calls per branch and argument", PinnedCalls,
				BonusValues.ApplicationCalls.GroupBy(call => call.Key, StringComparer.Ordinal)
					.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
		}

		[Fact]
		public void EveryBranchWithACallIsReplayed()
		{
			Assert.Equal(
				BonusValues.ApplicationCalls.Select(call => call.Handler).Distinct(StringComparer.Ordinal)
					.OrderBy(handler => handler, StringComparer.Ordinal),
				BonusValues.ReplayedHandlers);
		}

		// - the simple replay reads nodBonus["handler"].InnerText at intRating, nothing else
		[Fact]
		public void EverySimpleBranchReadsOnlyItsOwnText()
		{
			Assert.Empty(BonusValues.ApplicationCalls
				.Where(call => BonusValues.SimpleHandlers.Contains(call.Handler))
				.Where(call => call.Arguments != "nodBonus[\"" + call.Handler + "\"].InnerText, intRating")
				.Select(call => call.Key));
		}

		// - updating a hash is only correct after re-reading the code and fixing its port
		private static readonly IReadOnlyDictionary<string, string> PinnedFingerprints =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "ImprovementManager.NodeExists", "CCEEB841D4C41A47" },
				{ "ImprovementManager.ValueToInt", "E41DF909B3A78A30" },
				{ "armor", "75EDFF53F289627E" },
				{ "conditionmonitor", "189B501E4B75A0BB" },
				{ "livingpersona", "ACA64A106C57D3DE" },
				{ "selectattribute", "094E2F073FA0C237" },
				{ "selectskill", "E72691CCBDCA5B5B" },
				{ "selectskillgroup", "C41E1510740FA8FC" },
				{ "skillarticulation", "97A0318DFFF1B769" },
				{ "skillattribute", "D7C866ABAE3269E6" },
				{ "skillcategory", "C7F666FB971817EC" },
				{ "skillgroup", "A92531DE9E01DD1B" },
				{ "specificattribute", "4E8054C8521ED765" },
				{ "specificskill", "CE4113ACB35E6467" },
				{ "spellcategory", "C4CE2142039F8F98" },
				{ "weaponcategorydv", "EEE78DE2CF02B621" },
			};

		[Fact]
		public void NoPortedCodeChangedSinceItWasRead()
		{
			Dictionary<string, string> actual = ExpressionChecks.CalculatorFingerprints(BonusValues.Calculators)
				.Concat(BonusValues.BranchFingerprints(BonusValues.PortedBranches.Keys))
				.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

			string[] drifted = actual.Keys.Union(PinnedFingerprints.Keys, StringComparer.Ordinal)
				.Where(key => !actual.ContainsKey(key) || !PinnedFingerprints.ContainsKey(key)
					|| actual[key] != PinnedFingerprints[key])
				.OrderBy(key => key, StringComparer.Ordinal)
				.Select(key => key + " now hashes to " + (actual.ContainsKey(key) ? actual[key] : "nothing")
					+ ", pinned as " + (PinnedFingerprints.ContainsKey(key) ? PinnedFingerprints[key] : "nothing"))
				.ToArray();

			Assert.True(drifted.Length == 0,
				"This code changed since its port was written. Re-read it, fix the port, then update the hash:\n  "
				+ string.Join("\n  ", drifted));
		}

		// - a green run proves nothing if a path reaches no value
		// - bumping a number is only correct for deliberately added or removed data
		// - a path missing here has no data yet: its first value fails this test and gets a look
		private static readonly IReadOnlyDictionary<string, int> PinnedValueCounts =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "adeptpowerpoints", 1 },
				{ "armor/b", 19 },
				{ "armor/i", 23 },
				{ "armorencumbrancepenalty", 1 },
				{ "basicbiowareessmultiplier", 2 },
				{ "basiclifestylecost", 1 },
				{ "biowareessmultiplier", 2 },
				{ "composure", 1 },
				{ "concealability", 6 },
				{ "conditionmonitor/overflow", 3 },
				{ "conditionmonitor/physical", 28 },
				{ "conditionmonitor/stun", 2 },
				{ "conditionmonitor/threshold", 1 },
				{ "conditionmonitor/thresholdoffset", 7 },
				{ "cyberwareessmultiplier", 3 },
				{ "damageresistance", 14 },
				{ "drainresist", 3 },
				{ "flyspeed", 8 },
				{ "freenegativequalities", 3 },
				{ "freepositivequalities", 3 },
				{ "freespiritpowerpoints", 1 },
				{ "genetechcostmultiplier", 1 },
				{ "initiative", 17 },
				{ "initiativepass", 210 },
				{ "initiativepassadd", 2 },
				{ "judgeintentions", 2 },
				{ "lifestylecost", 12 },
				{ "livingpersona/biofeedback", 3 },
				{ "livingpersona/firewall", 2 },
				{ "livingpersona/response", 4 },
				{ "livingpersona/signal", 2 },
				{ "livingpersona/system", 2 },
				{ "matrixinitiative", 4 },
				{ "matrixinitiativepass", 8 },
				{ "matrixinitiativepassadd", 7 },
				{ "memory", 2 },
				{ "movementpercent", 6 },
				{ "notoriety", 26 },
				{ "nuyenamt", 6 },
				{ "nuyenmaxbp", 1 },
				{ "reach", 148 },
				{ "restricteditemcount", 3 },
				{ "selectattribute/max", 4 },
				{ "selectattribute/min", 1 },
				{ "selectattribute/val", 1 },
				{ "selectskill/max", 2 },
				{ "selectskill/val", 8 },
				{ "selectskillgroup/bonus", 2 },
				{ "skillarticulation/bonus", 2 },
				{ "skillattribute/bonus", 15 },
				{ "skillcategory/bonus", 24 },
				{ "skillgroup/bonus", 14 },
				{ "skillwire", 7 },
				{ "specificattribute/max", 86 },
				{ "specificattribute/max (-natural)", 6 },
				{ "specificattribute/min", 85 },
				{ "specificattribute/val", 38 },
				{ "specificattribute/val (ESS)", 24 },
				{ "specificskill/bonus", 128 },
				{ "specificskill/max", 7 },
				{ "spellcategory/val", 48 },
				{ "swimpercent", 4 },
				{ "throwrange", 1 },
				{ "throwstr", 1 },
				{ "transgenicsgenetechcost", 1 },
				{ "unarmedap", 1 },
				{ "unarmeddv", 4 },
				{ "weaponcategorydv/bonus", 26 },
			};

		[Fact]
		public void EachPathReachesTheMeasuredValues()
		{
			CountReport.AssertEqual("Bonus values per path", PinnedValueCounts,
				BonusValues.Corpus.Reached.GroupBy(value => value.Path, StringComparer.Ordinal)
					.ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
		}

		[Fact]
		public void OnlyTheKnownOwnersExceedTheExhaustiveRatingLimit()
		{
			Assert.Equal(new string[0], BonusValues.Corpus.CutShort.OrderBy(entry => entry, StringComparer.Ordinal));
		}
	}
}
