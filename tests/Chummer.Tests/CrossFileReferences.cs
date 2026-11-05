using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Chummer.Tests
{
	// - a name the code looks up and misses is a crash on null or a silent no-op
	// - each rule mirrors its consumer's lookup: key, category filter, fallback file
	// - only paths the data uses: a rule over nothing would pass without checking anything
	public static class CrossFileReferences
	{
		public sealed class KeyField
		{
			public KeyField(string source, string target, bool optional)
			{
				Source = source;
				Target = target;
				Optional = optional;
			}

			// - relative to the reference node, "." for its own text
			public string Source { get; }

			public string Target { get; }

			// - the consumer leaves the field out of its lookup when the reference lacks it
			public bool Optional { get; }
		}

		public sealed class Target
		{
			public Target(string fileName, string itemXPath)
			{
				FileName = fileName;
				ItemXPath = itemXPath;
			}

			public string FileName { get; }

			public string ItemXPath { get; }
		}

		public sealed class ReferenceRule
		{
			public ReferenceRule(string id, string consumer, string reads, string sourceFile,
				string sourceXPath, IReadOnlyList<KeyField> keyFields, IReadOnlyList<Target> targets)
			{
				Id = id;
				Consumer = consumer;
				Reads = reads;
				SourceFile = sourceFile;
				SourceXPath = sourceXPath;
				KeyFields = keyFields;
				Targets = targets;
			}

			public string Id { get; }

			// - "file.cs:line" of the loop or lookup the rule is read off
			public string Consumer { get; }

			// - code that must still be in that file, comments excluded
			// - proves the consumer exists, not that its lookup still matches the rule
			// - a consumer change needs the rule re-read by hand, as the line reference says
			public string Reads { get; }

			public string ConsumerFile => Consumer.Substring(0, Consumer.IndexOf(':'));

			public string SourceFile { get; }

			public string SourceXPath { get; }

			public IReadOnlyList<KeyField> KeyFields { get; }

			// - several targets are tried in order, as the consumer falls back
			public IReadOnlyList<Target> Targets { get; }
		}

		public sealed class Reference
		{
			public Reference(string ruleId, string itemName, IReadOnlyList<string> values)
			{
				RuleId = ruleId;
				ItemName = itemName;
				Values = values;
			}

			public string RuleId { get; }

			public string ItemName { get; }

			// - one per key field, null where the reference lacks it
			public IReadOnlyList<string> Values { get; }

			// - the allowlist key and the failure text
			public string Display => string.Join(" / ", Values.Select(value => value ?? "(none)"));
		}

		// The keys one target file offers one rule.
		public sealed class TargetKeys
		{
			public TargetKeys(string ruleId, int targetIndex, IReadOnlyList<IReadOnlyList<string>> keys)
			{
				RuleId = ruleId;
				TargetIndex = targetIndex;
				Keys = keys;
			}

			public string RuleId { get; }

			public int TargetIndex { get; }

			public IReadOnlyList<IReadOnlyList<string>> Keys { get; }
		}

		public sealed class ReferenceCheck
		{
			public ReferenceCheck(ReferenceRule rule, int referenceCount, IReadOnlyList<int> definitionCounts,
				IReadOnlyList<Reference> unresolved)
			{
				Rule = rule;
				ReferenceCount = referenceCount;
				DefinitionCounts = definitionCounts;
				Unresolved = unresolved;
			}

			public ReferenceRule Rule { get; }

			public int ReferenceCount { get; }

			// - per target: a sound fallback file must not hide a mistyped first one
			public IReadOnlyList<int> DefinitionCounts { get; }

			public IReadOnlyList<Reference> Unresolved { get; }
		}

		public static IReadOnlyList<Reference> ReferencesIn(string fileName, XmlDocument document,
			IEnumerable<ReferenceRule> rules)
		{
			List<Reference> references = new List<Reference>();

			foreach (ReferenceRule rule in rules.Where(rule => IsFile(rule.SourceFile, fileName)))
			{
				foreach (XmlNode node in document.SelectNodes(rule.SourceXPath))
				{
					references.Add(new Reference(rule.Id, OwnerLabel(node),
						rule.KeyFields.Select(field => ValueOf(node, field.Source)).ToArray()));
				}
			}

			return references;
		}

		public static IReadOnlyList<TargetKeys> TargetKeysIn(string fileName, XmlDocument document,
			IEnumerable<ReferenceRule> rules)
		{
			List<TargetKeys> found = new List<TargetKeys>();

			foreach (ReferenceRule rule in rules)
			{
				for (int index = 0; index < rule.Targets.Count; index++)
				{
					Target target = rule.Targets[index];
					if (!IsFile(target.FileName, fileName))
						continue;

					found.Add(new TargetKeys(rule.Id, index, document.SelectNodes(target.ItemXPath)
						.Cast<XmlNode>()
						.Select(item => (IReadOnlyList<string>)rule.KeyFields
							.Select(field => ValueOf(item, field.Target)).ToArray())
						.ToArray()));
				}
			}

			return found;
		}

		// - a reference resolves when any target offers a key equal on every field it carries
		public static IReadOnlyList<Reference> Unresolved(ReferenceRule rule,
			IEnumerable<Reference> references, IReadOnlyList<IReadOnlyList<string>> definitions)
		{
			return references
				.Where(reference => !definitions.Any(definition => Matches(rule, reference, definition)))
				.ToArray();
		}

		private static bool Matches(ReferenceRule rule, Reference reference, IReadOnlyList<string> definition)
		{
			for (int index = 0; index < rule.KeyFields.Count; index++)
			{
				string value = reference.Values[index];
				if (value == null)
				{
					if (!rule.KeyFields[index].Optional)
						return false;

					continue;
				}

				if (!string.Equals(value, definition[index], StringComparison.Ordinal))
					return false;
			}

			return true;
		}

		private static bool IsFile(string expected, string fileName)
		{
			return string.Equals(expected, fileName, StringComparison.OrdinalIgnoreCase);
		}

		// - the nearest named element above the reference, as for book references
		// - in <mods><name>, the parent's own <name> is the reference itself
		private static string OwnerLabel(XmlNode reference)
		{
			return DataPaths.ItemLabel(reference.Name == "name" ? reference.ParentNode.ParentNode : reference.ParentNode);
		}

		private static string ValueOf(XmlNode node, string path)
		{
			return path == "." ? node.InnerText : node.SelectSingleNode(path)?.InnerText;
		}

		private static readonly KeyField[] ByText = { new KeyField(".", "name", false) };

		private static readonly KeyField[] ByNameChild = { new KeyField("name", "name", false) };

		// - for targets that are bare text elements: categories, skill groups
		private static readonly KeyField[] ByTextToText = { new KeyField(".", ".", false) };

		private static Target[] In(string fileName, string itemXPath)
		{
			return new[] { new Target(fileName, itemXPath) };
		}

		private static readonly Target[] Weapons = In("weapons.xml", "/chummer/weapons/weapon");
		private static readonly Target[] Accessories = In("weapons.xml", "/chummer/accessories/accessory");
		private static readonly Target[] WeaponMods = In("weapons.xml", "/chummer/mods/mod");
		private static readonly Target[] Gear = In("gear.xml", "/chummer/gears/gear");
		private static readonly Target[] Armor = In("armor.xml", "/chummer/armors/armor");
		private static readonly Target[] ArmorMods = In("armor.xml", "/chummer/mods/mod");
		private static readonly Target[] Cyberware = In("cyberware.xml", "/chummer/cyberwares/cyberware");
		private static readonly Target[] Bioware = In("bioware.xml", "/chummer/biowares/bioware");
		private static readonly Target[] CyberwareGrades = In("cyberware.xml", "/chummer/grades/grade");
		private static readonly Target[] BiowareGrades = In("bioware.xml", "/chummer/grades/grade");
		private static readonly Target[] Vehicles = In("vehicles.xml", "/chummer/vehicles/vehicle");
		private static readonly Target[] VehicleMods = In("vehicles.xml", "/chummer/mods/mod");
		private static readonly Target[] CritterPowers = In("critterpowers.xml", "/chummer/powers/power");
		private static readonly Target[] Critters = In("critters.xml", "/chummer/metatypes/metatype");
		private static readonly Target[] Qualities = In("qualities.xml", "/chummer/qualities/quality");
		private static readonly Target[] Programs = In("programs.xml", "/chummer/programs/program");
		private static readonly Target[] Skills = In("skills.xml", "/chummer/skills/skill");
		private static readonly Target[] SkillGroups = In("skills.xml", "/chummer/skillgroups/name");
		private static readonly Target[] Powers = In("powers.xml", "/chummer/powers/power");
		private static readonly Target[] Spells = In("spells.xml", "/chummer/spells/spell");
		private static readonly Target[] Metamagics = In("metamagic.xml", "/chummer/metamagics/metamagic");
		private static readonly Target[] Echoes = In("echoes.xml", "/chummer/echoes/echo");

		// - the consumers query metatypes.xml first and fall back to critters.xml
		private static readonly Target[] Metatypes =
		{
			new Target("metatypes.xml", "/chummer/metatypes/metatype"),
			new Target("critters.xml", "/chummer/metatypes/metatype"),
		};

		private static readonly Target[] Metavariants =
		{
			new Target("metatypes.xml", "/chummer/metatypes/metatype/metavariants/metavariant"),
			new Target("critters.xml", "/chummer/metatypes/metatype/metavariants/metavariant"),
		};

		private static readonly Target[] MetatypeCategories =
		{
			new Target("metatypes.xml", "/chummer/categories/category"),
			new Target("critters.xml", "/chummer/categories/category"),
		};

		// - the character holds cyberware and bioware in one list and compares by name alone
		private static readonly Target[] AnyImplant = Cyberware.Concat(Bioware).ToArray();

		private static ReferenceRule Rule(string id, string consumer, string reads, string sourceFile,
			string sourceXPath, Target[] targets, KeyField[] keyFields = null)
		{
			return new ReferenceRule(id, consumer, reads, sourceFile, sourceXPath, keyFields ?? ByText, targets);
		}

		public static readonly IReadOnlyList<ReferenceRule> Rules = new[]
		{
			// - each Create filters by weapon category: a same-named weapon elsewhere does not count
			Rule("armor mod addweapon", "clsEquipment.cs:136",
				"objXmlArmorNode.SelectNodes(\"addweapon\")",
				"armor.xml", "/chummer/mods/mod/addweapon",
				In("weapons.xml", "/chummer/weapons/weapon[starts-with(category, 'Cyberware')]")),
			Rule("bioware addweapon", "clsEquipment.cs:2484",
				"objXmlCyberware.SelectNodes(\"addweapon\")",
				"bioware.xml", "/chummer/biowares/bioware/addweapon",
				In("weapons.xml", "/chummer/weapons/weapon[starts-with(category, 'Cyberware')]")),
			Rule("cyberware addweapon", "clsEquipment.cs:2484",
				"objXmlCyberware.SelectNodes(\"addweapon\")",
				"cyberware.xml", "/chummer/cyberwares/cyberware/addweapon",
				In("weapons.xml", "/chummer/weapons/weapon[starts-with(category, 'Cyberware')]")),
			Rule("gear addweapon", "clsEquipment.cs:9764",
				"objXmlGear.SelectNodes(\"addweapon\")",
				"gear.xml", "/chummer/gears/gear/addweapon",
				In("weapons.xml", "/chummer/weapons/weapon[category = 'Gear']")),
			Rule("quality addweapon", "clsUnique.cs:1074",
				"objXmlQuality.SelectNodes(\"addweapon\")",
				"qualities.xml", "/chummer/qualities/quality/addweapon",
				In("weapons.xml", "/chummer/weapons/weapon[starts-with(category, 'Quality')]")),

			Rule("armor mods", "clsEquipment.cs:1032",
				"objXmlArmorNode.SelectNodes(\"mods/name\")",
				"armor.xml", "/chummer/armors/armor/mods/name", ArmorMods),
			Rule("armor usegear", "clsEquipment.cs:1066",
				"objXmlArmorNode.SelectNodes(\"gears/usegear\")",
				"armor.xml", "/chummer/armors/armor/gears/usegear", Gear),
			// - CreateChildren recurses: nested usegear entries are read too
			Rule("gear usegear", "clsEquipment.cs:9933",
				"objXmlGear.SelectNodes(\"gears/usegear\")",
				"gear.xml", "/chummer/gears/gear/gears//usegear", Gear,
				new[] { new KeyField("name", "name", false), new KeyField("category", "category", false) }),

			Rule("vehicle mods", "clsEquipment.cs:14456",
				"objXmlVehicle.SelectNodes(\"mods/name\")",
				"vehicles.xml", "/chummer/vehicles/vehicle/mods/name", VehicleMods),
			Rule("vehicle gears", "clsEquipment.cs:14489",
				"objXmlVehicle.SelectNodes(\"gears/gear\")",
				"vehicles.xml", "/chummer/vehicles/vehicle/gears/gear", Gear),
			Rule("vehicle weapons", "clsEquipment.cs:14534",
				"objXmlVehicle.SelectNodes(\"weapons/weapon\")",
				"vehicles.xml", "/chummer/vehicles/vehicle/weapons/weapon", Weapons, ByNameChild),

			Rule("weapon underbarrel", "clsEquipment.cs:4325",
				"objXmlWeapon.SelectSingleNode(\"underbarrel\")",
				"weapons.xml", "/chummer/weapons/weapon/underbarrel", Weapons),
			Rule("weapon accessories", "clsEquipment.cs:4340",
				"objXmlWeapon.SelectNodes(\"accessories/accessory\")",
				"weapons.xml", "/chummer/weapons/weapon/accessories/accessory", Accessories),
			Rule("weapon mods", "clsEquipment.cs:4360",
				"objXmlWeapon.SelectNodes(\"mods/mod\")",
				"weapons.xml", "/chummer/weapons/weapon/mods/mod", WeaponMods),
			Rule("weapon useskill", "clsEquipment.cs:6917",
				"strSkill = _strUseSkill",
				"weapons.xml", "/chummer/weapons/weapon/useskill", Skills),

			// - a missed grade falls back to "Standard" without a word (clsEquipment.cs:2327)
			Rule("bioware forcegrade", "frmSelectCyberware.cs:262",
				"cboGrade.SelectedValue = objXmlCyberware[\"forcegrade\"]",
				"bioware.xml", "/chummer/biowares/bioware/forcegrade", BiowareGrades),
			Rule("cyberware suite grade", "frmCreate.cs:22828",
				"ConvertToCyberwareGrade(objXmlSuite[\"grade\"]",
				"cyberware.xml", "/chummer/suites/suite/grade", CyberwareGrades),
			// - CreateSuiteCyberware recurses into the children (frmCreate.cs:20272)
			Rule("cyberware suite items", "frmCreate.cs:22833",
				"\"/chummer/\" + strType + \"s/\" + strType",
				"cyberware.xml", "/chummer/suites/suite/cyberwares//cyberware", Cyberware, ByNameChild),

			Rule("critter powers", "frmMetatype.cs:635",
				"objXmlCritter.SelectNodes(\"powers/power\")",
				"critters.xml", "/chummer/metatypes/metatype/powers/power", CritterPowers),
			Rule("critter optional powers", "frmSelectCritterPower.cs:325",
				"SelectNodes(\"optionalpowers/power\")",
				"critters.xml", "/chummer/metatypes/metatype/optionalpowers/power", CritterPowers),
			Rule("critter metavariant powers", "frmMetatype.cs:657",
				"objXmlMetavariant.SelectNodes(\"powers/power\")",
				"critters.xml", "/chummer/metatypes/metatype/metavariants/metavariant/powers/power", CritterPowers),
			Rule("critter positive qualities", "frmMetatype.cs:521",
				"objXmlMetatype.SelectNodes(\"qualities/positive/quality\")",
				"critters.xml", "/chummer/metatypes/metatype/qualities/positive/quality", Qualities),
			Rule("critter negative qualities", "frmMetatype.cs:542",
				"objXmlMetatype.SelectNodes(\"qualities/negative/quality\")",
				"critters.xml", "/chummer/metatypes/metatype/qualities/negative/quality", Qualities),
			Rule("critter complex forms", "frmMetatype.cs:858",
				"objXmlCritter.SelectNodes(\"complexforms/complexform\")",
				"critters.xml", "/chummer/metatypes/metatype/complexforms/complexform", Programs),
			// - an empty entry is allowed by its category attribute alone (frmSelectProgram.cs:105)
			Rule("critter optional complex forms", "frmSelectProgram.cs:100",
				"SelectNodes(\"optionalcomplexforms/complexform\")",
				"critters.xml", "/chummer/metatypes/metatype/optionalcomplexforms/complexform[. != '']", Programs),
			Rule("critter gears", "frmMetatype.cs:893",
				"objXmlCritter.SelectNodes(\"gears/gear\")",
				"critters.xml", "/chummer/metatypes/metatype/gears/gear", Gear),
			// - "Exotic" names take their own branch and become a new skill, never looked up
			Rule("critter skills", "frmMetatype.cs:774",
				"objXmlCritter.SelectNodes(\"skills/skill\")",
				"critters.xml", "/chummer/metatypes/metatype/skills/skill[not(contains(., 'Exotic'))]", Skills),
			Rule("critter skill groups", "frmMetatype.cs:807",
				"objXmlCritter.SelectNodes(\"skills/group\")",
				"critters.xml", "/chummer/metatypes/metatype/skills/group", SkillGroups, ByTextToText),

			Rule("metatype powers", "frmMetatype.cs:635",
				"objXmlCritter.SelectNodes(\"powers/power\")",
				"metatypes.xml", "/chummer/metatypes/metatype/powers/power", CritterPowers),
			Rule("metatype optional powers", "frmSelectCritterPower.cs:325",
				"SelectNodes(\"optionalpowers/power\")",
				"metatypes.xml", "/chummer/metatypes/metatype/optionalpowers/power", CritterPowers),
			Rule("metavariant powers", "frmMetatype.cs:657",
				"objXmlMetavariant.SelectNodes(\"powers/power\")",
				"metatypes.xml", "/chummer/metatypes/metatype/metavariants/metavariant/powers/power", CritterPowers),
			Rule("metatype positive qualities", "frmMetatype.cs:521",
				"objXmlMetatype.SelectNodes(\"qualities/positive/quality\")",
				"metatypes.xml", "/chummer/metatypes/metatype/qualities/positive/quality", Qualities),
			Rule("metatype negative qualities", "frmMetatype.cs:542",
				"objXmlMetatype.SelectNodes(\"qualities/negative/quality\")",
				"metatypes.xml", "/chummer/metatypes/metatype/qualities/negative/quality", Qualities),
			Rule("metavariant positive qualities", "frmMetatype.cs:574",
				"objXmlMetavariant.SelectNodes(\"qualities/positive/quality\")",
				"metatypes.xml", "/chummer/metatypes/metatype/metavariants/metavariant/qualities/positive/quality", Qualities),
			Rule("metavariant negative qualities", "frmMetatype.cs:595",
				"objXmlMetavariant.SelectNodes(\"qualities/negative/quality\")",
				"metatypes.xml", "/chummer/metatypes/metatype/metavariants/metavariant/qualities/negative/quality", Qualities),
			Rule("metatype positive quality restriction", "frmSelectQuality.cs:334",
				"\"qualityrestriction/\"",
				"metatypes.xml", "/chummer/metatypes/metatype/qualityrestriction/positive/quality", Qualities),
			Rule("metatype negative quality restriction", "frmSelectQuality.cs:334",
				"\"qualityrestriction/\"",
				"metatypes.xml", "/chummer/metatypes/metatype/qualityrestriction/negative/quality", Qualities),

			Rule("vessel powers", "frmCareer.cs:2957",
				"objSelected.SelectNodes(\"powers/power\")",
				"vessels.xml", "/chummer/metatypes/metatype/powers/power", CritterPowers),
			Rule("tradition spirits", "SpiritControl.cs:408",
				"\"\\\"]/spirits/spirit\"",
				"traditions.xml", "/chummer/traditions/tradition/spirits/spirit", Critters),
			Rule("stream sprites", "SpiritControl.cs:408",
				"\"\\\"]/spirits/spirit\"",
				"streams.xml", "/chummer/traditions/tradition/spirits/spirit", Critters),

			Rule("quality powers", "frmCreate.cs:7839",
				"objXmlQuality.SelectNodes(\"powers/power\")",
				"qualities.xml", "/chummer/qualities/quality/powers/power", CritterPowers),
			Rule("quality addqualities", "frmCreate.cs:7782",
				"objXmlQuality.SelectNodes(\"addqualities/addquality\")",
				"qualities.xml", "/chummer/qualities/quality/addqualities/addquality", Qualities),
			Rule("quality latentsource", "frmCareer.cs:8820",
				"objXmlQuality[\"latentsource\"].InnerText == objQuality.Name",
				"qualities.xml", "/chummer/qualities/quality/latentsource", Qualities),

			// - a requirement naming nothing can never be met
			// - a missed required name or metatype category also crashes the hint text on null
			Rule("quality forbidden quality", "frmSelectQuality.cs:436",
				"objXmlForbidden.Name == \"quality\"",
				"qualities.xml", "/chummer/qualities/quality/forbidden/oneof/quality", Qualities),
			Rule("quality forbidden metatype", "frmSelectQuality.cs:449",
				"objXmlForbidden.Name == \"metatype\"",
				"qualities.xml", "/chummer/qualities/quality/forbidden/oneof/metatype", Metatypes),
			Rule("quality forbidden metatype category", "frmSelectQuality.cs:464",
				"objXmlForbidden.Name == \"metatypecategory\"",
				"qualities.xml", "/chummer/qualities/quality/forbidden/oneof/metatypecategory", MetatypeCategories,
				ByTextToText),
			Rule("quality requires one quality", "frmSelectQuality.cs:537",
				"objXmlRequired.Name == \"quality\"",
				"qualities.xml", "/chummer/qualities/quality/required/oneof/quality", Qualities),
			Rule("quality requires one metatype", "frmSelectQuality.cs:556",
				"objXmlRequired.Name == \"metatype\"",
				"qualities.xml", "/chummer/qualities/quality/required/oneof/metatype", Metatypes),
			Rule("quality requires one metatype category", "frmSelectQuality.cs:572",
				"objXmlRequired.Name == \"metatypecategory\"",
				"qualities.xml", "/chummer/qualities/quality/required/oneof/metatypecategory", MetatypeCategories,
				ByTextToText),
			Rule("quality requires one metavariant", "frmSelectQuality.cs:588",
				"objXmlRequired.Name == \"metavariant\"",
				"qualities.xml", "/chummer/qualities/quality/required/oneof/metavariant", Metavariants),
			Rule("quality requires all qualities", "frmSelectQuality.cs:842",
				"objXmlRequired.Name == \"quality\"",
				"qualities.xml", "/chummer/qualities/quality/required/allof/quality", Qualities),
			Rule("quality requires all metatypes", "frmSelectQuality.cs:861",
				"objXmlRequired.Name == \"metatype\"",
				"qualities.xml", "/chummer/qualities/quality/required/allof/metatype", Metatypes),
			Rule("quality requires all skills", "frmSelectQuality.cs:938",
				"objXmlRequired.Name == \"skill\"",
				"qualities.xml", "/chummer/qualities/quality/required/allof/skill", Skills, ByNameChild),
			Rule("quality requires all cyberware", "frmSelectQuality.cs:1013",
				"objXmlRequired.SelectNodes(\"cyberware\")",
				"qualities.xml", "/chummer/qualities/quality/required/allof/cyberwares/cyberware", AnyImplant),
			Rule("quality requires all bioware", "frmSelectQuality.cs:1034",
				"objXmlRequired.SelectNodes(\"bioware\")",
				"qualities.xml", "/chummer/qualities/quality/required/allof/cyberwares/bioware", AnyImplant),
			Rule("quality requires all cyberware plugins", "frmSelectQuality.cs:1089",
				"objXmlRequired.SelectNodes(\"cyberwareplugin\")",
				"qualities.xml", "/chummer/qualities/quality/required/allof/cyberwares/cyberwareplugin", AnyImplant),

			Rule("power requires all qualities", "frmSelectPower.cs:255",
				"objXmlPower.SelectNodes(\"required/allof/quality\")",
				"powers.xml", "/chummer/powers/power/required/allof/quality", Qualities),
			Rule("power requires one quality", "frmSelectPower.cs:274",
				"objXmlPower.SelectNodes(\"required/oneof/quality\")",
				"powers.xml", "/chummer/powers/power/required/oneof/quality", Qualities),
			Rule("metamagic requires all metamagics", "frmSelectMetamagic.cs:264",
				"SelectNodes(\"required/allof/metamagic\")",
				"metamagic.xml", "/chummer/metamagics/metamagic/required/allof/metamagic", Metamagics),
			Rule("metamagic requires all metatypes", "frmSelectMetamagic.cs:318",
				"SelectNodes(\"required/allof/metatype\")",
				"metamagic.xml", "/chummer/metamagics/metamagic/required/allof/metatype", Metatypes),
			Rule("metamagic requires all qualities", "frmSelectMetamagic.cs:350",
				"SelectNodes(\"required/allof/quality\")",
				"metamagic.xml", "/chummer/metamagics/metamagic/required/allof/quality", Qualities),
			Rule("echo requires all echoes", "frmSelectMetamagic.cs:288",
				"SelectNodes(\"required/allof/echo\")",
				"echoes.xml", "/chummer/echoes/echo/required/allof/echo", Echoes),

			Rule("pack positive qualities", "frmCreate.cs:20307",
				"objXmlKit.SelectNodes(\"qualities/positive/quality\")",
				"packs.xml", "/chummer/packs/pack/qualities/positive/quality", Qualities),
			Rule("pack skills", "frmCreate.cs:20434",
				"objXmlKit.SelectNodes(\"skills/skill\")",
				"packs.xml", "/chummer/packs/pack/skills/skill", Skills, ByNameChild),
			Rule("pack skill groups", "frmCreate.cs:20511",
				"objXmlKit.SelectNodes(\"skills/skillgroup\")",
				"packs.xml", "/chummer/packs/pack/skills/skillgroup", SkillGroups,
				new[] { new KeyField("name", ".", false) }),
			Rule("pack powers", "frmCreate.cs:20665",
				"objXmlKit.SelectNodes(\"powers/power\")",
				"packs.xml", "/chummer/packs/pack/powers/power", Powers, ByNameChild),
			Rule("pack programs", "frmCreate.cs:20725",
				"objXmlKit.SelectNodes(\"programs/program\")",
				"packs.xml", "/chummer/packs/pack/programs/program", Programs, ByNameChild),
			Rule("pack spells", "frmCreate.cs:20815",
				"objXmlKit.SelectNodes(\"spells/spell\")",
				"packs.xml", "/chummer/packs/pack/spells/spell", Spells),
			Rule("pack armor", "frmCreate.cs:20978",
				"objXmlKit.SelectNodes(\"armors/armor\")",
				"packs.xml", "/chummer/packs/pack/armors/armor", Armor, ByNameChild),
			Rule("pack armor mods", "frmCreate.cs:20990",
				"objXmlArmor.SelectNodes(\"mods/mod\")",
				"packs.xml", "/chummer/packs/pack/armors/armor/mods/mod", ArmorMods, ByNameChild),
			Rule("pack weapons", "frmCreate.cs:21041",
				"objXmlKit.SelectNodes(\"weapons/weapon\")",
				"packs.xml", "/chummer/packs/pack/weapons/weapon", Weapons, ByNameChild),
			Rule("pack weapon accessories", "frmCreate.cs:21057",
				"objXmlWeapon.SelectNodes(\"accessories/accessory\")",
				"packs.xml", "/chummer/packs/pack/weapons/weapon/accessories/accessory", Accessories, ByNameChild),
			Rule("pack cyberware", "frmCreate.cs:21130",
				"objXmlKit.SelectNodes(\"cyberwares/cyberware\")",
				"packs.xml", "/chummer/packs/pack/cyberwares/cyberware", Cyberware, ByNameChild),
			Rule("pack cyberware children", "frmCreate.cs:21153",
				"objXmlCyberware.SelectNodes(\"cyberwares/cyberware\")",
				"packs.xml", "/chummer/packs/pack/cyberwares/cyberware/cyberwares/cyberware", Cyberware, ByNameChild),
			Rule("pack cyberware grade", "frmCreate.cs:21140",
				"ConvertToCyberwareGrade(objXmlCyberware[\"grade\"]",
				"packs.xml", "/chummer/packs/pack/cyberwares/cyberware/grade", CyberwareGrades),
			Rule("pack bioware", "frmCreate.cs:21209",
				"objXmlKit.SelectNodes(\"biowares/bioware\")",
				"packs.xml", "/chummer/packs/pack/biowares/bioware", Bioware, ByNameChild),
			Rule("pack bioware grade", "frmCreate.cs:21219",
				"ConvertToCyberwareGrade(objXmlBioware[\"grade\"]",
				"packs.xml", "/chummer/packs/pack/biowares/bioware/grade", BiowareGrades),
			// - AddPACKSGear narrows by category only when the entry has one
			Rule("pack gear", "frmCreate.cs:22872",
				"objXmlGear[\"category\"] != null",
				"packs.xml", "/chummer/packs/pack/gears//gear", Gear,
				new[] { new KeyField("name", "name", false), new KeyField("category", "category", true) }),
			Rule("pack vehicles", "frmCreate.cs:21282",
				"objXmlKit.SelectNodes(\"vehicles/vehicle\")",
				"packs.xml", "/chummer/packs/pack/vehicles/vehicle", Vehicles, ByNameChild),
			Rule("pack vehicle mods", "frmCreate.cs:21310",
				"objXmlVehicle.SelectNodes(\"mods/mod\")",
				"packs.xml", "/chummer/packs/pack/vehicles/vehicle/mods/mod", VehicleMods, ByNameChild),
			// - unlike pack gear, the vehicle's own gear is looked up by name alone
			Rule("pack vehicle gear", "frmCreate.cs:21350",
				"objXmlVehicle.SelectNodes(\"gears/gear\")",
				"packs.xml", "/chummer/packs/pack/vehicles/vehicle/gears/gear", Gear, ByNameChild),
			Rule("pack vehicle weapons", "frmCreate.cs:21391",
				"objXmlVehicle.SelectNodes(\"weapons/weapon\")",
				"packs.xml", "/chummer/packs/pack/vehicles/vehicle/weapons/weapon", Weapons, ByNameChild),
			Rule("pack vehicle weapon accessories", "frmCreate.cs:21423",
				"objXmlWeapon.SelectNodes(\"accessories/accessory\")",
				"packs.xml", "/chummer/packs/pack/vehicles/vehicle/weapons/weapon/accessories/accessory", Accessories,
				ByNameChild),
		};
	}
}
