using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Chummer.Tests
{
	// - the assembly runs from bin\Debug\net48, not the repo root
	// - every path is found by walking up from the assembly to the repo's marker file
	public static class DataPaths
	{
		public static string RepoRoot { get; } = FindRepoRoot();

		public static string ChummerDataDir => Path.Combine(RepoRoot, "Chummer", "data");

		public static string ChummerLangDir => Path.Combine(RepoRoot, "Chummer", "lang");

		public static string ChummerSheetsDir => Path.Combine(RepoRoot, "Chummer", "sheets");

		// - one MemberData entry per file: a bad file fails its own case, not the whole directory
		public static IEnumerable<object[]> RuleXmlFiles()
		{
			return Directory.EnumerateFiles(ChummerDataDir, "*.xml", SearchOption.AllDirectories)
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(path => new object[] { path });
		}

		public static IEnumerable<object[]> LangXmlFiles()
		{
			return Directory.EnumerateFiles(ChummerLangDir, "*.xml", SearchOption.AllDirectories)
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(path => new object[] { path });
		}

		public static IEnumerable<object[]> SheetXslFiles()
		{
			// - not two globs: .NET Framework's legacy 8.3 rule makes "*.xsl" also match "*.xslt"
			// - xUnit masked the duplicates only by dropping colliding theory ids
			// - filtering on the real extension sidesteps the quirk
			return Directory.EnumerateFiles(ChummerSheetsDir, "*", SearchOption.AllDirectories)
				.Where(path =>
				{
					string extension = Path.GetExtension(path);
					return string.Equals(extension, ".xsl", StringComparison.OrdinalIgnoreCase)
						|| string.Equals(extension, ".xslt", StringComparison.OrdinalIgnoreCase);
				})
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(path => new object[] { path });
		}

		// - File.Exists pairing lets character.xsd and improvements.xml drop out on their own
		// - top-level only: "custom content/<pack>/" pairs names to schemas inconsistently
		// - the exact-count guard in DataPathsTests makes a silently dropped pair fail loudly
		public static IEnumerable<object[]> TopLevelRuleXmlWithSchemaFiles()
		{
			return Directory.EnumerateFiles(ChummerDataDir, "*.xml", SearchOption.TopDirectoryOnly)
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(path => new { XmlPath = path, XsdPath = Path.ChangeExtension(path, ".xsd") })
				.Where(pair => File.Exists(pair.XsdPath))
				.Select(pair => new object[] { pair.XmlPath, pair.XsdPath });
		}

		// - the app keys packs by name AND category at every lookup site
		// - sites: frmSelectPACKSKit.cs:111 and :715, frmCreatePACKSKit.cs:51, frmCreate.cs:20300
		// - anything absent here is keyed by <name> alone
		private static readonly IReadOnlyDictionary<string, string[]> CompositeKeyFields =
			new Dictionary<string, string[]>(StringComparer.Ordinal)
			{
				{ "packs.xml/packs", new[] { "name", "category" } },
			};

		private static readonly string[] NameOnlyKeyFields = { "name" };

		// - a control character: a printable separator could be forged by a legitimate value
		public const string KeyFieldSeparator = "\u001F";

		private static string[] KeyFieldsFor(string xmlFileName, string collectionName)
		{
			string[] fields;
			return CompositeKeyFields.TryGetValue(xmlFileName + "/" + collectionName, out fields)
				? fields
				: NameOnlyKeyFields;
		}

		// - override map, not inclusion list: a new collection is checked unless deliberately exempted
		// - a collection is governed by whichever block code resolves its <category> against
		private static readonly IReadOnlyDictionary<string, string> CategoryDeclarationBlockOverrides =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				// - frmSelectVehicleMod.cs:585 resolves mod categories against <modcategories>
				// - clsEquipment.cs:13453 and :13628 hit the vehicle block instead, a bug tracked elsewhere
				{ "vehicles.xml/mods", "modcategories" },
			};

		// - not null values in the map above: "redirected" and "governed by nothing" fail differently
		// - armor.xml/mods stays in scope: ArmorMod translates its categories (clsEquipment.cs:107)
		private static readonly HashSet<string> CollectionsWithoutCategoryDeclarations =
			new HashSet<string>(StringComparer.Ordinal)
			{
				// - WeaponMod never resolves <category> (clsEquipment.cs:8021-8029)
				"weapons.xml/mods",
				// - TechProgramOption has no category member (clsUnique.cs:6144)
				// - frmSelectProgramOption.cs:37 groups by programtype, not <category>
				// - the broken lookup at clsUnique.cs:5868 belongs to programs.xml/programs, not here
				"programs.xml/options",
			};

		// - pickers build their lists from the governing block (frmSelectWeapon.cs:47 and :82)
		// - translations attach only to declared nodes (clsXmlManager.cs:226)
		// - an untranslated category goes onto the printed sheet (clsEquipment.cs:304)
		private const string DefaultCategoryDeclarationBlock = "categories";

		// - built from the override map: a new override cannot leave its target block ungathered
		private static readonly HashSet<string> DeclarationBlockNames =
			new HashSet<string>(
				new[] { DefaultCategoryDeclarationBlock }.Concat(CategoryDeclarationBlockOverrides.Values),
				StringComparer.Ordinal);

		// - one collection reduced to its items' lookup keys, all the uniqueness check needs
		public sealed class RuleCollection
		{
			public RuleCollection(string filePath, string name, string[] keyFields,
				IReadOnlyList<string> itemKeys)
			{
				FilePath = filePath;
				Name = name;
				KeyFields = keyFields;
				ItemKeys = itemKeys;
			}

			public string FilePath { get; }

			public string Name { get; }

			public string[] KeyFields { get; }

			public IReadOnlyList<string> ItemKeys { get; }
		}

		// One <source> element: a reference to a book code declared in books.xml.
		public sealed class BookReference
		{
			public BookReference(string code, string itemName)
			{
				Code = code;
				ItemName = itemName;
			}

			public string Code { get; }

			// - nearest enclosing element with a <name>, so a failure names the entry, not just the file
			public string ItemName { get; }
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

		// - everything the data tests need from one file, so it is parsed exactly once for all of them
		public sealed class RuleFile
		{
			public RuleFile(string filePath, IReadOnlyList<RuleCollection> collections,
				IReadOnlyList<BookReference> bookReferences,
				IReadOnlyDictionary<string, IReadOnlyCollection<string>> categoryDeclarations,
				IReadOnlyList<CategoryUsage> categoryUsages,
				IReadOnlyList<RequiredFieldContract> requiredFieldContracts)
			{
				FilePath = filePath;
				Collections = collections;
				BookReferences = bookReferences;
				CategoryDeclarations = categoryDeclarations;
				CategoryUsages = categoryUsages;
				RequiredFieldContracts = requiredFieldContracts;
			}

			public string FilePath { get; }

			public string FileName => Path.GetFileName(FilePath);

			public IReadOnlyList<RuleCollection> Collections { get; }

			public IReadOnlyList<BookReference> BookReferences { get; }

			// - declaration block name -> its values, gathered only for names in DeclarationBlockNames
			// - a file with none is simply absent from the category check
			public IReadOnlyDictionary<string, IReadOnlyCollection<string>> CategoryDeclarations { get; }

			public IReadOnlyList<CategoryUsage> CategoryUsages { get; }

			// - one per required-field rule declared against this file, already evaluated
			public IReadOnlyList<RequiredFieldContract> RequiredFieldContracts { get; }
		}

		// - parsed once: the suite was reparsing the same documents roughly five times over
		// - cached as immutable strings: XmlDocument is not safe under xUnit's parallel classes
		// - every derived check reads these records instead of reopening the corpus
		private static readonly Lazy<IReadOnlyList<RuleFile>> CachedRuleFiles =
			new Lazy<IReadOnlyList<RuleFile>>(LoadTopLevelRuleFiles);

		private static readonly Lazy<IReadOnlyList<RuleCollection>> CachedRuleCollections =
			new Lazy<IReadOnlyList<RuleCollection>>(
				() => CachedRuleFiles.Value.SelectMany(file => file.Collections).ToArray());

		// - a collection qualifies when its items carry a direct <name> child, read off the data
		// - that rule alone excludes <version>, the declaration blocks and skills.xml's wrappers
		// - top-level only, in step with the schema pairing: "custom content/" is a separate concern
		public static IEnumerable<object[]> TopLevelRuleXmlCollections()
		{
			return CachedRuleCollections.Value
				.Select(collection => new object[] { collection.FilePath, collection.Name });
		}

		public static RuleCollection RuleCollectionFor(string xmlPath, string collectionName)
		{
			return CachedRuleCollectionsByKey.Value[IndexKey(xmlPath, collectionName)];
		}

		// - the book codes books.xml declares, the set every <source> has to land in
		// - parsed on its own: /chummer/books/book/code is a shape no other file shares
		public static IReadOnlyCollection<string> BookCodes => CachedBookCodes.Value;

		private static readonly Lazy<IReadOnlyCollection<string>> CachedBookCodes =
			new Lazy<IReadOnlyCollection<string>>(LoadBookCodes);

		private static IReadOnlyCollection<string> LoadBookCodes()
		{
			XmlDocument document = new XmlDocument { XmlResolver = null };
			document.Load(Path.Combine(ChummerDataDir, "books.xml"));

			return new HashSet<string>(
				document.SelectNodes("/chummer/books/book/code").Cast<XmlNode>()
					.Select(node => node.InnerText),
				StringComparer.Ordinal);
		}

		// - only files that cite a book: the rest would contribute empty, always-green cases
		// - the exact-count guard in DataPathsTests keeps that omission from quietly growing
		public static IEnumerable<object[]> TopLevelRuleXmlFilesCitingBooks()
		{
			return CachedRuleFiles.Value
				.Where(file => file.BookReferences.Count > 0)
				.Select(file => new object[] { file.FilePath });
		}

		public static IReadOnlyList<BookReference> BookReferencesFor(string xmlPath)
		{
			return CachedRuleFilesByPath.Value[xmlPath].BookReferences;
		}

		private static readonly Lazy<IReadOnlyDictionary<string, RuleFile>> CachedRuleFilesByPath =
			new Lazy<IReadOnlyDictionary<string, RuleFile>>(
				() => CachedRuleFiles.Value.ToDictionary(file => file.FilePath, StringComparer.Ordinal));

		// - what a single collection owes its declaration block: values used against values declared
		public sealed class CategoryContract
		{
			public CategoryContract(string filePath, string collectionName, string declarationBlock,
				IEnumerable<string> declaredCategories, IReadOnlyList<CategoryUsage> usages)
			{
				FilePath = filePath;
				CollectionName = collectionName;
				DeclarationBlock = declarationBlock;
				// - ordinal and rebuilt here, as in ReferencesToUndeclaredBooks: the XPath match is exact
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
				return Usages.Where(usage => !DeclaredCategories.Contains(usage.Category));
			}
		}

		// - in scope when items carry <category>, the governing block exists and no exemption applies
		// - lifestyles.xml and ranges.xml carry categories no block declares, so they have no contract
		public static IEnumerable<object[]> CategoryKeyedCollections()
		{
			return CachedCategoryContracts.Value
				.Select(contract => new object[] { contract.FilePath, contract.CollectionName });
		}

		public static CategoryContract CategoryContractFor(string xmlPath, string collectionName)
		{
			return CachedCategoryContractsByKey.Value[IndexKey(xmlPath, collectionName)];
		}

		private static readonly Lazy<IReadOnlyList<CategoryContract>> CachedCategoryContracts =
			new Lazy<IReadOnlyList<CategoryContract>>(BuildCategoryContracts);

		private static readonly Lazy<IReadOnlyDictionary<string, CategoryContract>> CachedCategoryContractsByKey =
			new Lazy<IReadOnlyDictionary<string, CategoryContract>>(
				() => CachedCategoryContracts.Value.ToDictionary(
					contract => IndexKey(contract.FilePath, contract.CollectionName), StringComparer.Ordinal));

		private static IReadOnlyList<CategoryContract> BuildCategoryContracts()
		{
			List<CategoryContract> contracts = new List<CategoryContract>();

			foreach (RuleFile file in CachedRuleFiles.Value)
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

			string key = OverrideKey(xmlFileName, collectionName);
			if (CollectionsWithoutCategoryDeclarations.Contains(key))
			{
				block = null;
				return false;
			}

			bool redirected = CategoryDeclarationBlockOverrides.TryGetValue(key, out block);
			if (!redirected)
				block = DefaultCategoryDeclarationBlock;

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

		// - file *name*, not IndexKey's full path: hand-written entries have to stay readable
		private static string OverrideKey(string xmlFileName, string collectionName)
		{
			return xmlFileName + "/" + collectionName;
		}

		// - every hand-written scope exception, for the staleness guard
		// - covers what the redirect throw cannot: a key naming a collection that no longer exists
		public static IEnumerable<string> CategoryScopeExceptionKeys()
		{
			return CategoryDeclarationBlockOverrides.Keys.Concat(CollectionsWithoutCategoryDeclarations)
				.OrderBy(key => key, StringComparer.Ordinal);
		}

		// - the universe the exception keys above have to name a member of
		public static IEnumerable<string> CollectionsUsingCategories()
		{
			return CachedRuleFiles.Value
				.SelectMany(file => file.CategoryUsages
					.Select(usage => OverrideKey(file.FileName, usage.CollectionName)))
				.Distinct(StringComparer.Ordinal);
		}

		// - these categories pick the class a gear entry becomes (frmCreate.cs:9287-9298)
		// - each class has its own Create, so one contract over all of gear.xml would average three
		// - the third rule is "not the other two" so a new category cannot fall out of the check
		private const string CommlinkCategories =
			"category = 'Commlink' or category = 'Commlink Upgrade'";

		private const string OperatingSystemCategories =
			"category = 'Commlink Operating System' or category = 'Commlink Operating System Upgrade'";

		// - per entity type, the elements its Create dereferences unguarded, cited to the exact lines
		// - the empty catch is the only optionality marker the code has
		// - hand-derived, not parsed from C#: telling real guards from guard-shaped reads takes judgement
		private static readonly IReadOnlyList<RequiredFieldRule> DeclaredRequiredFieldRules = new[]
		{
			// clsUnique.cs:1036-1051
			Rule("Quality", "qualities.xml", "/chummer/qualities/quality",
				"name", "bp", "category", "source", "page"),
			// clsUnique.cs:3565-3576
			Rule("Spell", "spells.xml", "/chummer/spells/spell",
				"name", "descriptor", "category", "type", "range", "damage", "duration", "dv",
				"source", "page"),
			// clsUnique.cs:4732-4734, reached with a metamagic.xml node (frmCreate.cs:7379)
			Rule("Metamagic", "metamagic.xml", "/chummer/metamagics/metamagic",
				"name", "source", "page"),
			// - the same Create, reached with an echoes.xml node when it is not (frmCreate.cs:7385)
			Rule("Echo", "echoes.xml", "/chummer/echoes/echo",
				"name", "source", "page"),
			// - clsUnique.cs:5582-5589, with <maxrating> present-but-empty allowed (:5589 tests for "")
			Rule("TechProgram", "programs.xml", "/chummer/programs/program",
				"name", "category", "source", "page", "capacity", "skill", "maxrating"),
			// clsUnique.cs:6172-6176, same empty-but-present <maxrating> as above
			Rule("TechProgramOption", "programs.xml", "/chummer/options/option",
				"name", "source", "page", "maxrating"),
			// clsUnique.cs:6535-6537
			Rule("MartialArt", "martialarts.xml", "/chummer/martialarts/martialart",
				"name", "source", "page"),
			// - clsUnique.cs:6781, nested under its art (frmCreate.cs:9222 selects it through the art)
			Rule("MartialArtAdvantage", "martialarts.xml",
				"/chummer/martialarts/martialart/advantages/advantage",
				"name"),
			// clsUnique.cs:6932-6934
			Rule("MartialArtManeuver", "martialarts.xml", "/chummer/maneuvers/maneuver",
				"name", "source", "page"),
			// clsUnique.cs:7539-7546
			Rule("CritterPower", "critterpowers.xml", "/chummer/powers/power",
				"name", "category", "type", "action", "range", "duration", "source", "page"),
			// clsEquipment.cs:82-92
			Rule("ArmorMod", "armor.xml", "/chummer/mods/mod",
				"name", "category", "armorcapacity", "b", "i", "maxrating", "avail", "cost",
				"source", "page"),
			// - clsEquipment.cs:946-953, plus :977 reading <cost> unguarded to test it for a variable price
			Rule("Armor", "armor.xml", "/chummer/armors/armor",
				"name", "category", "b", "i", "armorcapacity", "avail", "cost", "source", "page"),
			// clsEquipment.cs:2364-2375, plus :2450 reading <cost> unguarded
			Rule("Cyberware", "cyberware.xml", "/chummer/cyberwares/cyberware",
				"name", "category", "ess", "capacity", "avail", "cost", "source", "page"),
			// - the same Create with a bioware.xml node (it picks its XPath at clsEquipment.cs:2413)
			Rule("Bioware", "bioware.xml", "/chummer/biowares/bioware",
				"name", "category", "ess", "capacity", "avail", "cost", "source", "page"),
			// clsEquipment.cs:4235-4261
			Rule("Weapon", "weapons.xml", "/chummer/weapons/weapon",
				"name", "category", "type", "reach", "damage", "ap", "mode", "ammo", "rc",
				"avail", "cost", "source", "page"),
			// clsEquipment.cs:7246-7257
			Rule("WeaponAccessory", "weapons.xml", "/chummer/accessories/accessory",
				"name", "avail", "cost", "source", "page"),
			// clsEquipment.cs:7927-7938
			Rule("WeaponMod", "weapons.xml", "/chummer/mods/mod",
				"name", "slots", "avail", "cost", "source", "page"),
			// clsEquipment.cs:8863-8868
			Rule("Lifestyle", "lifestyles.xml", "/chummer/lifestyles/lifestyle",
				"name", "cost", "dice", "multiplier", "source", "page"),
			// - clsEquipment.cs:9552-9665, on all gear.xml holds bar the two commlink classes below
			Rule("Gear", "gear.xml",
				"/chummer/gears/gear[not(" + CommlinkCategories + " or " + OperatingSystemCategories + ")]",
				"name", "category", "avail", "rating", "source", "page"),
			// - clsEquipment.cs:11952-11996, the Gear fields plus <response>/<signal>
			// - the base class reads those two in a try, this override reads them unguarded
			Rule("Commlink", "gear.xml", "/chummer/gears/gear[" + CommlinkCategories + "]",
				"name", "category", "avail", "rating", "source", "page", "response", "signal"),
			// - clsEquipment.cs:12819-12856, likewise with <firewall>/<system> as the unguarded pair
			Rule("OperatingSystem", "gear.xml", "/chummer/gears/gear[" + OperatingSystemCategories + "]",
				"name", "category", "avail", "rating", "source", "page", "firewall", "system"),
			// clsEquipment.cs:13336-13437
			Rule("VehicleMod", "vehicles.xml", "/chummer/mods/mod",
				"name", "category", "slots", "avail", "source", "page"),
			// clsEquipment.cs:14406-14425
			Rule("Vehicle", "vehicles.xml", "/chummer/vehicles/vehicle",
				"name", "category", "handling", "accel", "speed", "pilot", "body", "armor",
				"sensor", "avail", "cost", "source", "page"),

			// - nested reference nodes a Create walks, held to the fields it reads off them
			// - clsEquipment.cs:9935 resolves a child by name AND category, then recurses (:9968)
			// - descendant axis: one anchored depth would leave deeper <usegear> nodes unchecked
			Rule("Gear child <usegear> reference", "gear.xml",
				"/chummer/gears/gear//gears/usegear",
				"name", "category"),
			// - clsEquipment.cs:9827-9828, a second child shape read in place rather than resolved
			Rule("Gear child <gear> reference", "gear.xml",
				"/chummer/gears/gear/gears/gear",
				"name", "category"),
			// clsEquipment.cs:14540
			Rule("Vehicle built-in weapon reference", "vehicles.xml",
				"/chummer/vehicles/vehicle/weapons/weapon",
				"name"),
			// - clsEquipment.cs:14595 and :14617 guard the wrapper but not the <name> inside
			// - no vehicle uses either today, so both rules match nothing
			// - declared anyway: the first entry to reach the live path must not arrive unchecked
			Rule("Vehicle built-in weapon accessory reference", "vehicles.xml",
				"/chummer/vehicles/vehicle/weapons/weapon/accessories/accessory",
				"name"),
			Rule("Vehicle built-in weapon mod reference", "vehicles.xml",
				"/chummer/vehicles/vehicle/weapons/weapon/mods/mod",
				"name"),

			// - clsEquipment.cs:4346 reads <mount> only while a weapon builds the accessory in
			// - required of a referenced accessory and of no other
			// - the node-set comparison is true when any weapon's reference matches the <name>
			Rule("WeaponAccessory built into a weapon", "weapons.xml",
				"/chummer/accessories/accessory[name = /chummer/weapons/weapon/accessories/accessory]",
				"mount"),
		};

		private static RequiredFieldRule Rule(string entity, string fileName, string itemXPath,
			params string[] requiredFields)
		{
			return new RequiredFieldRule(entity, fileName, itemXPath, requiredFields);
		}

		// One entity type's required-field rule, as read off its Create method.
		public sealed class RequiredFieldRule
		{
			public RequiredFieldRule(string entity, string fileName, string itemXPath,
				IReadOnlyList<string> requiredFields)
			{
				Entity = entity;
				FileName = fileName;
				ItemXPath = itemXPath;
				RequiredFields = requiredFields;
			}

			// - the theory's case id, so a failure names the class whose Create is the reason
			// - unique on purpose: file-sharing entities would collide and xUnit would drop a case
			public string Entity { get; }

			public string FileName { get; }

			public string ItemXPath { get; }

			public IReadOnlyList<string> RequiredFields { get; }
		}

		// One entry that is missing a field its Create would dereference.
		public sealed class MissingField
		{
			public MissingField(string itemName, string field)
			{
				ItemName = itemName;
				Field = field;
			}

			public string ItemName { get; }

			public string Field { get; }
		}

		public sealed class RequiredFieldContract
		{
			public RequiredFieldContract(RequiredFieldRule rule, string filePath, int itemCount,
				IReadOnlyList<MissingField> missingFields)
			{
				Rule = rule;
				FilePath = filePath;
				ItemCount = itemCount;
				MissingFields = missingFields;
			}

			public RequiredFieldRule Rule { get; }

			public string FilePath { get; }

			// - kept so a rule that stops matching anything is visible without reparsing
			public int ItemCount { get; }

			public IReadOnlyList<MissingField> MissingFields { get; }
		}

		public static IEnumerable<object[]> EntitiesWithRequiredFields()
		{
			return CachedRequiredFieldContracts.Value
				.Select(contract => new object[] { contract.Rule.Entity });
		}

		public static RequiredFieldContract RequiredFieldContractFor(string entity)
		{
			return CachedRequiredFieldContractsByEntity.Value[entity];
		}

		// - every declared rule, matched or not, for the guards that watch the table itself
		public static IReadOnlyList<RequiredFieldRule> RequiredFieldRules => DeclaredRequiredFieldRules;

		// - matches only a declaration: a call site never names the parameter type
		// - tolerates a line break after the paren
		// - captures the parameter name the fingerprint looks for inside the body
		private static readonly Regex EntityCreateSignature =
			new Regex(@"public\s+void\s+Create\(\s*XmlNode\s+(\w+)", RegexOptions.Compiled);

		private static readonly Regex ClassDeclaration =
			new Regex(@"^\s*public\s+(?:sealed\s+)?class\s+(\w+)", RegexOptions.Compiled);

		// - file:line of every entity Create, the only guard that can see one the table never met
		// - a missed Create drops a whole entity type out of the suite in silence
		// - the whole source tree, so a new entity class in a new file is caught too
		public static IReadOnlyList<string> EntityCreateMethodSites()
		{
			List<string> sites = new List<string>();

			foreach (string path in ApplicationSourceFiles())
			{
				string source = File.ReadAllText(path);

				foreach (Match match in EntityCreateSignature.Matches(source))
				{
					int line = 1 + source.Take(match.Index).Count(character => character == '\n');
					sites.Add(Path.GetFileName(path) + ":" + line);
				}
			}

			return sites;
		}

		// - obj/ is generated and absent from a fresh clone: results would differ between here and CI
		private static IEnumerable<string> ApplicationSourceFiles()
		{
			string generated = Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar;

			return Directory
				.EnumerateFiles(Path.Combine(RepoRoot, "Chummer"), "*.cs", SearchOption.AllDirectories)
				.Where(path => path.IndexOf(generated, StringComparison.Ordinal) < 0)
				.OrderBy(path => path, StringComparer.Ordinal);
		}

		// - the rule table drifts permissive only: a dropped field stays green, nobody checks it
		// - so this hashes, per class, every read of the Create parameter plus the try/catch around it
		// - a changed hash means the rule wants re-deriving against the changed method
		public static IReadOnlyDictionary<string, string> EntityCreateFingerprints()
		{
			Dictionary<string, string> fingerprints = new Dictionary<string, string>(StringComparer.Ordinal);

			foreach (string path in ApplicationSourceFiles())
			{
				foreach (KeyValuePair<string, string> entry in FingerprintsIn(File.ReadAllLines(path)))
					AddFingerprint(fingerprints, entry.Key, entry.Value);
			}

			return fingerprints;
		}

		// - public so the detection tests can drive it with hand-built source
		// - the real methods only show today's hashes, never that a deleted read would change one
		public static IReadOnlyDictionary<string, string> FingerprintsIn(string[] lines)
		{
			Dictionary<string, string> fingerprints = new Dictionary<string, string>(StringComparer.Ordinal);

			// - joined first: a broken-after-the-paren signature must match, as in the sites scan
			string source = string.Join("\n", lines);

			foreach (Match signature in EntityCreateSignature.Matches(source))
			{
				int signatureLine = source.Take(signature.Index).Count(character => character == '\n');

				AddFingerprint(fingerprints, ClassNameAbove(lines, signatureLine),
					Fingerprint(NodeReadsIn(lines, signatureLine, signature.Groups[1].Value)));
			}

			return fingerprints;
		}

		// - the class name is the pinned table's key, so a second Create would silently shadow the first
		private static void AddFingerprint(Dictionary<string, string> fingerprints, string className,
			string fingerprint)
		{
			if (fingerprints.ContainsKey(className))
			{
				throw new InvalidOperationException(
					className + " declares more than one Create(XmlNode ...). The "
					+ "fingerprint is keyed by class, so one of them would go unwatched.");
			}

			fingerprints.Add(className, fingerprint);
		}

		private static string ClassNameAbove(string[] lines, int signatureLine)
		{
			for (int i = signatureLine; i >= 0; i--)
			{
				Match declaration = ClassDeclaration.Match(lines[i]);
				if (declaration.Success)
					return declaration.Groups[1].Value;
			}

			return "(unknown)";
		}

		// - the lines inside one Create that its required-field rule was read off
		// - extent by brace depth: a nested type or lambda cannot end the method early
		private static IEnumerable<string> NodeReadsIn(string[] lines, int signatureLine, string parameter)
		{
			Regex read = new Regex(@"\b" + Regex.Escape(parameter) + @"\s*\[");
			int depth = 0;
			bool opened = false;

			for (int i = signatureLine; i < lines.Length; i++)
			{
				depth += lines[i].Count(character => character == '{')
					- lines[i].Count(character => character == '}');
				opened |= lines[i].IndexOf('{') >= 0;

				string line = lines[i].Trim();
				if (read.IsMatch(line) || line == "try" || line.StartsWith("catch", StringComparison.Ordinal))
					yield return Regex.Replace(line, @"\s+", " ");

				if (opened && depth <= 0)
					yield break;
			}
		}

		private static string Fingerprint(IEnumerable<string> lines)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines)));

				// - 16 hex characters: collision-safe enough while the pinned table stays readable
				return BitConverter.ToString(hash).Replace("-", string.Empty).Substring(0, 16);
			}
		}

		public static IReadOnlyList<RequiredFieldContract> RequiredFieldContracts =>
			CachedRequiredFieldContracts.Value;

		private static readonly Lazy<IReadOnlyList<RequiredFieldContract>> CachedRequiredFieldContracts =
			new Lazy<IReadOnlyList<RequiredFieldContract>>(BuildRequiredFieldContracts);

		private static readonly Lazy<IReadOnlyDictionary<string, RequiredFieldContract>>
			CachedRequiredFieldContractsByEntity =
				new Lazy<IReadOnlyDictionary<string, RequiredFieldContract>>(
					() => CachedRequiredFieldContracts.Value.ToDictionary(
						contract => contract.Rule.Entity, StringComparer.Ordinal));

		private static IReadOnlyList<RequiredFieldContract> BuildRequiredFieldContracts()
		{
			EnsureEntityNamesAreUnique(DeclaredRequiredFieldRules);

			RequiredFieldContract[] contracts = CachedRuleFiles.Value
				.SelectMany(file => file.RequiredFieldContracts)
				.ToArray();

			EnsureEveryRuleWasEvaluated(DeclaredRequiredFieldRules, contracts);

			return contracts;
		}

		// - a duplicated name makes xUnit drop a case while the count guard still reports 29
		// - the lookup then throws a keyless ArgumentException out of a Lazy, blaming nothing
		// - checked on the declarations, before the corpus: that is where the mistake is made
		public static void EnsureEntityNamesAreUnique(IEnumerable<RequiredFieldRule> declared)
		{
			string[] duplicated = declared
				.GroupBy(rule => rule.Entity, StringComparer.Ordinal)
				.Where(group => group.Count() > 1)
				.Select(group => "'" + group.Key + "' declared " + group.Count() + " times")
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			if (duplicated.Length > 0)
			{
				throw new InvalidOperationException(
					"These required-field rules share an entity name. It identifies a theory case, "
					+ "so xUnit would drop the collision and one rule would silently stop being "
					+ "checked:\n  " + string.Join("\n  ", duplicated));
			}
		}

		// - a mistyped file name produces no contract, no theory case and no failure
		// - one entity type's whole contract then stops being checked while the run stays green
		// - public like TryResolveDeclarationBlock: no real run reaches the throw, a test must
		public static void EnsureEveryRuleWasEvaluated(IEnumerable<RequiredFieldRule> declared,
			IEnumerable<RequiredFieldContract> evaluated)
		{
			HashSet<string> covered = new HashSet<string>(
				evaluated.Select(contract => contract.Rule.Entity), StringComparer.Ordinal);

			string[] orphaned = declared
				.Where(rule => !covered.Contains(rule.Entity))
				.Select(rule => rule.Entity + " -> " + rule.FileName)
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			if (orphaned.Length > 0)
			{
				throw new InvalidOperationException(
					"These required-field rules name a file that was not scanned, so their entity "
					+ "types are going unchecked:\n  " + string.Join("\n  ", orphaned));
			}
		}

		// - public so the detection tests can drive it with hand-built XML
		// - the real files show today's data is complete, never that an absence would be reported
		public static RequiredFieldContract EvaluateRequiredFields(RequiredFieldRule rule,
			string filePath, XmlDocument document)
		{
			List<MissingField> missing = new List<MissingField>();
			XmlNodeList items = document.SelectNodes(rule.ItemXPath);

			foreach (XmlNode item in items)
				missing.AddRange(MissingRequiredFieldsIn(item, rule.RequiredFields));

			return new RequiredFieldContract(rule, filePath, items.Count, missing);
		}

		// - presence, not content: an empty element satisfies this, as the code tolerates it
		// - clsUnique.cs:5589 tests <maxrating>'s text for "", so empty is a value the data uses
		// - XmlNode's indexer, the call Create makes: direct children only, exact ordinal match
		public static IEnumerable<MissingField> MissingRequiredFieldsIn(XmlNode item,
			IReadOnlyList<string> requiredFields)
		{
			return requiredFields
				.Where(field => item[field] == null)
				.Select(field => new MissingField(ItemLabel(item), field));
		}

		// - the entry's own <name>, else the nearest ancestor's, else "(unnamed)"
		// - a nameless nested reference is reported under the entry a reader would open
		private static string ItemLabel(XmlNode item)
		{
			return item["name"]?.InnerText ?? NearestItemName(item);
		}

		// - a dictionary, not a scan per call: the allowlist guard would make that quadratic
		// - it also gives file+collection identity one place to be checked, see the throw below
		private static readonly Lazy<IReadOnlyDictionary<string, RuleCollection>> CachedRuleCollectionsByKey =
			new Lazy<IReadOnlyDictionary<string, RuleCollection>>(IndexRuleCollections);

		private static IReadOnlyDictionary<string, RuleCollection> IndexRuleCollections()
		{
			Dictionary<string, RuleCollection> index =
				new Dictionary<string, RuleCollection>(StringComparer.Ordinal);

			foreach (RuleCollection collection in CachedRuleCollections.Value)
			{
				string key = IndexKey(collection.FilePath, collection.Name);
				if (index.ContainsKey(key))
				{
					// - two same-named wrappers collide on a case id and xUnit drops the second
					// - the theory would look healthy while checking half the data
					throw new InvalidOperationException(
						"Two <" + collection.Name + "> collections in "
						+ Path.GetFileName(collection.FilePath)
						+ ". The uniqueness check identifies a collection by file and element "
						+ "name, so this needs an unambiguous identity before it can be checked.");
				}

				index.Add(key, collection);
			}

			return index;
		}

		private static string IndexKey(string xmlPath, string collectionName)
		{
			return xmlPath + "/" + collectionName;
		}

		private static IReadOnlyList<RuleFile> LoadTopLevelRuleFiles()
		{
			List<RuleFile> files = new List<RuleFile>();

			foreach (string xmlPath in Directory
				.EnumerateFiles(ChummerDataDir, "*.xml", SearchOption.TopDirectoryOnly)
				.OrderBy(path => path, StringComparer.Ordinal))
			{
				XmlDocument document = new XmlDocument
				{
					// - matches the XmlReader tests' default: two loaders must not disagree here
					XmlResolver = null
				};
				document.Load(xmlPath);

				XmlElement root = document.DocumentElement;
				if (root == null)
					continue;

				string fileName = Path.GetFileName(xmlPath);
				List<RuleCollection> collections = new List<RuleCollection>();
				List<CategoryUsage> categoryUsages = new List<CategoryUsage>();
				Dictionary<string, IReadOnlyCollection<string>> declarations =
					new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);

				foreach (XmlNode collection in root.ChildNodes)
				{
					if (collection.NodeType != XmlNodeType.Element)
						continue;

					if (DeclarationBlockNames.Contains(collection.Name))
					{
						if (declarations.ContainsKey(collection.Name))
						{
							// - indexer assignment would keep the last block and drop the first
							// - the symptom then blames the data, a long way from the parse
							throw new InvalidOperationException(
								"Two <" + collection.Name + "> blocks in " + fileName
								+ ". The category check reads one declaration set per block, so "
								+ "the second would replace the first and everything declared "
								+ "only in the first would look undeclared.");
						}

						declarations.Add(collection.Name, collection.ChildNodes.Cast<XmlNode>()
							.Where(entry => entry.NodeType == XmlNodeType.Element)
							.Select(entry => entry.InnerText)
							.ToArray());
						continue;
					}

					categoryUsages.AddRange(CategoryUsagesIn(collection));

					string[] keyFields = KeyFieldsFor(fileName, collection.Name);
					IReadOnlyList<string> itemKeys = ItemKeysIn(collection, keyFields);

					if (itemKeys.Count == 0)
						continue;

					collections.Add(new RuleCollection(xmlPath, collection.Name, keyFields, itemKeys));
				}

				files.Add(new RuleFile(xmlPath, collections, BookReferencesIn(document),
					declarations, categoryUsages,
					RequiredFieldContractsFor(fileName, xmlPath, document)));
			}

			return files;
		}

		// - inside the single parse: these rules would otherwise reopen a third of the corpus
		private static IReadOnlyList<RequiredFieldContract> RequiredFieldContractsFor(string fileName,
			string xmlPath, XmlDocument document)
		{
			return DeclaredRequiredFieldRules
				.Where(rule => string.Equals(rule.FileName, fileName, StringComparison.Ordinal))
				.Select(rule => EvaluateRequiredFields(rule, xmlPath, document))
				.ToArray();
		}

		// - every <source>, at any depth, the opposite axis from the anchored uniqueness check
		// - <source> has no definition/reference duality to conflate, unlike <name>
		// - anchoring to collection items would silently drop the metavariant references
		public static IReadOnlyList<BookReference> BookReferencesIn(XmlDocument document)
		{
			return document.SelectNodes("//source").Cast<XmlNode>()
				.Select(node => new BookReference(node.InnerText, NearestItemName(node)))
				.ToArray();
		}

		// - one implementation for the real files and the hand-built cases that prove it works
		// - ordinal and verbatim: Options.BookXPath() (clsOptions.cs:2086-2090) compares exactly
		// - the set is rebuilt here so a caller's comparer cannot defeat the test next door
		public static IEnumerable<BookReference> ReferencesToUndeclaredBooks(
			IEnumerable<BookReference> references, IEnumerable<string> declaredCodes)
		{
			HashSet<string> declared = new HashSet<string>(declaredCodes, StringComparer.Ordinal);

			return references.Where(reference => !declared.Contains(reference.Code));
		}

		// - the closest ancestor with a <name>: a metavariant, not the metatype holding it
		// - the more specific of the two is the one a reader would go looking for
		private static string NearestItemName(XmlNode node)
		{
			for (XmlNode ancestor = node.ParentNode; ancestor != null; ancestor = ancestor.ParentNode)
			{
				XmlElement name = (ancestor as XmlElement)?["name"];
				if (name != null)
					return name.InnerText;
			}

			return "(unnamed)";
		}

		// - public like BookReferencesIn, carrying the opposite axis decision of the pair
		// - only a hand-built case keeps the two from quietly converging
		public static IEnumerable<CategoryUsage> CategoryUsagesIn(XmlNode collection)
		{
			// - direct children only: a picker lists weapon[category = "..."] at that level alone
			// - not BookReferencesIn's deep axis: a deeper <category> is on a reference no dropdown selects
			return collection.ChildNodes.Cast<XmlNode>()
				.Where(item => item.NodeType == XmlNodeType.Element && item["category"] != null)
				.Select(item => new CategoryUsage(collection.Name, item["category"].InnerText,
					item["name"]?.InnerText ?? "(unnamed)"));
		}

		// - public so the uniqueness tests can drive it with hand-built XML
		// - the real files show today's data is clean, never that a duplicate would be caught
		public static IReadOnlyList<string> ItemKeysIn(XmlNode collection, string[] keyFields)
		{
			return collection.ChildNodes.Cast<XmlNode>()
				.Where(item => item.NodeType == XmlNodeType.Element && item["name"] != null)
				.Select(item => BuildKey(item, keyFields))
				.ToArray();
		}

		// - one implementation for the real files and the hand-built cases that prove it works
		// - ordinal here for the same reason as in BuildKey below
		public static IEnumerable<KeyValuePair<string, int>> DuplicateItemKeys(
			IReadOnlyList<string> itemKeys)
		{
			return itemKeys
				.GroupBy(key => key, StringComparer.Ordinal)
				.Where(group => group.Count() > 1)
				.Select(group => new KeyValuePair<string, int>(group.Key, group.Count()));
		}

		// - ordinal and verbatim: the application's XPath lookup compares codepoint for codepoint
		// - entries differing in case or padding are separately reachable, not a collision
		// - normalising would look tidy while making the tests disagree with the application
		private static string BuildKey(XmlNode item, string[] keyFields)
		{
			return string.Join(KeyFieldSeparator,
				keyFields.Select(field => item[field]?.InnerText ?? string.Empty));
		}

		private static string FindRepoRoot()
		{
			DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir != null)
			{
				if (File.Exists(Path.Combine(dir.FullName, "ChummerCS.sln")))
					return dir.FullName;
				dir = dir.Parent;
			}

			throw new DirectoryNotFoundException(
				"Could not locate the repo root (ChummerCS.sln) above " + AppContext.BaseDirectory);
		}
	}
}
