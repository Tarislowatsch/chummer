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

		private static readonly CrossFileReferences.ReferenceRule[] LocalRules = { KitUnderbarrel };

		// - every reference rule that adds a weapon by name
		public static readonly IReadOnlyList<CrossFileReferences.ReferenceRule> Rules = CrossFileReferences.Rules
			.Where(rule => WeaponTargets(rule).Any())
			.Concat(LocalRules)
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
			public Weapon(string scope, string name, bool carrierOnly, IEnumerable<string> admittedBy)
			{
				Scope = scope;
				Name = name;
				CarrierOnly = carrierOnly;
				AdmittedBy = new HashSet<string>(admittedBy, StringComparer.Ordinal);
			}

			public string Scope { get; }

			public string Name { get; }

			public bool CarrierOnly { get; }

			// - the rules whose target filter selects this entry, e.g. category = 'Gear'
			public IReadOnlyCollection<string> AdmittedBy { get; }

			public string Label => Scope == null ? Name : Scope + ": " + Name;

			internal string Key => KeyOf(Scope, Name);
		}

		// One reference that adds a weapon by name.
		public sealed class Carry
		{
			public Carry(string scope, string ruleId, string name, string ownerWeapon)
			{
				Scope = scope;
				RuleId = ruleId;
				Name = name;
				OwnerWeapon = ownerWeapon;
			}

			public string Scope { get; }

			public string RuleId { get; }

			public string Name { get; }

			// - the weapon holding the reference, null when another kind of item does
			public string OwnerWeapon { get; }
		}

		// - a base weapon needs a base carrier: without the pack nobody gets it
		// - a pack weapon takes a carrier from the base or its own pack
		// - a carrier inside a weapon counts once that weapon is reachable itself
		public static IEnumerable<Weapon> Unreachable(IReadOnlyList<Weapon> weapons, IReadOnlyList<Carry> carries)
		{
			HashSet<string> reachable = new HashSet<string>(
				weapons.Where(weapon => !weapon.CarrierOnly).Select(weapon => weapon.Key), StringComparer.Ordinal);
			ILookup<string, Carry> carriesByName = carries.ToLookup(carry => carry.Name, StringComparer.Ordinal);

			bool grew = true;
			while (grew)
			{
				grew = false;
				foreach (Weapon weapon in weapons.Where(weapon => !reachable.Contains(weapon.Key)).ToArray())
				{
					if (carriesByName[weapon.Name].Any(carry => Adds(carry, weapon)
						&& (carry.OwnerWeapon == null || reachable.Contains(KeyOf(carry.Scope, carry.OwnerWeapon)))))
					{
						reachable.Add(weapon.Key);
						grew = true;
					}
				}
			}

			return weapons.Where(weapon => !reachable.Contains(weapon.Key));
		}

		// - an override entry is the pack's version of a base weapon, scoped to that pack
		public static IReadOnlyList<Weapon> WeaponsInData()
		{
			List<Weapon> weapons = WeaponsIn(null, Xml.Load(Path.Combine(DataPaths.ChummerDataDir, WeaponsFile))).ToList();
			foreach (PackFiles.PackFile file in PackFiles.Paired().Where(file => file.BaseFileName == WeaponsFile))
				weapons.AddRange(WeaponsIn(file.PackName, Xml.Load(file.FilePath)));

			return weapons;
		}

		public static IReadOnlyList<Carry> CarriesInData()
		{
			List<Carry> carries = RuleCorpus.Files
				.SelectMany(file => CarriesOf(null, file.CrossFileReferences))
				.ToList();
			foreach (CrossFileReferences.ReferenceRule rule in LocalRules)
			{
				carries.AddRange(CarriesOf(null, CrossFileReferences.ReferencesIn(rule.SourceFile,
					Xml.Load(Path.Combine(DataPaths.ChummerDataDir, rule.SourceFile)), new[] { rule })));
			}

			foreach (PackFiles.PackFile file in PackFiles.Paired())
			{
				carries.AddRange(CarriesOf(file.PackName,
					CrossFileReferences.ReferencesIn(file.BaseFileName, Xml.Load(file.FilePath), Rules)));
			}

			return carries;
		}

		public static IEnumerable<Weapon> WeaponsIn(string scope, XmlDocument document)
		{
			Dictionary<XmlNode, List<string>> admitted = new Dictionary<XmlNode, List<string>>();
			foreach (CrossFileReferences.ReferenceRule rule in Rules)
			{
				foreach (XmlNode node in WeaponTargets(rule)
					.SelectMany(target => document.SelectNodes(target.ItemXPath).Cast<XmlNode>()))
				{
					List<string> ids;
					if (!admitted.TryGetValue(node, out ids))
						admitted.Add(node, ids = new List<string>());
					ids.Add(rule.Id);
				}
			}

			return document.SelectNodes("/chummer/weapons/weapon[name and category]").Cast<XmlNode>()
				.Select(node => new Weapon(scope, node["name"].InnerText,
					CategoryDeclarations.IsCarrierOnly(WeaponsFile, "weapons", node["category"].InnerText),
					admitted.ContainsKey(node) ? admitted[node] : Enumerable.Empty<string>()))
				.ToArray();
		}

		private static IEnumerable<Carry> CarriesOf(string scope, IEnumerable<CrossFileReferences.Reference> references)
		{
			foreach (CrossFileReferences.Reference reference in references)
			{
				int nameField;
				if (!NameFields.TryGetValue(reference.RuleId, out nameField) || reference.Values[nameField] == null)
					continue;

				yield return new Carry(scope, reference.RuleId, reference.Values[nameField],
					WeaponOwnedRules.Contains(reference.RuleId) ? reference.ItemName : null);
			}
		}

		private static bool Adds(Carry carry, Weapon weapon)
		{
			return weapon.AdmittedBy.Contains(carry.RuleId)
				&& (carry.Scope == null || string.Equals(carry.Scope, weapon.Scope, StringComparison.Ordinal));
		}

		private static string KeyOf(string scope, string name)
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
