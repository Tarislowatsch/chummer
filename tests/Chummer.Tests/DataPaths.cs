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
	// - the test assembly runs from bin\Debug\net48 under tests\Chummer.Tests, not the repo root, so every path here is found by walking up from the running assembly until the repo's marker file turns up, rather than assumed relative to the current directory
	public static class DataPaths
	{
		public static string RepoRoot { get; } = FindRepoRoot();

		public static string ChummerDataDir => Path.Combine(RepoRoot, "Chummer", "data");

		public static string ChummerLangDir => Path.Combine(RepoRoot, "Chummer", "lang");

		public static string ChummerSheetsDir => Path.Combine(RepoRoot, "Chummer", "sheets");

		// - one MemberData entry per file so a bad file fails its own test case, instead of being absorbed into one pass/fail verdict for the whole directory
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
			// - not two EnumerateFiles calls with "*.xsl" and "*.xslt": .NET Framework's file-system globbing still honours the legacy 8.3 short-name rule, under which "*.xsl" also matches "*.xslt", silently duplicating every .xslt file into both result sets
			// - xUnit only stayed correct because it happens to drop theory cases with a colliding ID - a lucky safety net, not something to depend on
			// - enumerating everything once and comparing the actual extension sidesteps the quirk entirely
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

		// - pairs each top-level Chummer/data/*.xml file with its same-named .xsd, built from the .xml side and filtered by File.Exists rather than a hardcoded list of 26 names
		// - so character.xsd (no matching character.xml) and improvements.xml (no matching improvements.xsd) drop out on their own
		// - deliberately SearchOption.TopDirectoryOnly: "custom content/<pack>/" uses inconsistent name-to-schema pairing and is out of scope here
		// - pairing by matching filename is itself a soft spot: a rename touching only the .xml or only the .xsd side drops the pair silently, rather than erroring
		// - accepted because the exact-count guard in DataPathsTests turns that silence into an immediate, loud failure
		public static IEnumerable<object[]> TopLevelRuleXmlWithSchemaFiles()
		{
			return Directory.EnumerateFiles(ChummerDataDir, "*.xml", SearchOption.TopDirectoryOnly)
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(path => new { XmlPath = path, XsdPath = Path.ChangeExtension(path, ".xsd") })
				.Where(pair => File.Exists(pair.XsdPath))
				.Select(pair => new object[] { pair.XmlPath, pair.XsdPath });
		}

		// - which collections carry a composite lookup key instead of <name> alone
		// - the running application decides this, not taste: every keyed pack lookup in the repo selects by name AND category, so two packs sharing a name only collide when their categories match too
		// - deliberate in the data: a kit like "Brawler" is split into an Attribute Kit part and a Gear Kit part carrying one display name
		// - this comment is the sole justification for the one exception in this design, so all four consuming sites are listed here - a future change has to find every one of them:
		//   frmSelectPACKSKit.cs:111 (a kit is picked) and :715 (a custom kit is deleted), frmCreatePACKSKit.cs:51 (a custom kit is saved), frmCreate.cs:20300 (a kit is applied to a character)
		// - if those lookups ever change, this entry has to follow them
		// - the map describes key composition and nothing else: no exclusions, no expected counts, no other test knobs; anything absent is keyed by <name>
		private static readonly IReadOnlyDictionary<string, string[]> CompositeKeyFields =
			new Dictionary<string, string[]>(StringComparer.Ordinal)
			{
				{ "packs.xml/packs", new[] { "name", "category" } },
			};

		private static readonly string[] NameOnlyKeyFields = { "name" };

		// - separates the parts of a composite key: a control character rather than a printable one, so a value legitimately containing the separator cannot forge a collision
		public const string KeyFieldSeparator = "\u001F";

		private static string[] KeyFieldsFor(string xmlFileName, string collectionName)
		{
			string[] fields;
			return CompositeKeyFields.TryGetValue(xmlFileName + "/" + collectionName, out fields)
				? fields
				: NameOnlyKeyFields;
		}

		// - which declaration block a collection's <category> values have to appear in; default is the file's own <categories> block, and the two structures below hold only the deviations
		// - so a collection added to the data later is checked unless somebody deliberately exempts it - an inclusion list would leave new collections silently unchecked instead
		//
		// - the rule, applied uniformly: a collection is governed by a block when some code resolves its <category> against that block
		// - what a failure costs varies, and deliberately does not decide membership - "no dropdown reads it" is not the same as "nothing reads it"
		// - reachability: a picker builds its list from the block and then browses by the selected value (frmSelectWeapon.cs:47 and :82), so an undeclared value is unreachable in the UI
		// - translation: the block is the only place a translated label can attach - clsXmlManager.cs:226 overlays a language file by matching category text against an existing node in the base file, so undeclared means no translation, permanently, and the untranslated category goes onto the printed sheet (ArmorMod.Print, clsEquipment.cs:304)
		// - both are silent, and both are the failure mode this check exists for
		//
		// - the deviations, each read off the consuming code:
		// - vehicles.xml/mods answers to <modcategories>, via frmSelectVehicleMod.cs:585
		// - VehicleMod also resolves its category twice more, at clsEquipment.cs:13453 and :13628, but against /chummer/categories - the *vehicle* block ("Bike", "Car", ...), which no mod category can ever match; those two are a bug, not a second contract, so they are tracked separately rather than encoded here as a pinned defect
		// - weapons.xml/mods answers to nothing: WeaponMod translates its name and page (clsEquipment.cs:8021-8029) and stops there, no category lookup anywhere; every /chummer/mods/mod query in the codebase selects by name, and frmSelectWeaponAccessory browses by mount and book
		// - programs.xml/options answers to nothing either, for a stronger reason: TechProgramOption (clsUnique.cs:6144) has no category member at all, and frmSelectProgramOption.cs:37 groups by programtypes/programtype, not by <category>
		// - not explained by the broken lookup at clsUnique.cs:5868 - that one is TechProgram.DisplayCategory, which belongs to programs.xml/programs, a collection already checked here and clean
		//
		// - armor.xml/mods is deliberately absent: an earlier version exempted it on the grounds that no form lists its categories - true, but beside the point, since ArmorMod resolves them at clsEquipment.cs:107 and :281 into _strAltCategory, which DisplayCategory (:416) prints
		// - its contract is the same translation-only one that keeps vehicles.xml/mods in scope, so exempting one and keeping the other was an inconsistency, not a judgement
		// - the coincidence worth naming is narrower than it first looks: three of armor.xml/mods' eight values also appear in armor.xml's <categories>, and programs.xml/options' "Hacking" likewise - overlap is evidence of nothing either way, only the consuming code decides
		//
		// collections answering to a block other than the default one
		private static readonly IReadOnlyDictionary<string, string> CategoryDeclarationBlockOverrides =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "vehicles.xml/mods", "modcategories" },
			};

		// - collections answering to no block at all, and therefore not checked; deliberately a second structure rather than a null value in the map above
		// - "redirected somewhere else" and "governed by nothing" are different statements that fail differently, and BuildCategoryContracts has to tell them apart - spelling that as block == null would make it a null check wearing the clothes of a comparison
		private static readonly HashSet<string> CollectionsWithoutCategoryDeclarations =
			new HashSet<string>(StringComparer.Ordinal)
			{
				"weapons.xml/mods",
				"programs.xml/options",
			};

		private const string DefaultCategoryDeclarationBlock = "categories";

		// - the block names worth collecting while parsing: derived from the default plus whatever the override map points at, so adding an override cannot leave its target block ungathered
		// - anything else under the root is a catalogue collection, whose InnerText is meaningless as a declaration
		private static readonly HashSet<string> DeclarationBlockNames =
			new HashSet<string>(
				new[] { DefaultCategoryDeclarationBlock }.Concat(CategoryDeclarationBlockOverrides.Values),
				StringComparer.Ordinal);

		// - one collection of catalogue entries, reduced to the lookup keys of its items - that is all the uniqueness check needs
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

			// - nearest enclosing element carrying a <name>, so a failure can say which entry holds the bad code instead of only which file
			public string ItemName { get; }
		}

		// One catalogue entry's <category> value, with enough context to name it
		// in a failure message.
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

		// - everything the data tests need from one top-level rule file, so the file is opened and parsed exactly once for all of them
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

			// - declaration block name -> the values it declares; only blocks named in DeclarationBlockNames are gathered
			// - a file with none is simply absent from the category check
			public IReadOnlyDictionary<string, IReadOnlyCollection<string>> CategoryDeclarations { get; }

			public IReadOnlyList<CategoryUsage> CategoryUsages { get; }

			// - one per required-field rule declared against this file, already evaluated
			public IReadOnlyList<RequiredFieldContract> RequiredFieldContracts { get; }
		}

		// - parsed once, then reused: without this the same documents get reparsed roughly five times over - once to discover the collections, once per theory case, once more per collection for the allowlist guard
		// - measured before caching: one pass over the 3.2 MB corpus costs ~60 ms, and the uniqueness tests accounted for ~70% of the whole suite's runtime re-doing it
		// - deliberately caching immutable string projections rather than the XmlDocument instances themselves: xUnit runs test classes in parallel, and XmlDocument promises thread safety only for static members, so sharing live documents would be a trap for the next test class using this helper - strings cannot have that problem
		// - everything derived from these documents hangs off this one pass for the same reason: the book-code and category checks read from the RuleFile records below rather than opening the corpus again
		private static readonly Lazy<IReadOnlyList<RuleFile>> CachedRuleFiles =
			new Lazy<IReadOnlyList<RuleFile>>(LoadTopLevelRuleFiles);

		private static readonly Lazy<IReadOnlyList<RuleCollection>> CachedRuleCollections =
			new Lazy<IReadOnlyList<RuleCollection>>(
				() => CachedRuleFiles.Value.SelectMany(file => file.Collections).ToArray());

		// - every (file, collection) pair whose entries are identified by <name>; which collections those are is read off the data instead of listed here - a collection qualifies when its items have a direct <name> child
		// - that rule alone excludes <version> (no element children), <categories>, <costs>/<safehousecosts>, <limits> and <modcategories> (their items are <category>/<cost>/<limit> elements holding text, with no <name> inside), and the per-skill-group wrappers in skills.xml, where the items *are* <name> elements rather than elements *having* one
		// - deliberately top-level-only, in step with the schema-validation pairing above: "custom content/<pack>/" is a separate concern
		public static IEnumerable<object[]> TopLevelRuleXmlCollections()
		{
			return CachedRuleCollections.Value
				.Select(collection => new object[] { collection.FilePath, collection.Name });
		}

		public static RuleCollection RuleCollectionFor(string xmlPath, string collectionName)
		{
			return CachedRuleCollectionsByKey.Value[IndexKey(xmlPath, collectionName)];
		}

		// - the 42 book codes books.xml declares - the set every <source> has to land in
		// - read on its own rather than folded into the per-file pass above: the declaration lives at /chummer/books/book/code, a shape no other file shares, and threading it through the generic loop would mean special-casing a filename in the middle of it
		// - one small file parsed once is the cheaper trade
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

		// - one case per top-level file that actually cites a book; files with no <source> at all (books.xml itself, and the handful of lookup tables) are left out rather than contributing empty, always-green cases
		// - the exact-count guard in DataPathsTests is what keeps that omission from quietly growing
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

		// - what a single collection owes its declaration block: the values its items use, and the values that block declares
		public sealed class CategoryContract
		{
			public CategoryContract(string filePath, string collectionName, string declarationBlock,
				IEnumerable<string> declaredCategories, IReadOnlyList<CategoryUsage> usages)
			{
				FilePath = filePath;
				CollectionName = collectionName;
				DeclarationBlock = declarationBlock;
				// - ordinal, and built here rather than taken as given, for the same reason as ReferencesToUndeclaredBooks
				// - the picker matches category = "..." in XPath, which is exact, and the comparison rule belongs with the check that documents it
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

			// - every usage whose value the block does not declare - one per affected entry, not per distinct value, so a caller can count the entries or name one of them
			// - grouping is left to the caller because the two here want different things: the failure message groups by value, the allowlist guard needs the raw values
			public IEnumerable<CategoryUsage> UndeclaredUsages()
			{
				return Usages.Where(usage => !DeclaredCategories.Contains(usage.Category));
			}
		}

		// - every (file, collection) whose <category> is answerable to a declaration block; three conditions have to hold, and each rules out a real case in today's data:
		// - the collection's items carry <category> at all (weapons.xml/accessories has none)
		// - the file declares the governing block: lifestyles.xml and ranges.xml have items with categories but no <categories> block anywhere, so there is no local contract to check for them, and no code reads one either
		// - the collection is not exempted by the override map above
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

		// - which block a collection answers to, given what its file declares
		//
		// - split out of BuildCategoryContracts, taking the declarations as an argument rather than reading the cached corpus: the throw below is otherwise unreachable from a test, since the real override is correct and nothing in a run would ever execute it
		// - the only evidence it works would be a mutation somebody did once by hand; passing declarations in makes both outcomes drivable
		//
		// - false means out of scope, for two reasons deliberately kept apart: a collection listed as answering to nothing is exempt, while a file that carries no such block at all has no local contract - lifestyles.xml and ranges.xml hold categories nothing declares anywhere, which is the data's shape, not a defect
		//
		// - a redirect naming a block the file does not carry is neither: it is a typo in a hand-maintained list, and staying quiet about it takes the whole collection out of the check
		// - misspell "modcategories" and vehicles.xml/mods - 321 entries - stops being examined, leaving the count guard to report "19 instead of 20" with no hint which collection went missing
		// - same reasoning as the duplicate-wrapper throw in IndexRuleCollections: a named failure beats a silence that still looks healthy
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

		// - keyed by file *name*, not full path - deliberately a different key space from IndexKey's, which identifies a cached collection by path; the two must not be conflated, since these entries are written by hand and have to stay readable in a source listing
		private static string OverrideKey(string xmlFileName, string collectionName)
		{
			return xmlFileName + "/" + collectionName;
		}

		// - every hand-written scope exception, for the guard that checks none of them has gone stale
		// - a redirect pointing at a missing block throws while contracts are built; this covers the other direction, where the *key* names a collection that no longer exists and the entry silently stops meaning anything
		public static IEnumerable<string> CategoryScopeExceptionKeys()
		{
			return CategoryDeclarationBlockOverrides.Keys.Concat(CollectionsWithoutCategoryDeclarations)
				.OrderBy(key => key, StringComparer.Ordinal);
		}

		// - which (file, collection) pairs carry <category> at all - the universe the exception keys above have to name a member of
		public static IEnumerable<string> CollectionsUsingCategories()
		{
			return CachedRuleFiles.Value
				.SelectMany(file => file.CategoryUsages
					.Select(usage => OverrideKey(file.FileName, usage.CollectionName)))
				.Distinct(StringComparer.Ordinal);
		}

		// - the gear.xml categories that decide which of three classes a catalogue entry is built into (frmCreate.cs:9287-9298)
		// - each of the three has its own Create with its own set of unprotected reads
		// - one contract over all of gear.xml would therefore be three contracts averaged into one
		// - XPath fragments rather than a name list, because the three rules below have to partition the same collection
		// - the third is written as "not the other two" so a new category cannot fall out of the check
		private const string CommlinkCategories =
			"category = 'Commlink' or category = 'Commlink Upgrade'";

		private const string OperatingSystemCategories =
			"category = 'Commlink Operating System' or category = 'Commlink Operating System Upgrade'";

		// - the required-field contract: for each entity type, the elements its Create reads without a guard
		//
		// - the empty catch *is* the optionality marker, the only one the code has
		// - Gear.Create (clsEquipment.cs:9552-9665) reads six elements straight off the node and wraps roughly twenty others
		// - an absent <avail> is therefore a NullReferenceException, while an absent <capacity> is silently fine
		//
		// - three shapes of unprotected read, all reduced to one rule shape here (an XPath naming the nodes, plus the fields they must carry):
		// - the entity's own catalogue node, which is most of the table
		// - a nested reference node the same Create walks, e.g. the <gears>/<usegear> children CreateChildren (clsEquipment.cs:9935) recurses through
		// - a catalogue node reached only via a reference, which is the weapons.xml <mount> rule at the end
		//
		// - each rule cites the line it was read off, because nothing else keeps the claim and the code together
		//
		// - the field lists are deliberately not derived by parsing the C#
		// - telling `if (node["x"] != null)` from `if (node["x"].InnerText != "")` takes judgement, and the second of those is an unguarded read that looks like a guard
		// - a mechanism getting that wrong would be worse than a list somebody can review
		// - the *surface* is guarded mechanically all the same: EntityCreateMethodSites below counts the Create declarations, so a new entity type cannot appear without this table noticing
		//
		// - the field lists themselves drift in one direction only, which was measured rather than assumed
		// - naming a field the code does not read turns red against the data, so an invented requirement cannot survive
		// - dropping one the code does read stays green, because a requirement nobody checks can never be violated
		// - no assertion over the XML can see that, which is why EntityCreateFingerprints below watches the source instead: when a Create stops reading a field, its rule is flagged for re-deriving
		// - what remains is the table being edited by hand while the method stays put - only moving the contract onto the entity removes that, which is a backlog item of its own, gated on the golden master
		//
		// - top-level files only, in step with the schema and uniqueness checks
		private static readonly IReadOnlyList<RequiredFieldRule> DeclaredRequiredFieldRules = new[]
		{
			// clsUnique.cs:1036-1051
			Rule("Quality", "qualities.xml", "/chummer/qualities/quality",
				"name", "bp", "category", "source", "page"),
			// clsUnique.cs:3565-3576
			Rule("Spell", "spells.xml", "/chummer/spells/spell",
				"name", "descriptor", "category", "type", "range", "damage", "duration", "dv",
				"source", "page"),
			// clsUnique.cs:4732-4734, reached with a metamagic.xml node when MAG is enabled (frmCreate.cs:7379)
			Rule("Metamagic", "metamagic.xml", "/chummer/metamagics/metamagic",
				"name", "source", "page"),
			// - the same Create, reached with an echoes.xml node when it is not (frmCreate.cs:7385)
			Rule("Echo", "echoes.xml", "/chummer/echoes/echo",
				"name", "source", "page"),
			// - clsUnique.cs:5582-5589; <maxrating> must be present but may be empty, since :5589 tests its text for ""
			Rule("TechProgram", "programs.xml", "/chummer/programs/program",
				"name", "category", "source", "page", "capacity", "skill", "maxrating"),
			// clsUnique.cs:6172-6176, same empty-but-present <maxrating> as above
			Rule("TechProgramOption", "programs.xml", "/chummer/options/option",
				"name", "source", "page", "maxrating"),
			// clsUnique.cs:6535-6537
			Rule("MartialArt", "martialarts.xml", "/chummer/martialarts/martialart",
				"name", "source", "page"),
			// - clsUnique.cs:6781; nested under its art rather than in a collection of its own (frmCreate.cs:9222 selects it through the art)
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
			// - the same Create, reached with a bioware.xml node (it picks its own XPath at clsEquipment.cs:2413)
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
			// - clsEquipment.cs:9552-9665, on everything gear.xml holds except the two commlink classes below
			Rule("Gear", "gear.xml",
				"/chummer/gears/gear[not(" + CommlinkCategories + " or " + OperatingSystemCategories + ")]",
				"name", "category", "avail", "rating", "source", "page"),
			// - clsEquipment.cs:11952-11996; the same six fields as Gear, plus <response>/<signal>
			// - those two are read inside a try by the base class and unguarded by this override
			Rule("Commlink", "gear.xml", "/chummer/gears/gear[" + CommlinkCategories + "]",
				"name", "category", "avail", "rating", "source", "page", "response", "signal"),
			// - clsEquipment.cs:12819-12856; likewise, with <firewall>/<system> as the pair it hardens
			Rule("OperatingSystem", "gear.xml", "/chummer/gears/gear[" + OperatingSystemCategories + "]",
				"name", "category", "avail", "rating", "source", "page", "firewall", "system"),
			// clsEquipment.cs:13336-13437
			Rule("VehicleMod", "vehicles.xml", "/chummer/mods/mod",
				"name", "category", "slots", "avail", "source", "page"),
			// clsEquipment.cs:14406-14425
			Rule("Vehicle", "vehicles.xml", "/chummer/vehicles/vehicle",
				"name", "category", "handling", "accel", "speed", "pilot", "body", "armor",
				"sensor", "avail", "cost", "source", "page"),

			// - the nested reference nodes a Create walks, held to the fields it reads off them
			//
			// - clsEquipment.cs:9935 resolves a child by name AND category
			// - CreateChildren then recurses into its own result at :9968
			// - hence the descendant axis: anchoring to one depth would leave the deeper <usegear> nodes unchecked
			Rule("Gear child <usegear> reference", "gear.xml",
				"/chummer/gears/gear//gears/usegear",
				"name", "category"),
			// - clsEquipment.cs:9827-9828, a second child shape read directly rather than resolved against the catalogue
			Rule("Gear child <gear> reference", "gear.xml",
				"/chummer/gears/gear/gears/gear",
				"name", "category"),
			// clsEquipment.cs:14540
			Rule("Vehicle built-in weapon reference", "vehicles.xml",
				"/chummer/vehicles/vehicle/weapons/weapon",
				"name"),
			// - clsEquipment.cs:14595 and :14617; both guard on the wrapper being present but not on the <name> inside
			// - no vehicle uses either today, so both rules match nothing
			// - declared anyway, because the first entry to use the live code path would otherwise arrive unchecked
			// - the vacancy is pinned in DataPathsTests, so a rule going quiet the other way round cannot hide here
			Rule("Vehicle built-in weapon accessory reference", "vehicles.xml",
				"/chummer/vehicles/vehicle/weapons/weapon/accessories/accessory",
				"name"),
			Rule("Vehicle built-in weapon mod reference", "vehicles.xml",
				"/chummer/vehicles/vehicle/weapons/weapon/mods/mod",
				"name"),

			// - clsEquipment.cs:4346 reads <mount> off the catalogue accessory only while a weapon builds it in
			// - <mount> is therefore required of a referenced accessory and of no other - 12 of the 83 today
			// - the predicate compares <name> against a node-set, which XPath makes true when any one of them matches
			Rule("WeaponAccessory built into a weapon", "weapons.xml",
				"/chummer/accessories/accessory[name = /chummer/weapons/weapon/accessories/accessory]",
				"mount"),
		};

		private static RequiredFieldRule Rule(string entity, string fileName, string itemXPath,
			params string[] requiredFields)
		{
			return new RequiredFieldRule(entity, fileName, itemXPath, requiredFields);
		}

		// One entity type's required-field rule, as read off the Create method that
		// builds it from a catalogue node.
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

			// - the theory's case identity, so a failure names the class whose Create is the reason
			// - unique across the table on purpose: entities sharing a file (Gear and Commlink, Cyberware and Bioware) would otherwise collide on a case id and xUnit would drop one
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

			// - how many nodes the rule's XPath matched, kept so a rule that stops matching anything is visible without reparsing
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

		// - every declared rule, whether or not it matched anything, for the guards that watch the table itself
		public static IReadOnlyList<RequiredFieldRule> RequiredFieldRules => DeclaredRequiredFieldRules;

		// - matches the declaration of an entity Create, never a call site: only a declaration names the parameter type
		// - tolerates a line break after the paren, the one formatting variant that would otherwise read as "no such method"
		// - captures the parameter name, which is what the fingerprint below looks for inside the body
		private static readonly Regex EntityCreateSignature =
			new Regex(@"public\s+void\s+Create\(\s*XmlNode\s+(\w+)", RegexOptions.Compiled);

		private static readonly Regex ClassDeclaration =
			new Regex(@"^\s*public\s+(?:sealed\s+)?class\s+(\w+)", RegexOptions.Compiled);

		// - the production side of the rule table: file:line of every entity Create in the application sources
		// - the table above is hand-written, and every other guard compares it against the data or against itself - none of them can see a Create method the table has never heard of
		// - that gap drops a whole entity type out of the suite in silence, which is the failure this closes
		// - the whole source tree rather than the two files holding them today, so a new entity class in a new file is caught too
		// - obj/ is excluded because it is generated and absent from a fresh clone, which would make the count differ between here and CI
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

		// - obj/ is generated and absent from a fresh clone, which would otherwise make results differ between here and CI
		private static IEnumerable<string> ApplicationSourceFiles()
		{
			string generated = Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar;

			return Directory
				.EnumerateFiles(Path.Combine(RepoRoot, "Chummer"), "*.cs", SearchOption.AllDirectories)
				.Where(path => path.IndexOf(generated, StringComparison.Ordinal) < 0)
				.OrderBy(path => path, StringComparer.Ordinal);
		}

		// - the other half of the rule table's defence, and the one that closes its blind side
		//
		// - a rule naming a field the code does not read turns red against the data, so an invented requirement cannot survive
		// - a rule that *drops* a field the code does read stays green forever, because a requirement nobody checks can never be violated
		// - so the table drifts silently in the permissive direction, and no assertion over the XML can see it
		//
		// - this watches the source instead: per entity class, a hash over exactly the lines a rule was derived from - every read of the Create parameter, plus the try/catch structure around them, whitespace-normalised
		// - deriving the field list mechanically would need judgement this cannot have; noticing that the lines behind it moved needs none
		// - measured on the real methods: deleting a required read changes the hash, wrapping one in try/catch changes it, an unrelated comment or a reindentation does not
		// - the try/catch tokens are in there because moving a read into a try relaxes the contract without touching the read itself
		//
		// - false alarms are near-free here for as long as the production code is frozen, and an edit to a Create method is exactly when its rule wants re-deriving
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

		// - one file's worth of fingerprints, public so the detection tests can drive it with hand-built source
		// - the real methods only ever show today's hashes, never that a deleted read would change one
		public static IReadOnlyDictionary<string, string> FingerprintsIn(string[] lines)
		{
			Dictionary<string, string> fingerprints = new Dictionary<string, string>(StringComparer.Ordinal);

			// - matched on the joined text so a signature broken after the paren is found, as in the sites scan
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
		// - the method's extent is found by brace depth rather than by the next signature, so a nested type or lambda cannot end it early
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

				// - 16 hex characters: plenty against accidental collision, short enough that the pinned table stays readable
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

		// - the entity name is the theory's case id and the key of the lookup below, so it has to be unique - and until this check existed, nothing said so on purpose
		// - measured on a table with the name deliberately doubled: xUnit drops the colliding case, so one rule stops being checked while the count guard still reports 29
		// - the lookup below then throws ArgumentException, which on net48 carries no key in its message, out of a Lazy, into 29 unrelated-looking failures
		// - a reader gets "an item with the same key has already been added" and no hint that the cause is a duplicated name two hundred lines up
		// - same call as the duplicate collection wrapper in IndexRuleCollections: a named failure beats a technical one that points nowhere
		//
		// - checked against the declarations rather than the evaluated contracts, because the declaration is where the mistake is made
		// - runs before the corpus is touched, so the failure arrives while the theory is still being discovered
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

		// - a rule is matched to its file by name, so a renamed or mistyped file produces no contract, no theory case and no failure
		// - one entity type's whole contract then stops being checked while the run stays green
		// - split out and public for the same reason TryResolveDeclarationBlock is: the real table is correct, so a run never reaches the throw
		// - a test has to be able to drive it with a table of its own
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

		// - runs one rule over one document
		// - public so the detection tests can drive it with hand-built XML
		// - the real files only ever show that today's data is complete, never that an absent element would in fact be reported
		public static RequiredFieldContract EvaluateRequiredFields(RequiredFieldRule rule,
			string filePath, XmlDocument document)
		{
			List<MissingField> missing = new List<MissingField>();
			XmlNodeList items = document.SelectNodes(rule.ItemXPath);

			foreach (XmlNode item in items)
				missing.AddRange(MissingRequiredFieldsIn(item, rule.RequiredFields));

			return new RequiredFieldContract(rule, filePath, items.Count, missing);
		}

		// - presence, not content: an element that is there but empty satisfies this, because that is what the code tolerates
		// - clsUnique.cs:5589 tests <maxrating>'s text for "" before converting, so empty is a value the data uses on purpose
		// - 40 programs, 7 program options and 33 spells with no <descriptor> rely on that
		//
		// - the lookup is XmlNode's own indexer, the same call Create makes: direct children only, exact ordinal name match
		// - a <name> nested inside a wrapper does not answer a Create reading node["name"]
		// - neither does a <Name>
		public static IEnumerable<MissingField> MissingRequiredFieldsIn(XmlNode item,
			IReadOnlyList<string> requiredFields)
		{
			return requiredFields
				.Where(field => item[field] == null)
				.Select(field => new MissingField(ItemLabel(item), field));
		}

		// - the entry's own <name> where it has one, otherwise the nearest ancestor's
		// - a nested reference missing its <name> is then reported under the catalogue entry holding it, which is what somebody would go looking for
		// - a top-level entry missing its own falls through to "(unnamed)"
		private static string ItemLabel(XmlNode item)
		{
			return item["name"]?.InnerText ?? NearestItemName(item);
		}

		// - a dictionary rather than a scan per call: the allowlist guard asks for every collection in turn, which over a linear lookup is quadratic; also gives file+collection identity a single place to be checked, which a scan cannot - see the throw below
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
					// - nothing forbids a file from carrying two same-named collection wrappers; if one ever did, the theory could not tell the two apart, since its cases are identified by file and element name and xUnit drops the second case as a colliding id (the same quirk noted on SheetXslFiles above)
					// - the theory would then look healthy while quietly checking only half the data - failing here with the file named beats that silence
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
					// - matches what the XmlReader-based tests here already enforce by default: no external entity resolution, no DTD processing; the data is repo-controlled so nothing rides on it, but two loaders in one test project should not disagree about it
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
							// - indexer assignment would have kept the last block and dropped the first without a word; the symptom is at least loud - every value declared only in the first block starts being reported as undeclared - but it points at the data rather than at the parse, which is a long way to walk back
							// - same call as the duplicate collection wrapper in IndexRuleCollections
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

		// - evaluated here, inside the single parse, for the reason given on CachedRuleFiles: these rules would otherwise reopen a third of the corpus a second time
		private static IReadOnlyList<RequiredFieldContract> RequiredFieldContractsFor(string fileName,
			string xmlPath, XmlDocument document)
		{
			return DeclaredRequiredFieldRules
				.Where(rule => string.Equals(rule.FileName, fileName, StringComparison.Ordinal))
				.Select(rule => EvaluateRequiredFields(rule, xmlPath, document))
				.ToArray();
		}

		// - every <source> in a document, wherever it sits
		//
		// - the unanchored axis is deliberate, the opposite call from the anchored one the uniqueness check makes above - which is exactly why it needs stating rather than assuming
		// - <name> has a definition/reference duality (a nested <cyberware> inside a cybereye is a reference to an option, not a catalogue entry), and an unanchored query would conflate the two; <source> has no such duality, since every occurrence names the book an entry came out of, at whatever depth it sits
		// - measured rather than asserted: over the top-level corpus //source finds 6293 elements, falling into exactly two shapes with nothing left over - 6166 at /chummer/<collection>/<item>/source, and 127 at /chummer/metatypes/metatype/metavariants/metavariant/source in metatypes.xml and critters.xml
		// - anchoring to collection items the way the name check does would drop those 127 metavariant references silently - the failure mode this project has already been bitten by in the other direction
		// - public so the detection tests can drive it with hand-built XML: reading it off the real files only ever shows that today's data is clean, never that a dangling code would in fact be caught, nor that the axis above is still the deep one
		public static IReadOnlyList<BookReference> BookReferencesIn(XmlDocument document)
		{
			return document.SelectNodes("//source").Cast<XmlNode>()
				.Select(node => new BookReference(node.InnerText, NearestItemName(node)))
				.ToArray();
		}

		// - the comparison itself, in one place so the theory over the real files and the hand-built cases that prove it works cannot drift into two implementations that agree only by luck
		// - ordinal and verbatim, for the same reason BuildKey is: Options.BookXPath() (clsOptions.cs:2086-2090) emits source = "SR4" predicates, and XPath string equality is exact - no case folding, no trimming
		// - a code differing in case or padding genuinely does not match there, so treating it as a match here would make the test disagree with the behaviour it describes
		// - the ordinal set is rebuilt here rather than trusting whatever comparer the caller's collection happens to carry, since otherwise the rule this method documents would be decided somewhere else, and the case-sensitivity test next door could be defeated by passing a differently-compared set
		public static IEnumerable<BookReference> ReferencesToUndeclaredBooks(
			IEnumerable<BookReference> references, IEnumerable<string> declaredCodes)
		{
			HashSet<string> declared = new HashSet<string>(declaredCodes, StringComparer.Ordinal);

			return references.Where(reference => !declared.Contains(reference.Code));
		}

		// - walks up to the closest ancestor that has a <name>, which for a metavariant <source> is the metavariant itself rather than its metatype - the more specific of the two, and the one a reader would go looking for
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

		// - public for the same reason BookReferencesIn is, and this one carries the opposite axis decision of the two, which makes pinning it by hand the only way to keep the pair from quietly converging
		public static IEnumerable<CategoryUsage> CategoryUsagesIn(XmlNode collection)
		{
			// - direct children only, matching how the pickers browse: a form lists /chummer/weapons/weapon[category = "..."], so only that top level of items is reachable-by-category in the first place
			// - deliberately not the deep axis BookReferencesIn uses: a <category> further down belongs to a nested reference, which no dropdown ever selects
			return collection.ChildNodes.Cast<XmlNode>()
				.Where(item => item.NodeType == XmlNodeType.Element && item["category"] != null)
				.Select(item => new CategoryUsage(collection.Name, item["category"].InnerText,
					item["name"]?.InnerText ?? "(unnamed)"));
		}

		// - the lookup key of every catalogue entry directly under a collection wrapper, in document order; public so the uniqueness tests can drive it with hand-built XML
		// - reading it off the real files only ever shows that today's data is clean, never that a duplicate would actually be caught, nor that the deliberate comparison rules below still hold
		public static IReadOnlyList<string> ItemKeysIn(XmlNode collection, string[] keyFields)
		{
			return collection.ChildNodes.Cast<XmlNode>()
				.Where(item => item.NodeType == XmlNodeType.Element && item["name"] != null)
				.Select(item => BuildKey(item, keyFields))
				.ToArray();
		}

		// - the duplicate-detection itself, in one place so the theory over the real files and the hand-built cases that prove it works cannot drift apart into two implementations agreeing only by luck
		// - ordinal here for the same reason as in BuildKey below
		public static IEnumerable<KeyValuePair<string, int>> DuplicateItemKeys(
			IReadOnlyList<string> itemKeys)
		{
			return itemKeys
				.GroupBy(key => key, StringComparer.Ordinal)
				.Where(group => group.Count() > 1)
				.Select(group => new KeyValuePair<string, int>(group.Key, group.Count()));
		}

		// - ordinal and verbatim - no trimming, no case folding - on purpose: mirrors what the application does, since a lookup like SelectSingleNode("/chummer/gears/gear[name = \"...\"]") compares the raw string codepoint for codepoint
		// - so two entries differing only in case or surrounding whitespace genuinely are two separately reachable entries, not a collision - normalising here would look like a tidy-up and would in fact make the tests disagree with the behaviour they exist to describe
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
