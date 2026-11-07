using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - the expression ports are hand-read: a calculator changing underneath leaves them stale
	public class ExpressionCalculatorTests
	{
		// - updating a hash is only correct after re-reading the member and fixing its port
		private static readonly IReadOnlyDictionary<string, string> PinnedFingerprints =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "Armor.TotalAvail", "FC931D43347A4C5D" },
				{ "ArmorMod.TotalAvail", "0C9601AFEEBECFA6" },
				{ "Cyberware.TotalAvail", "B075F614972C0718" },
				{ "Gear.TotalAvail", "9FBD764457DF3A15" },
				{ "Vehicle.CalculatedAvail", "3CE4E755BFA18399" },
				{ "VehicleMod.TotalAvail", "5ADAB36A9D97789A" },
				{ "Weapon.TotalAvail", "92E0764828135116" },
				{ "WeaponAccessory.TotalAvail", "92E0764828135116" },
				{ "WeaponMod.TotalAvail", "92E0764828135116" },
				{ "frmCreate.GetAvailInt", "5FBAB326C1072FD0" },
				{ "ArmorMod.TotalCost", "DC7B82685AA907EF" },
				{ "Cyberware.OwnCost", "E701F41021BAA841" },
				{ "Cyberware.TotalCost", "E35C70252BE4A26C" },
				{ "Gear.OwnCost", "E94D0D5FE3BD36BA" },
				{ "Gear.TotalCost", "A0ECC92BDBA85AA1" },
				{ "Vehicle.OwnCost", "80DC6B9BB909D09A" },
				{ "Vehicle.TotalCost", "783760ED2244C048" },
				{ "VehicleMod.OwnCost", "D11C4DC31D0A753B" },
				{ "VehicleMod.TotalCost", "C04283733432E648" },
				{ "WeaponAccessory.OwnCost", "9B313DA93EB23449" },
				{ "WeaponAccessory.TotalCost", "86FCBC198480E0DF" },
				{ "WeaponMod.TotalCost", "2ED9CDCE8F3C2C40" },
				{ "Cyberware.CalculatedESS", "A036BBB612C578D5" },
				{ "frmSelectCyberware.UpdateCyberwareInfo", "EDFA0E780E9A1F41" },
				{ "Armor.CalculatedCapacity", "8F84C5123096F853" },
				{ "Armor.CapacityRemaining", "A1888264E000DF7F" },
				{ "ArmorMod.CalculatedCapacity", "EFB60AE6D5E0D06E" },
				{ "Cyberware.CalculatedCapacity", "BA6B3E292E9412C8" },
				{ "Cyberware.CapacityRemaining", "EB1E95164B37C9A6" },
				{ "Gear.CalculatedArmorCapacity", "506740D6A9C105DF" },
				{ "Gear.CalculatedCapacity", "B8C2BF6F731973A3" },
				{ "Gear.CapacityRemaining", "E13B93D7C5ED3311" },
				{ "Gear.PluginArmorCapacity", "50CE0B08188A0A17" },
				{ "Gear.PluginCapacity", "036495AAEAB2A853" },
				{ "TechProgram.CalculatedCapacity", "6D870F89B42E3537" },
				{ "frmSelectArmorMod.UpdateSelectedArmor", "4CDE8691C715E37F" },
				{ "frmSelectGear.UpdateGearInfo", "622D4BED27B67394" },
			};

		[Fact]
		public void NoPortedCalculatorChangedSinceItWasRead()
		{
			IReadOnlyDictionary<string, string> actual = ExpressionChecks.CalculatorFingerprints(PortedCalculators());

			string[] drifted = actual
				.Where(entry => !PinnedFingerprints.ContainsKey(entry.Key)
					|| !string.Equals(PinnedFingerprints[entry.Key], entry.Value, StringComparison.Ordinal))
				.Select(entry => entry.Key + " now hashes to " + entry.Value + ", pinned as "
					+ (PinnedFingerprints.ContainsKey(entry.Key) ? PinnedFingerprints[entry.Key] : "nothing"))
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			Assert.True(drifted.Length == 0,
				"These calculators changed since their port was written. Re-read each one, fix its "
				+ "port, then update the hash:\n  " + string.Join("\n  ", drifted));
		}

		// - a pin without a port would keep watching a member nothing replays any more
		[Fact]
		public void EveryPinNamesAPortedCalculator()
		{
			Assert.Empty(PinnedFingerprints.Keys.Except(PortedCalculators(), StringComparer.Ordinal));
		}

		// - an entity reading avail unguarded with no port would have its values go unchecked
		[Fact]
		public void EveryEntityThatRequiresAvailHasAPort()
		{
			Assert.Equal(
				EntitiesRequiring("avail"),
				AvailabilityExpressions.Rules.Select(rule => rule.Entity).OrderBy(entity => entity, StringComparer.Ordinal));
		}

		[Fact]
		public void EveryExpressionRuleReachesEntries()
		{
			string[] empty = ExpressionChecks.AllRules
				.Where(rule => DataPaths.ExpressionCheckFor(rule.Id).ItemCount == 0)
				.Select(rule => rule.Id)
				.ToArray();

			Assert.Empty(empty);
		}

		// - Weapon and Lifestyle are left to NumericFields: it must convert their <cost>
		[Fact]
		public void EveryEntityThatRequiresCostHasAPortOrAConversion()
		{
			string[] ported = CostExpressions.Rules.Select(rule => rule.Entity).ToArray();
			string[] uncovered = EntitiesRequiring("cost")
				.Where(entity => !ported.Contains(entity))
				.Where(entity => !NumericFields.ConvertedFields.ContainsKey(entity)
					|| !NumericFields.ConvertedFields[entity].Any(field => field.Path == "cost"))
				.ToArray();

			Assert.Empty(uncovered);
		}

		// - only these are replayed at 1..limit plus their top end, not over their whole range
		// - all use plain Rating arithmetic today: a new one is a cue to check it rates alike
		[Fact]
		public void OnlyTheKnownEntriesExceedTheExhaustiveRatingLimit()
		{
			Assert.Equal(
				new[]
				{
					"Bioware: Essence Hole (600)",
					"Cyberware: Essence Hole (600)",
					"Gear: Certified Credstick, Ebony (1000000)",
					"Gear: Certified Credstick, Gold (100000)",
					"Gear: Certified Credstick, Platinum (500000)",
					"Gear: Certified Credstick, Silver (20000)",
					"Gear: Certified Credstick, Standard (5000)",
				},
				ExpressionChecks.AllRules
					.SelectMany(rule => DataPaths.ExpressionCheckFor(rule.Id).CutShort)
					.Distinct(StringComparer.Ordinal)
					.OrderBy(entry => entry, StringComparer.Ordinal));
		}

		private static IEnumerable<string> PortedCalculators()
		{
			return AvailabilityExpressions.Calculators.Concat(CostExpressions.Calculators).Concat(EssExpressions.Calculators)
				.Concat(CapacityExpressions.Calculators).Distinct(StringComparer.Ordinal);
		}

		private static IEnumerable<string> EntitiesRequiring(string field)
		{
			return DataPaths.RequiredFieldRules
				.Where(rule => rule.RequiredFields.Contains(field))
				.Select(rule => rule.Entity)
				.OrderBy(entity => entity, StringComparer.Ordinal);
		}
	}
}
