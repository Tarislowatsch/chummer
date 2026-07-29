using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - undeclared values are unreachable: dropdown frmSelectWeapon.cs:47, query frmSelectWeapon.cs:82
	// - translation matches the block (clsXmlManager.cs:226): raw text prints (clsEquipment.cs:304)
	// - scope is decided in DataPaths.CategoryDeclarationBlockOverrides, off the consuming code
	public class CategoryDeclarationTests
	{
		// - declaring one of these is a visible behaviour change to make deliberately, not a tidy-up
		// - "Periphirals" and "Paranroaml" mirror typos in the data, not typos in this list
		// - format: <file>/<collection>/<category>, one line per entry
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

		// Same reason the duplicate allowlist has a staleness check: see NameUniquenessTests.
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

		// Deferred categories are copied verbatim from the failure output into the list.
		private static string Entry(string xmlPath, string collectionName, string category)
		{
			return Path.GetFileName(xmlPath) + "/" + collectionName + "/" + category;
		}
	}
}
