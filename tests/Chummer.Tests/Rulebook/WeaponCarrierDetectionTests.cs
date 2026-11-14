using System.Linq;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Rulebook
{
	// - hand-built input: the shipped data shows none of these cases
	public class WeaponCarrierDetectionTests
	{
		[Theory]
		[InlineData(null, null, true)]
		[InlineData(null, "a", true)]
		[InlineData("a", null, true)]
		[InlineData("a", "a", true)]
		[InlineData("a", "b", false)]
		public void APacksCarrierReachesOnlyTheBaseAndThatPack(string carrierScope, string weaponScope, bool reached)
		{
			WeaponCarriers.Weapon weapon = CarrierOnly(weaponScope, "Grenade", "gear addweapon");

			Assert.Equal(!reached, Unreachable(new[] { weapon }, new WeaponCarriers.Carry(carrierScope, "gear addweapon",
				"Grenade", null)).Any());
		}

		// - Gear.Create looks the name up under category = 'Gear' only
		[Fact]
		public void ACarrierWhoseFilterRejectsTheWeaponDoesNotReachIt()
		{
			WeaponCarriers.Weapon weapon = CarrierOnly(null, "Spur", "quality addweapon");

			Assert.Single(Unreachable(new[] { weapon }, new WeaponCarriers.Carry(null, "gear addweapon", "Spur", null)));
		}

		[Fact]
		public void TheRealRulesFilterByCategory()
		{
			WeaponCarriers.Weapon weapon = WeaponCarriers.WeaponsIn(null, Xml.Parse(
				"<chummer><weapons><weapon><name>Spur</name><category>Quality</category></weapon></weapons></chummer>"))
				.Single();

			Assert.Contains("quality addweapon", weapon.AdmittedBy);
			Assert.DoesNotContain("gear addweapon", weapon.AdmittedBy);
		}

		[Fact]
		public void AnUnderbarrelOfAnUnreachableWeaponIsUnreachable()
		{
			WeaponCarriers.Weapon[] weapons =
			{
				CarrierOnly(null, "Parent", "gear addweapon", "weapon underbarrel"),
				CarrierOnly(null, "Child", "weapon underbarrel"),
			};
			WeaponCarriers.Carry underbarrel = new WeaponCarriers.Carry(null, "weapon underbarrel", "Child", "Parent");

			Assert.Equal(new[] { "Parent", "Child" }, Unreachable(weapons, underbarrel).Select(weapon => weapon.Name));
			Assert.Empty(Unreachable(weapons, underbarrel, new WeaponCarriers.Carry(null, "gear addweapon", "Parent", null)));
		}

		private static WeaponCarriers.Weapon CarrierOnly(string scope, string name, params string[] admittedBy)
		{
			return new WeaponCarriers.Weapon(scope, name, true, admittedBy);
		}

		private static WeaponCarriers.Weapon[] Unreachable(WeaponCarriers.Weapon[] weapons,
			params WeaponCarriers.Carry[] carries)
		{
			return WeaponCarriers.Unreachable(weapons, carries).ToArray();
		}
	}
}
