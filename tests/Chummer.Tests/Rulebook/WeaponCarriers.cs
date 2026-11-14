using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Chummer.Tests.Kernel;

namespace Chummer.Tests.Rulebook
{
	// - which carrier-only weapons some carrier really adds, base and packs together
	public static class WeaponCarriers
	{
		private const string WeaponsFile = "weapons.xml";

		// - frmCreate.cs:21099 reads it, but no packs.xml carries one
		// - here, not in CrossFileReferences: a rule there needs data to check
		private static readonly CrossFileReferences.ReferenceRule KitUnderbarrel = new CrossFileReferences.ReferenceRule(
			"kit weapon underbarrel", "frmCreate.cs:21099", "objXmlWeapon[\"underbarrel\"]",
			"packs.xml", "/chummer/packs/pack/weapons/weapon/underbarrel",
			new[] { new CrossFileReferences.KeyField(".", "name", false) },
			new[] { new CrossFileReferences.Target(WeaponsFile, "/chummer/weapons/weapon") });

		// - every reference rule that adds a weapon by name
		public static readonly IReadOnlyList<CrossFileReferences.ReferenceRule> Rules = CrossFileReferences.Rules
			.Where(rule => WeaponTargets(rule).Any())
			.Concat(new[] { KitUnderbarrel })
			.ToArray();

		private static readonly IReadOnlyDictionary<string, int> NameFields =
			Rules.ToDictionary(rule => rule.Id, NameFieldOf, StringComparer.Ordinal);

		private static readonly HashSet<string> WeaponOwnedRules = new HashSet<string>(
			Rules.Where(rule => rule.SourceXPath.StartsWith("/chummer/weapons/weapon/", StringComparison.Ordinal))
				.Select(rule => rule.Id),
			StringComparer.Ordinal);

		// One weapon entry: Scope is null for the base file, else the pack's name.
		public sealed class Weapon
		{
			public Weapon(string scope, string name, bool carrierOnly, IEnumerable<string> admittedBy, string id = null)
			{
				Scope = scope;
				Name = name;
				CarrierOnly = carrierOnly;
				AdmittedBy = new HashSet<string>(admittedBy, StringComparer.Ordinal);
				Id = id ?? name;
			}

			public string Scope { get; }

			public string Name { get; }

			public bool CarrierOnly { get; }

			// - the rules whose lookup finds this entry, e.g. under category = 'Gear'
			public IReadOnlyCollection<string> AdmittedBy { get; }

			// - per entry, not per name: two entries may share a name in different categories
			public string Id { get; }

			public string Label => Scope == null ? Name : Scope + ": " + Name;

			internal string Key => KeyOf(Scope, Id);
		}

		// One reference that adds a weapon by name.
		public sealed class Carry
		{
			public Carry(string scope, string ruleId, string name, string ownerWeapon, string source = null)
			{
				Scope = scope;
				RuleId = ruleId;
				Name = name;
				OwnerWeapon = ownerWeapon;
				Source = source;
			}

			public string Scope { get; }

			public string RuleId { get; }

			public string Name { get; }

			// - the Id of the weapon holding the reference, null when another kind of item does
			public string OwnerWeapon { get; }

			// - <file>/<collection>/<name> of the top-level item holding the reference
			public string Source { get; }
		}

		// - a base weapon needs a base carrier: without the pack nobody gets it
		// - a pack weapon takes a carrier from the base or its own pack
		// - a carrier inside a weapon counts once that weapon is reachable itself
		public static IEnumerable<Weapon> Unreachable(IReadOnlyList<Weapon> weapons, IReadOnlyList<Carry> carries,
			IEnumerable<string> replaced = null)
		{
			HashSet<string> rewritten = new HashSet<string>(replaced ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
			HashSet<string> reachable = new HashSet<string>(
				weapons.Where(weapon => !weapon.CarrierOnly).Select(weapon => weapon.Key), StringComparer.Ordinal);
			ILookup<string, Carry> carriesByName = carries.ToLookup(carry => carry.Name, StringComparer.Ordinal);

			bool grew = true;
			while (grew)
			{
				grew = false;
				foreach (Weapon weapon in weapons.Where(weapon => !reachable.Contains(weapon.Key)).ToArray())
				{
					if (carriesByName[weapon.Name].Any(carry => Adds(carry, weapon, rewritten)
						&& (carry.OwnerWeapon == null || reachable.Contains(KeyOf(carry.Scope, carry.OwnerWeapon)))))
					{
						reachable.Add(weapon.Key);
						grew = true;
					}
				}
			}

			return weapons.Where(weapon => !reachable.Contains(weapon.Key));
		}

		public static IEnumerable<Weapon> UnreachableInData()
		{
			IReadOnlyList<KeyValuePair<PackFiles.PackFile, XmlDocument>> packs = LoadedPacks();
			return Unreachable(WeaponsInData(packs), CarriesInData(packs), ReplacedInData(packs));
		}

		// - each pack file without the entries Load discards (CustomContent.ItemsLoadDiscards)
		public static XmlDocument LoadedPart(PackFiles.PackFile file, XmlDocument pack, XmlDocument baseDocument)
		{
			foreach (CustomContent.PackItem item in CustomContent.ItemsLoadDiscards(file, pack, baseDocument).ToArray())
			{
				foreach (XmlNode node in pack.SelectNodes(item.XPath).Cast<XmlNode>().ToArray())
					node.ParentNode.RemoveChild(node);
			}

			return pack;
		}

		// - against the base only: a twin across packs fails NoTwoCustomEntriesShareAName
		private static IReadOnlyList<KeyValuePair<PackFiles.PackFile, XmlDocument>> LoadedPacks()
		{
			Dictionary<string, XmlDocument> bases = new Dictionary<string, XmlDocument>(StringComparer.Ordinal);
			List<KeyValuePair<PackFiles.PackFile, XmlDocument>> packs = new List<KeyValuePair<PackFiles.PackFile, XmlDocument>>();
			foreach (PackFiles.PackFile file in PackFiles.Paired())
			{
				XmlDocument baseDocument;
				if (!bases.TryGetValue(file.BaseFileName, out baseDocument))
					bases.Add(file.BaseFileName, baseDocument = Xml.Load(file.BaseFilePath));
				packs.Add(new KeyValuePair<PackFiles.PackFile, XmlDocument>(file,
					LoadedPart(file, Xml.Load(file.FilePath), baseDocument)));
			}

			return packs;
		}

		// - an override entry is the pack's version of a base weapon, scoped to that pack
		private static IReadOnlyList<Weapon> WeaponsInData(IEnumerable<KeyValuePair<PackFiles.PackFile, XmlDocument>> packs)
		{
			List<Weapon> weapons = WeaponsIn(null, Xml.Load(Path.Combine(DataPaths.ChummerDataDir, WeaponsFile))).ToList();
			foreach (KeyValuePair<PackFiles.PackFile, XmlDocument> pack in packs.Where(pack => pack.Key.BaseFileName == WeaponsFile))
				weapons.AddRange(WeaponsIn(pack.Key.PackName, pack.Value));

			return weapons;
		}

		private static IReadOnlyList<Carry> CarriesInData(IEnumerable<KeyValuePair<PackFiles.PackFile, XmlDocument>> packs)
		{
			List<Carry> carries = Rules.Select(rule => rule.SourceFile)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.SelectMany(fileName => CarriesIn(null, fileName, Xml.Load(Path.Combine(DataPaths.ChummerDataDir, fileName))))
				.ToList();
			foreach (KeyValuePair<PackFiles.PackFile, XmlDocument> pack in packs)
				carries.AddRange(CarriesIn(pack.Key.PackName, pack.Key.BaseFileName, pack.Value));

			return carries;
		}

		// - Load writes the override's InnerXml over the base item (clsXmlManager.cs:170)
		private static IEnumerable<string> ReplacedInData(IEnumerable<KeyValuePair<PackFiles.PackFile, XmlDocument>> packs)
		{
			return packs.Where(pack => pack.Key.IsOverride)
				.SelectMany(pack => CustomContent.NamedItemsIn(pack.Value)
					.Select(item => KeyOf(pack.Key.PackName, pack.Key.BaseFileName + "/" + item.Collection + "/" + item.Name)));
		}

		// - a rule admits only the first entry of a name: the consumer's lookup is SelectSingleNode
		public static IEnumerable<Weapon> WeaponsIn(string scope, XmlDocument document)
		{
			IReadOnlyDictionary<XmlNode, string> ids = WeaponIdsIn(document);
			Dictionary<XmlNode, List<string>> admitted = new Dictionary<XmlNode, List<string>>();
			foreach (CrossFileReferences.ReferenceRule rule in Rules)
			{
				foreach (XmlNode node in WeaponTargets(rule)
					.SelectMany(target => document.SelectNodes(target.ItemXPath).Cast<XmlNode>())
					.Where(node => node["name"] != null)
					.GroupBy(node => node["name"].InnerText, StringComparer.Ordinal)
					.Select(group => group.First()))
				{
					List<string> rules;
					if (!admitted.TryGetValue(node, out rules))
						admitted.Add(node, rules = new List<string>());
					rules.Add(rule.Id);
				}
			}

			// - no <category>, as after an override without one: no picker lists it, only a carrier
			return ids.Select(entry => new Weapon(scope, entry.Key["name"].InnerText,
					entry.Key["category"] == null
						|| CategoryDeclarations.IsCarrierOnly(WeaponsFile, "weapons", entry.Key["category"].InnerText),
					admitted.ContainsKey(entry.Key) ? admitted[entry.Key] : Enumerable.Empty<string>(),
					entry.Value))
				.ToArray();
		}

		// - read off the nodes: Reference.ItemName names a vehicle or weapon inside a kit, not the kit
		public static IEnumerable<Carry> CarriesIn(string scope, string fileName, XmlDocument document)
		{
			IReadOnlyDictionary<XmlNode, string> weaponIds = WeaponIdsIn(document);
			foreach (CrossFileReferences.ReferenceRule rule in Rules
				.Where(rule => string.Equals(rule.SourceFile, fileName, StringComparison.OrdinalIgnoreCase)))
			{
				string nameSource = rule.KeyFields[NameFields[rule.Id]].Source;
				foreach (XmlNode node in document.SelectNodes(rule.SourceXPath))
				{
					string name = nameSource == "." ? node.InnerText : node.SelectSingleNode(nameSource)?.InnerText;
					XmlNode item = TopItemOf(node);
					if (name == null || item == null)
						continue;

					string owner = null;
					if (WeaponOwnedRules.Contains(rule.Id))
						weaponIds.TryGetValue(item, out owner);
					yield return new Carry(scope, rule.Id, name, owner,
						fileName + "/" + item.ParentNode.Name + "/" + item["name"]?.InnerText);
				}
			}
		}

		// - an entry's Id: its name and position among the file's weapons
		private static IReadOnlyDictionary<XmlNode, string> WeaponIdsIn(XmlDocument document)
		{
			return document.SelectNodes("/chummer/weapons/weapon[name]").Cast<XmlNode>()
				.Select((node, index) => new KeyValuePair<XmlNode, string>(node, node["name"].InnerText + "#" + index))
				.ToDictionary(entry => entry.Key, entry => entry.Value);
		}

		// - the catalogue entry directly under /chummer/<collection>
		private static XmlNode TopItemOf(XmlNode node)
		{
			for (XmlNode item = node; item.ParentNode != null; item = item.ParentNode)
			{
				if (item.ParentNode.ParentNode == node.OwnerDocument.DocumentElement)
					return item;
			}

			return null;
		}

		// - a base carrier is gone for a pack whose override rewrote its item
		private static bool Adds(Carry carry, Weapon weapon, HashSet<string> rewritten)
		{
			if (!weapon.AdmittedBy.Contains(carry.RuleId))
				return false;
			if (carry.Scope != null)
				return string.Equals(carry.Scope, weapon.Scope, StringComparison.Ordinal);

			return weapon.Scope == null || !rewritten.Contains(KeyOf(weapon.Scope, carry.Source));
		}

		internal static string KeyOf(string scope, string name)
		{
			return scope + "\0" + name;
		}

		private static IEnumerable<CrossFileReferences.Target> WeaponTargets(CrossFileReferences.ReferenceRule rule)
		{
			return rule.Targets.Where(target => target.FileName == WeaponsFile
				&& target.ItemXPath.StartsWith("/chummer/weapons/weapon", StringComparison.Ordinal)
				&& !target.ItemXPath.StartsWith("/chummer/weapons/weapon/", StringComparison.Ordinal));
		}

		// - a rule without a name field would drop its carriers without a word
		private static int NameFieldOf(CrossFileReferences.ReferenceRule rule)
		{
			int index = rule.KeyFields.ToList().FindIndex(field => field.Target == "name");
			if (index < 0)
				throw new InvalidOperationException(rule.Id + " adds a weapon but has no key field for its name.");

			return index;
		}
	}
}
