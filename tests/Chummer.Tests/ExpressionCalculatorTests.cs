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

		private static IEnumerable<string> PortedCalculators()
		{
			return AvailabilityExpressions.Calculators;
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
