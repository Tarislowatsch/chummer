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

		// - every reference rule that adds a weapon by name
		// - kit underbarrels have no rule: no kit carries one (frmCreate.cs:21097)
		public static readonly IReadOnlyList<CrossFileReferences.ReferenceRule> Rules = CrossFileReferences.Rules
			.Where(rule => WeaponTargets(rule).Any())
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

		// - base carriers reach every weapon, a pack's carriers only the base and that pack
		// - a carrier inside a weapon counts once that weapon is reachable itself
		public static IEnumerable<Weapon> Unreachable(IReadOnlyList<Weapon> weapons, IReadOnlyList<Carry> carries)
		{
			HashSet<Weapon> reachable = new HashSet<Weapon>(weapons.Where(weapon => !weapon.CarrierOnly));
			ILookup<string, Carry> carriesByName = carries.ToLookup(carry => carry.Name, StringComparer.Ordinal);

			bool grew = true;
			while (grew)
			{
				grew = false;
				foreach (Weapon weapon in weapons.Where(weapon => !reachable.Contains(weapon)).ToArray())
				{
					if (carriesByName[weapon.Name].Any(carry => Adds(carry, weapon) && OwnerReachable(carry, reachable)))
					{
						reachable.Add(weapon);
						grew = true;
					}
				}
			}

			return weapons.Where(weapon => !reachable.Contains(weapon));
		}

		public static IReadOnlyList<Weapon> WeaponsInData()
		{
			List<Weapon> weapons = WeaponsIn(null, Xml.Load(Path.Combine(DataPaths.ChummerDataDir, WeaponsFile))).ToList();
			foreach (PackFiles.PackFile file in PackFiles.Paired()
				.Where(file => !file.IsOverride && file.BaseFileName == WeaponsFile))
			{
				weapons.AddRange(WeaponsIn(file.PackName, Xml.Load(file.FilePath)));
			}

			return weapons;
		}

		public static IReadOnlyList<Carry> CarriesInData()
		{
			List<Carry> carries = RuleCorpus.Files
				.SelectMany(file => CarriesOf(null, file.CrossFileReferences))
				.ToList();
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
				&& (carry.Scope == null || weapon.Scope == null
					|| string.Equals(carry.Scope, weapon.Scope, StringComparison.Ordinal));
		}

		private static bool OwnerReachable(Carry carry, HashSet<Weapon> reachable)
		{
			return carry.OwnerWeapon == null
				|| reachable.Any(weapon => string.Equals(weapon.Scope, carry.Scope, StringComparison.Ordinal)
					&& string.Equals(weapon.Name, carry.OwnerWeapon, StringComparison.Ordinal));
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
