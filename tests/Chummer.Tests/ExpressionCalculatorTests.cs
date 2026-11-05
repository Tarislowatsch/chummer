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

		private static IEnumerable<string> PortedCalculators()
		{
			return AvailabilityExpressions.Calculators.Concat(CostExpressions.Calculators);
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
