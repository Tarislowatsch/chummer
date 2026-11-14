using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
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
		};

		// - a declared exemption puts carrier weapons on sale while every guard stays silent
		// - pack <categories> join the base block in the picker
		[Fact]
		public void CarrierOnlyCategoriesAreUsedAndUndeclared()
		{
			HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> declared = new HashSet<string>(StringComparer.Ordinal);
			foreach (object[] testCase in CategoryDeclarations.CategoryKeyedCollections())
			{
				string xmlPath = (string)testCase[0];
				string collectionName = (string)testCase[1];
				CategoryDeclarations.CategoryContract contract = CategoryDeclarations.ContractFor(xmlPath, collectionName);
				used.UnionWith(contract.Usages.Select(usage => Entry(xmlPath, collectionName, usage.Category)));
				declared.UnionWith(contract.DeclaredCategories.Select(category => Entry(xmlPath, collectionName, category)));
			}

			foreach (PackFiles.PackFile file in PackFiles.Paired())
			{
				XmlDocument pack = Xml.Load(file.FilePath);
				foreach (XmlNode collection in RuleCorpus.CatalogueCollectionsIn(pack.DocumentElement))
				{
					used.UnionWith(CategoryDeclarations.UsagesIn(collection)
						.Select(usage => Entry(file.BaseFileName, collection.Name, usage.Category)));
				}

				declared.UnionWith(PackDeclarations(file, pack));
			}

			string[] stale = CategoryDeclarations.CarrierOnlyCategoryKeys()
				.Where(key => !used.Contains(key) || declared.Contains(key))
				.OrderBy(key => key, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNoneInFull("These carrier-only categories are declared or no longer used", stale);
		}

		// - every collection of the base file: a pack's block joins in without the pack's items
		private static IEnumerable<string> PackDeclarations(PackFiles.PackFile file, XmlDocument pack)
		{
			IReadOnlyDictionary<string, IReadOnlyCollection<string>> blocks =
				CustomContent.CategoryDeclarationsVisibleTo(file, pack);
			foreach (object[] testCase in CategoryDeclarations.CategoryKeyedCollections()
				.Where(testCase => Path.GetFileName((string)testCase[0]) == file.BaseFileName))
			{
				string collectionName = (string)testCase[1];
				string block;
				IReadOnlyCollection<string> categories;
				if (CategoryDeclarations.TryResolveDeclarationBlock(file.BaseFileName, collectionName, blocks,
						out block, out categories))
				{
					foreach (string category in categories)
						yield return Entry(file.BaseFileName, collectionName, category);
				}
			}
		}

		// - in a carrier-only category, yet no carrier adds them: unreachable
		// - fixing one is a data change for its own ticket: delete the line there
		private static readonly HashSet<string> KnownWeaponsWithoutCarrier = new HashSet<string>(StringComparer.Ordinal)
		{
			"Grenade: Smoke (2050)",
			"Minigrenade: Smoke (2050)",
			"Mortar Round: Anti-Aircraft",
			"Mortar Round: Rocket Assist",
		};

		// - an empty rule set would leave every carrier-only weapon "unreachable" for the wrong reason
		[Fact]
		public void CarrierRulesCoverItemsVehiclesAndKits()
		{
			Assert.Equal(
				new[]
				{
					"armor mod addweapon", "bioware addweapon", "cyberware addweapon", "gear addweapon",
					"pack vehicle weapons", "pack weapons", "quality addweapon", "vehicle weapons", "weapon underbarrel",
				},
				WeaponCarriers.Rules.Select(rule => rule.Id).OrderBy(id => id, StringComparer.Ordinal));
		}

		[Fact]
		public void EveryCarrierOnlyWeaponHasACarrier()
		{
			string[] orphans = WeaponCarriers.Unreachable(WeaponCarriers.WeaponsInData(), WeaponCarriers.CarriesInData())
				.Select(weapon => weapon.Label)
				.Distinct(StringComparer.Ordinal)
				.ToArray();

			string[] differences = orphans.Where(name => !KnownWeaponsWithoutCarrier.Contains(name))
				.Select(name => "no carrier adds: " + name)
				.Concat(KnownWeaponsWithoutCarrier.Where(name => !orphans.Contains(name))
					.Select(name => "carried now or gone, drop it from the list: " + name))
				.OrderBy(line => line, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNoneInFull("Carrier-only weapons and " + nameof(KnownWeaponsWithoutCarrier) + " disagree",
				differences);
		}

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
