using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Chummer.Tests
{
	// A picker builds its category dropdown from the file's <categories> block and
	// then browses items by the selected value - frmSelectWeapon.cs:47 fills the
	// combo box, frmSelectWeapon.cs:82 queries
	// /chummer/weapons/weapon[category = "..."]. The two halves are joined by
	// nothing but the string. An item whose <category> is missing from that block
	// therefore has no dropdown entry that would ever select it: it is in the data,
	// it is valid, and it is unreachable in the user interface. Silently.
	//
	// Which collections are answerable to which block, and which are not answerable
	// to any, is decided in DataPaths.CategoryDeclarationBlockOverrides - read off
	// the consuming forms rather than off the shape of the data, because the shapes
	// overlap by coincidence in several files.
	public class CategoryDeclarationTests
	{
		private const int MaxUndeclaredInMessage = 20;

		// Categories the data already uses without declaring them. Fixing these is
		// a separate job with a visible consequence: every one of them makes items
		// appear in a dropdown that players have never seen there, so it is a
		// behaviour change to be made deliberately and noted, not a tidy-up to
		// slip in beside the test that found it. Two of the entries are plain
		// typos on the declaration side of the same word ("Periphirals" for
		// Peripherals, "Paranroaml" for Paranormal); the rest are declarations
		// that were simply never written.
		//
		// Entry format: <file>/<collection>/<category>, one line each so a
		// resolved case is removed by deleting one line.
		private static readonly HashSet<string> KnownUndeclaredCategories = new HashSet<string>(StringComparer.Ordinal)
		{
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
				StringBuilder message = new StringBuilder();
				message.Append(Path.GetFileName(xmlPath)).Append(" uses ").Append(unexpected.Length)
					.Append(" category value(s) in <").Append(collectionName)
					.Append("> that <").Append(contract.DeclarationBlock).Append("> does not declare:");
				foreach (var group in unexpected.Take(MaxUndeclaredInMessage))
				{
					message.Append("\n  '").Append(group.Key).Append("' on ").Append(group.Count())
						.Append(" entries, e.g. '").Append(group.First().ItemName).Append("'");
				}
				if (unexpected.Length > MaxUndeclaredInMessage)
				{
					message.Append("\n  ... and ").Append(unexpected.Length - MaxUndeclaredInMessage)
						.Append(" more");
				}
				Assert.Fail(message.ToString());
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
