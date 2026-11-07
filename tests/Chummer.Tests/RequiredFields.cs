using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace Chummer.Tests
{
	// - a field a Create dereferences unguarded is a crash on the click that adds its entry
	public static class RequiredFields
	{
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
		private static readonly IReadOnlyList<RequiredFieldRule> DeclaredRules = new[]
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

		public static IEnumerable<object[]> Entities()
		{
			return CachedContracts.Value
				.Select(contract => new object[] { contract.Rule.Entity });
		}

		public static RequiredFieldContract ContractFor(string entity)
		{
			return CachedContractsByEntity.Value[entity];
		}

		// - every declared rule, matched or not, for the guards that watch the table itself
		public static IReadOnlyList<RequiredFieldRule> Rules => DeclaredRules;

		// - matches only a declaration: a call site never names the parameter type
		// - tolerates a line break after the paren
		// - captures the parameter name the fingerprint looks for inside the body
		internal static readonly Regex EntityCreateSignature =
			new Regex(@"public\s+void\s+Create\(\s*XmlNode\s+(\w+)", RegexOptions.Compiled);

		private static readonly Regex ClassDeclaration =
			new Regex(@"^\s*public\s+(?:sealed\s+)?class\s+(\w+)", RegexOptions.Compiled);

		// - file:line of every entity Create, the only guard that can see one the table never met
		// - a missed Create drops a whole entity type out of the suite in silence
		// - the whole source tree, so a new entity class in a new file is caught too
		public static IReadOnlyList<string> EntityCreateMethodSites()
		{
			List<string> sites = new List<string>();

			foreach (string path in DataPaths.ApplicationSourceFiles())
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

		// - the rule table drifts permissive only: a dropped field stays green, nobody checks it
		// - so this hashes, per class, every read of the Create parameter plus the try/catch around it
		// - a changed hash means the rule wants re-deriving against the changed method
		public static IReadOnlyDictionary<string, string> EntityCreateFingerprints()
		{
			Dictionary<string, string> fingerprints = new Dictionary<string, string>(StringComparer.Ordinal);

			foreach (string path in DataPaths.ApplicationSourceFiles())
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
					SourceLines.Fingerprint(NodeReadsIn(lines, signatureLine, signature.Groups[1].Value)));
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

		internal static string ClassNameAbove(string[] lines, int signatureLine)
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

		public static IReadOnlyList<RequiredFieldContract> Contracts =>
			CachedContracts.Value;

		private static readonly Lazy<IReadOnlyList<RequiredFieldContract>> CachedContracts =
			new Lazy<IReadOnlyList<RequiredFieldContract>>(BuildContracts);

		private static readonly Lazy<IReadOnlyDictionary<string, RequiredFieldContract>>
			CachedContractsByEntity =
				new Lazy<IReadOnlyDictionary<string, RequiredFieldContract>>(
					() => CachedContracts.Value.ToDictionary(
						contract => contract.Rule.Entity, StringComparer.Ordinal));

		private static IReadOnlyList<RequiredFieldContract> BuildContracts()
		{
			EnsureEntityNamesAreUnique(DeclaredRules);

			RequiredFieldContract[] contracts = RuleCorpus.Files
				.SelectMany(file => file.RequiredFieldContracts)
				.ToArray();

			EnsureEveryRuleWasEvaluated(DeclaredRules, contracts);

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
		// - public: no real run reaches the throw, only a test can
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

		// - inside the single parse: these rules would otherwise reopen a third of the corpus
		internal static IReadOnlyList<RequiredFieldContract> ContractsIn(string fileName,
			string xmlPath, XmlDocument document)
		{
			return DeclaredRules
				.Where(rule => string.Equals(rule.FileName, fileName, StringComparison.Ordinal))
				.Select(rule => Evaluate(rule, xmlPath, document))
				.ToArray();
		}

		// - public so the detection tests can drive it with hand-built XML
		// - the real files show today's data is complete, never that an absence would be reported
		public static RequiredFieldContract Evaluate(RequiredFieldRule rule,
			string filePath, XmlDocument document)
		{
			List<MissingField> missing = new List<MissingField>();
			XmlNodeList items = document.SelectNodes(rule.ItemXPath);

			foreach (XmlNode item in items)
				missing.AddRange(MissingFieldsIn(item, rule.RequiredFields));

			return new RequiredFieldContract(rule, filePath, items.Count, missing);
		}

		// - presence, not content: an empty element satisfies this, as the code tolerates it
		// - clsUnique.cs:5589 tests <maxrating>'s text for "", so empty is a value the data uses
		// - XmlNode's indexer, the call Create makes: direct children only, exact ordinal match
		public static IEnumerable<MissingField> MissingFieldsIn(XmlNode item,
			IReadOnlyList<string> requiredFields)
		{
			return requiredFields
				.Where(field => item[field] == null)
				.Select(field => new MissingField(RuleCorpus.ItemLabel(item), field));
		}
	}
}
