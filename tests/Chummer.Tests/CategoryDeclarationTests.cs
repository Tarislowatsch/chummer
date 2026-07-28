using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// A declaration block is what a category value has to exist in for anything to
	// resolve it, and the two things that resolve categories both fail silently
	// when it does not.
	//
	// A picker builds its dropdown from the block and then browses items by the
	// selected value - frmSelectWeapon.cs:47 fills the combo box, :82 queries
	// /chummer/weapons/weapon[category = "..."]. The halves are joined by nothing
	// but the string, so an undeclared value has no dropdown entry that would ever
	// select it: the item is in the data, it is valid, and it is unreachable.
	//
	// Translation attaches to the same block and nowhere else. clsXmlManager.cs:226
	// overlays a language file by matching its category text onto an existing node
	// in the base file, so an undeclared value can never receive a translation -
	// and the untranslated string is what lands on the printed sheet
	// (clsEquipment.cs:304). That consequence is milder than unreachability but no
	// less permanent, and it is why a collection nothing lists can still be
	// governed.
	//
	// Which collections answer to which block, and which answer to none, is decided
	// in DataPaths.CategoryDeclarationBlockOverrides - read off the consuming code
	// rather than off the shape of the data, whose blocks overlap by coincidence in
	// several files.
	public class CategoryDeclarationTests
	{
		// Categories the data already uses without declaring them. Fixing these is
		// a separate job with a visible consequence: declaring one either makes
		// items appear in a dropdown that players have never seen there, or starts
		// translating a label that has always printed in English. Either way it is
		// a behaviour change to be made deliberately and noted, not a tidy-up to
		// slip in beside the test that found it. Two of the entries are plain
		// typos on the declaration side of the same word ("Periphirals" for
		// Peripherals, "Paranroaml" for Paranormal); the rest are declarations
		// that were simply never written.
		//
		// Entry format: <file>/<collection>/<category>, one line each so a
		// resolved case is removed by deleting one line.
		private static readonly HashSet<string> KnownUndeclaredCategories = new HashSet<string>(StringComparer.Ordinal)
		{
			"armor.xml/mods/Chemical Seal",
			"armor.xml/mods/Full Body Armor",
			"armor.xml/mods/General",
			"armor.xml/mods/Second Skin Polymer",
			"armor.xml/mods/Victory Liners",
			"critterpowers.xml/powers/Dracoforms",
			"critterpowers.xml/powers/Paranroaml",
			"gear.xml/gears/Mook",
			"gear.xml/gears/Periphirals",
			"gear.xml/gears/Transgenic",
			"vehicles.xml/vehicles/Rotocraft",
			"weapons.xml/weapons/Cyberware",
			"weapons.xml/weapons/Cyberware Blades",
			"weapons.xml/weapons/Cyberware Clubs",
			"weapons.xml/weapons/Cyberware Exotic Melee Weapons",
			"weapons.xml/weapons/Cyberware Exotic Ranged Weapons",
			"weapons.xml/weapons/Cyberware Grenade Launchers",
			"weapons.xml/weapons/Cyberware Heavy Pistols",
			"weapons.xml/weapons/Cyberware Holdouts",
			"weapons.xml/weapons/Cyberware Light Pistols",
			"weapons.xml/weapons/Cyberware Machine Pistols",
			"weapons.xml/weapons/Cyberware Shotguns",
			"weapons.xml/weapons/Cyberware Submachine Guns",
			"weapons.xml/weapons/Cyberware Tasers",
			"weapons.xml/weapons/Cyberware Throwing Weapons",
			"weapons.xml/weapons/Gear",
			"weapons.xml/weapons/Quality",
			"weapons.xml/weapons/Underbarrel Weapons",
		};

		[Theory]
		[MemberData(nameof(DataPaths.CategoryKeyedCollections), MemberType = typeof(DataPaths))]
		public void EveryCategoryUsedIsDeclared(string xmlPath, string collectionName)
		{
			DataPaths.CategoryContract contract = DataPaths.CategoryContractFor(xmlPath, collectionName);

			var unexpected = contract.UndeclaredUsages()
				.GroupBy(usage => usage.Category, StringComparer.Ordinal)
				.Where(group => !KnownUndeclaredCategories.Contains(Entry(xmlPath, collectionName, group.Key)))
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.ToArray();

			if (unexpected.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					Path.GetFileName(xmlPath) + " uses " + unexpected.Length
						+ " category value(s) in <" + collectionName + "> that <"
						+ contract.DeclarationBlock + "> does not declare",
					unexpected,
					group => "'" + group.Key + "' on " + group.Count()
						+ " entries, e.g. '" + group.First().ItemName + "'"));
			}
		}

		// Same reason the duplicate allowlist has one: an entry that stops being a
		// problem - because the declaration was added, or the last item using it
		// was removed - would otherwise sit here forever and cover the next
		// accidental reintroduction of that exact value.
		[Fact]
		public void AllowlistedCategoriesAreAllStillUndeclared()
		{
			HashSet<string> actual = new HashSet<string>(StringComparer.Ordinal);
			foreach (object[] testCase in DataPaths.CategoryKeyedCollections())
			{
				string xmlPath = (string)testCase[0];
				string collectionName = (string)testCase[1];
				foreach (DataPaths.CategoryUsage usage in
					DataPaths.CategoryContractFor(xmlPath, collectionName).UndeclaredUsages())
				{
					actual.Add(Entry(xmlPath, collectionName, usage.Category));
				}
			}

			string[] stale = KnownUndeclaredCategories.Where(entry => !actual.Contains(entry))
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			Assert.True(stale.Length == 0,
				"These categories are declared (or no longer used) and must be removed from "
				+ nameof(KnownUndeclaredCategories) + ":\n  " + string.Join("\n  ", stale));
		}

		// Matches exactly what a failure message prints, so a category somebody
		// decides to defer can be copied straight from the output into the list.
		private static string Entry(string xmlPath, string collectionName, string category)
		{
			return Path.GetFileName(xmlPath) + "/" + collectionName + "/" + category;
		}
	}
}
