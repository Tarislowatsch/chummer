using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
				IReadOnlyList<CategoryUsage> categoryUsages)
			{
				FilePath = filePath;
				Collections = collections;
				BookReferences = bookReferences;
				CategoryDeclarations = categoryDeclarations;
				CategoryUsages = categoryUsages;
			}

			public string FilePath { get; }

			public string FileName => Path.GetFileName(FilePath);

			public IReadOnlyList<RuleCollection> Collections { get; }

			public IReadOnlyList<BookReference> BookReferences { get; }

			// - declaration block name -> the values it declares; only blocks named in DeclarationBlockNames are gathered
			// - a file with none is simply absent from the category check
			public IReadOnlyDictionary<string, IReadOnlyCollection<string>> CategoryDeclarations { get; }

			public IReadOnlyList<CategoryUsage> CategoryUsages { get; }
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
					declarations, categoryUsages));
			}

			return files;
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
