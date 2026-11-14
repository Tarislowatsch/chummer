using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Chummer.Tests.Kernel;

namespace Chummer.Tests.Rulebook
{
	// - every catalogue <category> has to be declared in the block the code resolves it against
	public static class CategoryDeclarations
	{
		// - override map, not inclusion list: a new collection is checked unless deliberately exempted
		// - a collection is governed by whichever block code resolves its <category> against
		private static readonly IReadOnlyDictionary<string, string> BlockOverrides =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				// - frmSelectVehicleMod.cs:585 resolves mod categories against <modcategories>
				// - clsEquipment.cs:13453 and :13628 hit the vehicle block instead, a bug tracked elsewhere
				{ "vehicles.xml/mods", "modcategories" },
			};

		// - not null values in the map above: "redirected" and "governed by nothing" fail differently
		// - armor.xml/mods stays in scope: ArmorMod translates its categories (clsEquipment.cs:107)
		private static readonly HashSet<string> CollectionsWithoutDeclarations =
			new HashSet<string>(StringComparer.Ordinal)
			{
				// - WeaponMod never resolves <category> (clsEquipment.cs:8021-8029)
				"weapons.xml/mods",
				// - TechProgramOption has no category member (clsUnique.cs:6144)
				// - frmSelectProgramOption.cs:37 groups by programtype, not <category>
				// - the broken lookup at clsUnique.cs:5868 belongs to programs.xml/programs, not here
				"programs.xml/options",
			};

		// - these weapons enter only through a carrier's <addweapon> or <underbarrel>
		// - declaring them would sell each one on its own in frmSelectWeapon
		private static readonly HashSet<string> CarrierOnlyCategories = new HashSet<string>(StringComparer.Ordinal)
		{
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

		public static IEnumerable<string> CarrierOnlyCategoryKeys()
		{
			return CarrierOnlyCategories;
		}

		public static bool IsCarrierOnly(string xmlFileName, string collectionName, string category)
		{
			return CarrierOnlyCategories.Contains(RuleCorpus.TableKey(xmlFileName, collectionName) + "/" + category);
		}

		// - pickers build their lists from the governing block (frmSelectWeapon.cs:47 and :82)
		// - translations attach only to declared nodes (clsXmlManager.cs:226)
		// - an untranslated category goes onto the printed sheet (clsEquipment.cs:304)
		private const string DefaultBlock = "categories";

		// - built from the override map: a new override cannot leave its target block ungathered
		private static readonly HashSet<string> DeclarationBlockNames =
			new HashSet<string>(
				new[] { DefaultBlock }.Concat(BlockOverrides.Values),
				StringComparer.Ordinal);

		// - a predicate, not the set: callers in other modules must not be able to edit it
		internal static bool IsDeclarationBlock(string elementName)
		{
			return DeclarationBlockNames.Contains(elementName);
		}

		// One catalogue entry's <category> value, named for failure messages.
		public sealed class CategoryUsage
		{
			public CategoryUsage(string collectionName, string category, string itemName)
			{
				CollectionName = collectionName;
				Category = category;
				ItemName = itemName;
			}

			public string CollectionName { get; }

			public string Category { get; }

			public string ItemName { get; }
		}

		// - what a single collection owes its declaration block: values used against values declared
		public sealed class CategoryContract
		{
			public CategoryContract(string filePath, string collectionName, string declarationBlock,
				IEnumerable<string> declaredCategories, IReadOnlyList<CategoryUsage> usages)
			{
				FilePath = filePath;
				CollectionName = collectionName;
				DeclarationBlock = declarationBlock;
				// - ordinal, as in BookReferences.Undeclared: the XPath match is exact
				DeclaredCategories = new HashSet<string>(declaredCategories, StringComparer.Ordinal);
				Usages = usages;
			}

			public string FilePath { get; }

			public string CollectionName { get; }

			// Which block governs - "categories" for almost everything,
			// "modcategories" for vehicle mods.
			public string DeclarationBlock { get; }

			public IReadOnlyCollection<string> DeclaredCategories { get; }

			public IReadOnlyList<CategoryUsage> Usages { get; }

			// - one per affected entry, not per distinct value, so a caller can count or name entries
			// - grouping is left to the caller: message and allowlist guard want different shapes
			public IEnumerable<CategoryUsage> UndeclaredUsages()
			{
				string fileName = System.IO.Path.GetFileName(FilePath);
				return Usages.Where(usage => !DeclaredCategories.Contains(usage.Category)
					&& !IsCarrierOnly(fileName, CollectionName, usage.Category));
			}
		}

		// - in scope when items carry <category>, the governing block exists and no exemption applies
		// - lifestyles.xml and ranges.xml carry categories no block declares, so they have no contract
		public static IEnumerable<object[]> CategoryKeyedCollections()
		{
			return CachedCategoryContracts.Value
				.Select(contract => new object[] { contract.FilePath, contract.CollectionName });
		}

		public static CategoryContract ContractFor(string xmlPath, string collectionName)
		{
			return CachedCategoryContractsByKey.Value[RuleCorpus.CacheKey(xmlPath, collectionName)];
		}

		private static readonly Lazy<IReadOnlyList<CategoryContract>> CachedCategoryContracts =
			new Lazy<IReadOnlyList<CategoryContract>>(BuildCategoryContracts);

		private static readonly Lazy<IReadOnlyDictionary<string, CategoryContract>> CachedCategoryContractsByKey =
			new Lazy<IReadOnlyDictionary<string, CategoryContract>>(
				() => CachedCategoryContracts.Value.ToDictionary(
					contract => RuleCorpus.CacheKey(contract.FilePath, contract.CollectionName),
					StringComparer.Ordinal));

		private static IReadOnlyList<CategoryContract> BuildCategoryContracts()
		{
			List<CategoryContract> contracts = new List<CategoryContract>();

			foreach (RuleCorpus.RuleFile file in RuleCorpus.Files)
			{
				foreach (IGrouping<string, CategoryUsage> collection in file.CategoryUsages
					.GroupBy(usage => usage.CollectionName, StringComparer.Ordinal))
				{
					string block;
					IReadOnlyCollection<string> declared;
					if (!TryResolveDeclarationBlock(file.FileName, collection.Key,
							file.CategoryDeclarations, out block, out declared))
					{
						continue;
					}

					contracts.Add(new CategoryContract(file.FilePath, collection.Key, block,
						declared, collection.ToArray()));
				}
			}

			return contracts;
		}

		// - takes the declarations as an argument so a test can drive both outcomes
		// - false means out of scope: exempted, or the file carries no such block at all
		// - a redirect naming a missing block throws: the typo would silently drop a whole collection
		public static bool TryResolveDeclarationBlock(string xmlFileName, string collectionName,
			IReadOnlyDictionary<string, IReadOnlyCollection<string>> declarations,
			out string block, out IReadOnlyCollection<string> declared)
		{
			declared = null;

			string key = RuleCorpus.TableKey(xmlFileName, collectionName);
			if (CollectionsWithoutDeclarations.Contains(key))
			{
				block = null;
				return false;
			}

			bool redirected = BlockOverrides.TryGetValue(key, out block);
			if (!redirected)
				block = DefaultBlock;

			if (declarations.TryGetValue(block, out declared))
				return true;

			if (redirected)
			{
				throw new InvalidOperationException(
					key + " is redirected to <" + block + ">, which " + xmlFileName
					+ " does not contain. Its categories would go unchecked. Fix the override "
					+ "or point it at a block that exists.");
			}

			return false;
		}

		// - every hand-written scope exception, for the staleness guard
		// - covers what the redirect throw cannot: a key naming a collection that no longer exists
		public static IEnumerable<string> ScopeExceptionKeys()
		{
			return BlockOverrides.Keys.Concat(CollectionsWithoutDeclarations)
				.OrderBy(key => key, StringComparer.Ordinal);
		}

		// - the universe the exception keys above have to name a member of
		public static IEnumerable<string> CollectionsUsingCategories()
		{
			return RuleCorpus.Files
				.SelectMany(file => file.CategoryUsages
					.Select(usage => RuleCorpus.TableKey(file.FileName, usage.CollectionName)))
				.Distinct(StringComparer.Ordinal);
		}

		// - declaration block name -> declared values, for the blocks in DeclarationBlockNames only
		// - shared with the pack checks: base and pack must read their blocks the same way
		public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> DeclarationBlocksIn(
			XmlElement root, string fileName)
		{
			Dictionary<string, IReadOnlyCollection<string>> declarations =
				new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);

			foreach (XmlNode block in root.ChildNodes)
			{
				if (block.NodeType != XmlNodeType.Element || !IsDeclarationBlock(block.Name))
					continue;

				if (declarations.ContainsKey(block.Name))
				{
					// - indexer assignment would keep the last block and drop the first
					// - the symptom then blames the data, a long way from the parse
					throw new InvalidOperationException(
						"Two <" + block.Name + "> blocks in " + fileName
						+ ". The category check reads one declaration set per block, so "
						+ "the second would replace the first and everything declared "
						+ "only in the first would look undeclared.");
				}

				declarations.Add(block.Name, block.ChildNodes.Cast<XmlNode>()
					.Where(entry => entry.NodeType == XmlNodeType.Element)
					.Select(entry => entry.InnerText)
					.ToArray());
			}

			return declarations;
		}

		public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> For(string xmlPath)
		{
			return RuleCorpus.FileAt(xmlPath).CategoryDeclarations;
		}

		// - public like BookReferences.In, carrying the opposite axis decision of the pair
		// - only a hand-built case keeps the two from quietly converging
		public static IEnumerable<CategoryUsage> UsagesIn(XmlNode collection)
		{
			// - direct children only: a picker lists weapon[category = "..."] at that level alone
			// - not the deep axis of BookReferences.In: no picker lists a deeper <category>
			return collection.ChildNodes.Cast<XmlNode>()
				.Where(item => item.NodeType == XmlNodeType.Element && item["category"] != null)
				.Select(item => new CategoryUsage(collection.Name, item["category"].InnerText,
					item["name"]?.InnerText ?? "(unnamed)"));
		}
	}
}
