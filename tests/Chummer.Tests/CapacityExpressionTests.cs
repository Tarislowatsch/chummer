using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// - deliberately no allowlist: every capacity value passes today
	public class CapacityExpressionTests
	{
		[Theory]
		[MemberData(nameof(ExpressionChecks.RuleIds), "capacity", MemberType = typeof(ExpressionChecks))]
		[MemberData(nameof(ExpressionChecks.RuleIds), "armorcapacity", MemberType = typeof(ExpressionChecks))]
		public void EveryCapacityPassesItsCalculatorsAndConsumers(string id)
		{
			ExpressionChecks.ExpressionCheck check = ExpressionChecks.CheckFor(id);

			if (check.Failures.Count > 0)
			{
				Assert.Fail(FailureReport.Build(
					Path.GetFileName(check.FilePath) + " has " + check.Failures.Count + " " + id
						+ " value(s) a capacity calculator or its consumer fails on",
					check.Failures,
					failure => "'" + failure.ItemName + "' = \"" + failure.Value + "\" at rating "
						+ failure.Rating + ": " + failure.Error));
			}
		}

		// - key "file Class.Member": literals "capacity" or "armorcapacity" in that member
		// - Create, Load, Save and Print copy the text, the ported members evaluate it
		// - a new member is a new reader: port it or say here why it only copies
		private static readonly IReadOnlyDictionary<string, int> PinnedFieldReads =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "clsEquipment.cs Armor.Create", 1 },
				{ "clsEquipment.cs Armor.Load", 1 },
				{ "clsEquipment.cs Armor.Save", 1 },
				{ "clsEquipment.cs ArmorMod.Create", 1 },
				{ "clsEquipment.cs ArmorMod.Load", 1 },
				{ "clsEquipment.cs ArmorMod.Save", 1 },
				{ "clsEquipment.cs Commlink.Create", 1 },
				{ "clsEquipment.cs Commlink.Load", 1 },
				{ "clsEquipment.cs Commlink.Save", 1 },
				// - the second read copies a "[*]" subsystem's capacity, the same field
				{ "clsEquipment.cs Cyberware.Create", 2 },
				{ "clsEquipment.cs Cyberware.Load", 1 },
				{ "clsEquipment.cs Cyberware.Print", 1 },
				{ "clsEquipment.cs Cyberware.Save", 1 },
				{ "clsEquipment.cs Gear.Create", 2 },
				// - usegear/capacity, see EveryChildGearCapacityOverrideParses
				{ "clsEquipment.cs Gear.CreateChildren", 2 },
				{ "clsEquipment.cs Gear.Load", 2 },
				{ "clsEquipment.cs Gear.Print", 1 },
				{ "clsEquipment.cs Gear.Save", 2 },
				{ "clsUnique.cs TechProgram.Create", 1 },
				{ "clsUnique.cs TechProgram.Load", 1 },
				{ "clsUnique.cs TechProgram.Save", 1 },
				// - shows the text verbatim, compares it to "" and "0" only
				{ "frmSelectArmor.cs frmSelectArmor.lstArmor_SelectedIndexChanged", 3 },
				{ "frmSelectArmorMod.cs frmSelectArmorMod.UpdateSelectedArmor", 1 },
				{ "frmSelectCyberware.cs frmSelectCyberware.UpdateCyberwareInfo", 13 },
				{ "frmSelectGear.cs frmSelectGear.UpdateGearInfo", 2 },
				// - a null check that hides gear without armor capacity
				{ "frmSelectGear.cs frmSelectGear.txtSearch_TextChanged", 1 },
			};

		[Fact]
		public void EveryCapacityReadSitsInAKnownMember()
		{
			Dictionary<string, int> actual = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (string path in DataPaths.ApplicationSourceFiles())
			{
				string[] lines = File.ReadAllLines(path);
				string[] members = ExpressionChecks.EnclosingMembers(lines);
				string file = Path.GetFileName(path);
				foreach (NameLookups.Literal literal in NameLookups.LiteralsIn(file, lines)
					.Where(literal => literal.Text == "capacity" || literal.Text == "armorcapacity"))
				{
					int line = int.Parse(literal.Site.Substring(literal.Site.LastIndexOf(':') + 1));
					string key = file + " " + (members[line - 1] ?? "(no member)");
					actual[key] = actual.TryGetValue(key, out int count) ? count + 1 : 1;
				}
			}

			CountReport.AssertEqual("Capacity literals per member", PinnedFieldReads, actual);
		}

		// - usegear/capacity at any depth: CreateChildren recurses (clsEquipment.cs:9968)
		// - wrapped in brackets (:9963), then deducted from its parent like any plugin
		// - exact count: a moved path must not leave this green with no value checked
		[Fact]
		public void EveryChildGearCapacityOverrideParses()
		{
			XmlDocument document = new XmlDocument();
			document.Load(Path.Combine(DataPaths.ChummerDataDir, "gear.xml"));
			string[] capacities = document.SelectNodes("//usegear[capacity]").Cast<XmlNode>()
				.Select(child => "[" + child["capacity"].InnerText + "]")
				.ToArray();
			Assert.Equal(3, capacities.Length);

			string[] failures = capacities
				.Where(capacity =>
				{
					try
					{
						CapacityExpressions.GearChild(CapacityExpressions.CalculatedGearCapacity(capacity, 1));
						return false;
					}
					catch (Exception)
					{
						return true;
					}
				})
				.ToArray();

			Assert.True(failures.Length == 0, "Unparsable child capacities:\n  " + string.Join("\n  ", failures));
		}

		// - a green run proves nothing if a rule reaches no value: Gear skips an absent field
		// - bumping a number is only correct for deliberately added or removed data
		// - the 0 rows stay: a first value there is checked at once
		private static readonly IReadOnlyDictionary<string, int> PinnedValueCounts =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "Armor.armorcapacity", 192 },
				{ "ArmorMod.armorcapacity", 62 },
				{ "Bioware.capacity", 164 },
				{ "Commlink.armorcapacity", 16 },
				{ "Commlink.capacity", 0 },
				{ "Cyberware.capacity", 275 },
				{ "Gear.armorcapacity", 79 },
				{ "Gear.capacity", 181 },
				{ "OperatingSystem.armorcapacity", 0 },
				{ "OperatingSystem.capacity", 0 },
				{ "TechProgram.capacity", 71 },
			};

		[Fact]
		public void EachRuleReachesTheMeasuredValues()
		{
			Dictionary<string, RequiredFields.RequiredFieldRule> entities = RequiredFields.Rules
				.ToDictionary(rule => rule.Entity, StringComparer.Ordinal);
			Dictionary<string, int> actual = CapacityExpressions.Rules.ToDictionary(rule => rule.Id, rule =>
			{
				XmlDocument document = new XmlDocument();
				document.Load(Path.Combine(DataPaths.ChummerDataDir, entities[rule.Entity].FileName));
				return document.SelectNodes(entities[rule.Entity].ItemXPath).Cast<XmlNode>()
					.Count(item => item[rule.Field] != null);
			}, StringComparer.Ordinal);

			CountReport.AssertEqual("Values per rule", PinnedValueCounts, actual);
		}
	}
}
