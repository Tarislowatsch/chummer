using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Rulebook
{
	// - undeclared values are unreachable: dropdown frmSelectWeapon.cs:47, query frmSelectWeapon.cs:82
	// - translation matches the block (clsXmlManager.cs:226): raw text prints (clsEquipment.cs:304)
	// - scope is set in CategoryDeclarations.BlockOverrides, off the consuming code
	public class CategoryDeclarationTests
	{
		// - declaring one of these is a visible behaviour change to make deliberately, not a tidy-up
		// - format: <file>/<collection>/<category>, one line per entry
		private static readonly HashSet<string> KnownUndeclaredCategories = new HashSet<string>(StringComparer.Ordinal)
		{
			"armor.xml/mods/Chemical Seal",
			"armor.xml/mods/Full Body Armor",
			"armor.xml/mods/General",
			"armor.xml/mods/Second Skin Polymer",
			"armor.xml/mods/Victory Liners",
			"critterpowers.xml/powers/Dracoforms",
			"gear.xml/gears/Mook",
			"gear.xml/gears/Transgenic",
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
		[MemberData(nameof(CategoryDeclarations.CategoryKeyedCollections), MemberType = typeof(CategoryDeclarations))]
		public void EveryCategoryUsedIsDeclared(string xmlPath, string collectionName)
		{
			CategoryDeclarations.CategoryContract contract =
				CategoryDeclarations.ContractFor(xmlPath, collectionName);

			var unexpected = contract.UndeclaredUsages()
				.GroupBy(usage => usage.Category, StringComparer.Ordinal)
				.Where(group => !KnownUndeclaredCategories.Contains(Entry(xmlPath, collectionName, group.Key)))
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNone(
				Path.GetFileName(xmlPath) + " uses " + unexpected.Length
					+ " category value(s) in <" + collectionName + "> that <"
					+ contract.DeclarationBlock + "> does not declare",
				unexpected,
				group => "'" + group.Key + "' on " + group.Count()
					+ " entries, e.g. '" + group.First().ItemName + "'");
		}

		// Same reason the duplicate allowlist has a staleness check: see NameUniquenessTests.
		[Fact]
		public void AllowlistedCategoriesAreAllStillUndeclared()
		{
			HashSet<string> actual = new HashSet<string>(StringComparer.Ordinal);
			foreach (object[] testCase in CategoryDeclarations.CategoryKeyedCollections())
			{
				string xmlPath = (string)testCase[0];
				string collectionName = (string)testCase[1];
				foreach (CategoryDeclarations.CategoryUsage usage in
					CategoryDeclarations.ContractFor(xmlPath, collectionName).UndeclaredUsages())
				{
					actual.Add(Entry(xmlPath, collectionName, usage.Category));
				}
			}

			string[] stale = KnownUndeclaredCategories.Where(entry => !actual.Contains(entry))
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNoneInFull("These categories are declared (or no longer used) and must be removed from "
				+ nameof(KnownUndeclaredCategories), stale);
		}

		// - a renamed collection leaves its hand-written exemption applying to nothing
		// - the count pin in DataPathsTests cannot tell that silence from a legitimate change
		// - both structures share the check: a key belongs to exactly one of them
		[Fact]
		public void CategoryScopeExceptionsAllNameARealCollection()
		{
			string[] known = CategoryDeclarations.CollectionsUsingCategories().ToArray();

			string[] stale = CategoryDeclarations.ScopeExceptionKeys()
				.Where(key => !known.Contains(key))
				.ToArray();

			FailureReport.AssertNoneInFull("These category scope exceptions name a (file, collection) that carries no "
				+ "<category> any more", stale);
		}

		// Deferred categories are copied verbatim from the failure output into the list.
		private static string Entry(string xmlPath, string collectionName, string category)
		{
			return Path.GetFileName(xmlPath) + "/" + collectionName + "/" + category;
		}
	}
}
