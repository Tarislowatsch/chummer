using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// - the real data holds no near misses: its theories cannot pin these key shapes
	// - each case mirrors one lookup shape a rule in CrossFileReferences relies on
	public class CrossFileReferenceDetectionTests
	{
		private const string Weapons =
			"<chummer><weapons>"
			+ "<weapon><name>Spur</name><category>Cyberware Blades</category></weapon>"
			+ "<weapon><name>Shock Glove</name><category>Gear</category></weapon>"
			+ "</weapons></chummer>";

		[Fact]
		public void TextReferenceToAnExistingNameResolves()
		{
			Assert.Empty(UnresolvedKeys(Rule(Target("weapons.xml", "/chummer/weapons/weapon")),
				Source("<gear><name>Glove Kit</name><addweapon>Shock Glove</addweapon></gear>"), Weapons));
		}

		[Fact]
		public void TextReferenceToAMissingNameIsReported()
		{
			Assert.Equal(new[] { "Shock Gloves" }, UnresolvedKeys(Rule(Target("weapons.xml", "/chummer/weapons/weapon")),
				Source("<gear><name>Glove Kit</name><addweapon>Shock Gloves</addweapon></gear>"), Weapons));
		}

		// XPath equality in the consumers is exact.
		[Fact]
		public void NameDifferingOnlyInCaseIsReported()
		{
			Assert.Equal(new[] { "shock glove" }, UnresolvedKeys(Rule(Target("weapons.xml", "/chummer/weapons/weapon")),
				Source("<gear><name>Glove Kit</name><addweapon>shock glove</addweapon></gear>"), Weapons));
		}

		// - the shape of the addweapon rules
		[Fact]
		public void TargetFilterExcludesASameNamedItemOutsideIt()
		{
			Assert.Equal(new[] { "Spur" }, UnresolvedKeys(
				Rule(Target("weapons.xml", "/chummer/weapons/weapon[category = 'Gear']")),
				Source("<gear><name>Glove Kit</name><addweapon>Spur</addweapon></gear>"), Weapons));
		}

		[Fact]
		public void CompositeKeyNeedsEveryFieldToMatch()
		{
			string gear = "<chummer><gears><gear><name>Hardening</name><category>Commlink Option</category></gear>"
				+ "</gears></chummer>";

			Assert.Equal(new[] { "Hardening / Cyberdeck Option" }, UnresolvedKeys(
				Rule(Target("gear.xml", "/chummer/gears/gear"), NameAndCategory(optionalCategory: false)),
				Source("<gear><name>Kit</name><gears><usegear><name>Hardening</name>"
					+ "<category>Cyberdeck Option</category></usegear></gears></gear>", "//usegear"),
				gear, "gear.xml"));
		}

		// - the shape of the "pack gear" rule
		[Fact]
		public void AbsentOptionalFieldIsLeftOutOfTheMatch()
		{
			string gear = "<chummer><gears><gear><name>Hardening</name><category>Commlink Option</category></gear>"
				+ "</gears></chummer>";

			Assert.Empty(UnresolvedKeys(
				Rule(Target("gear.xml", "/chummer/gears/gear"), NameAndCategory(optionalCategory: true)),
				Source("<gear><name>Kit</name><gears><usegear><name>Hardening</name></usegear></gears></gear>",
					"//usegear"),
				gear, "gear.xml"));
		}

		// - the consumer reads it unguarded: no fallback to the name alone
		[Fact]
		public void AbsentRequiredFieldIsReported()
		{
			string gear = "<chummer><gears><gear><name>Hardening</name><category>Commlink Option</category></gear>"
				+ "</gears></chummer>";

			Assert.Equal(new[] { "Hardening / (none)" }, UnresolvedKeys(
				Rule(Target("gear.xml", "/chummer/gears/gear"), NameAndCategory(optionalCategory: false)),
				Source("<gear><name>Kit</name><gears><usegear><name>Hardening</name></usegear></gears></gear>",
					"//usegear"),
				gear, "gear.xml"));
		}

		// - the shape of the metatype rules
		[Fact]
		public void ReferenceFoundOnlyInTheFallbackTargetResolves()
		{
			CrossFileReferences.ReferenceRule rule = new CrossFileReferences.ReferenceRule("test", "x.cs:1", "",
				"source.xml", "//quality/required/metatype", ByText(),
				new[]
				{
					new CrossFileReferences.Target("metatypes.xml", "/chummer/metatypes/metatype"),
					new CrossFileReferences.Target("critters.xml", "/chummer/metatypes/metatype"),
				});

			IReadOnlyList<CrossFileReferences.Reference> references = CrossFileReferences.ReferencesIn("source.xml",
				Document("<chummer><qualities><quality><name>Q</name><required><metatype>Kitsune</metatype>"
					+ "</required></quality></qualities></chummer>"), new[] { rule });
			IReadOnlyList<IReadOnlyList<string>> definitions = new[]
				{
					KeysIn(rule, "metatypes.xml", Metatype("Elf")),
					KeysIn(rule, "critters.xml", Metatype("Kitsune")),
				}
				.SelectMany(keys => keys)
				.ToArray();

			Assert.Empty(CrossFileReferences.Unresolved(rule, references, definitions));
		}

		// - the shape of the "critter skills" rule
		[Fact]
		public void SourcePredicateExemptsWhatTheConsumerNeverLooksUp()
		{
			string skills = "<chummer><skills><skill><name>Running</name></skill></skills></chummer>";
			string critter = "<metatype><name>Sea Lion</name><skills><skill>Exotic Range Weapon</skill>"
				+ "<skill>Gymanstics</skill></skills></metatype>";

			Assert.Equal(new[] { "Gymanstics" }, UnresolvedKeys(
				Rule(Target("skills.xml", "/chummer/skills/skill")),
				Source(critter, "//skills/skill[not(contains(., 'Exotic'))]"), skills, "skills.xml"));
		}

		// - the shape of the metatype category and skill group rules
		[Fact]
		public void BareTextTargetIsComparedByItsText()
		{
			string metatypes = "<chummer><categories><category>Metahuman</category></categories></chummer>";

			Assert.Equal(new[] { "Shapeshifters" }, UnresolvedKeys(
				Rule(Target("metatypes.xml", "/chummer/categories/category"),
					new[] { new CrossFileReferences.KeyField(".", ".", false) }),
				Source("<quality><name>Q</name><required><metatypecategory>Shapeshifters</metatypecategory>"
					+ "</required></quality>", "//metatypecategory"),
				metatypes, "metatypes.xml"));
		}

		// Naming the containing entry would send a reader to the wrong element.
		[Fact]
		public void NestedReferenceIsAttributedToTheNearestNamedElement()
		{
			CrossFileReferences.ReferenceRule rule = Rule(Target("critterpowers.xml", "/chummer/powers/power"));
			XmlDocument critters = Document("<chummer><metatypes><metatype><name>Dwarf</name><metavariants>"
				+ "<metavariant><name>Gnome</name><powers><power>Fear</power></powers></metavariant>"
				+ "</metavariants></metatype></metatypes></chummer>");

			CrossFileReferences.Reference reference = CrossFileReferences
				.ReferencesIn("source.xml", critters, new[] { WithSource(rule, "//metavariant/powers/power") })
				.Single();

			Assert.Equal("Gnome", reference.ItemName);
		}

		// - in <mods><name>A</name></mods> the parent's <name> is the reference, not its owner
		[Fact]
		public void NameElementReferenceIsAttributedToItsGrandparent()
		{
			CrossFileReferences.ReferenceRule rule = Rule(Target("armor.xml", "/chummer/mods/mod"));
			XmlDocument armor = Document("<chummer><armors><armor><name>Armor Jacket</name>"
				+ "<mods><name>Fire Resistance</name></mods></armor></armors></chummer>");

			CrossFileReferences.Reference reference = CrossFileReferences
				.ReferencesIn("source.xml", armor, new[] { WithSource(rule, "//mods/name") })
				.Single();

			Assert.Equal("Armor Jacket", reference.ItemName);
		}

		[Fact]
		public void ReferencesAreReadOnlyFromTheRuleSourceFile()
		{
			CrossFileReferences.ReferenceRule rule = WithSource(
				Rule(Target("weapons.xml", "/chummer/weapons/weapon")), "//addweapon");

			Assert.Empty(CrossFileReferences.ReferencesIn("other.xml",
				Document("<chummer><gears><gear><addweapon>X</addweapon></gear></gears></chummer>"), new[] { rule }));
		}

		[Fact]
		public void DefinitionsAreReadOnlyFromTheTargetFile()
		{
			CrossFileReferences.ReferenceRule rule = Rule(Target("weapons.xml", "/chummer/weapons/weapon"));

			Assert.Empty(CrossFileReferences.TargetKeysIn("gear.xml", Document(Weapons), new[] { rule }));
		}

		private static string[] UnresolvedKeys(CrossFileReferences.ReferenceRule rule, SourceSpec source,
			string targetXml, string targetFile = "weapons.xml")
		{
			CrossFileReferences.ReferenceRule sourced = WithSource(rule, source.XPath);
			IReadOnlyList<CrossFileReferences.Reference> references =
				CrossFileReferences.ReferencesIn("source.xml", source.Document, new[] { sourced });

			return CrossFileReferences.Unresolved(sourced, references, KeysIn(sourced, targetFile, targetXml))
				.Select(reference => reference.Display)
				.ToArray();
		}

		private static IReadOnlyList<IReadOnlyList<string>> KeysIn(CrossFileReferences.ReferenceRule rule,
			string fileName, string xml)
		{
			return CrossFileReferences.TargetKeysIn(fileName, Document(xml), new[] { rule })
				.SelectMany(keys => keys.Keys)
				.ToArray();
		}

		private sealed class SourceSpec
		{
			public SourceSpec(XmlDocument document, string xPath)
			{
				Document = document;
				XPath = xPath;
			}

			public XmlDocument Document { get; }

			public string XPath { get; }
		}

		private static SourceSpec Source(string entry, string xPath = "//addweapon")
		{
			return new SourceSpec(Document("<chummer><entries>" + entry + "</entries></chummer>"), xPath);
		}

		private static CrossFileReferences.ReferenceRule Rule(CrossFileReferences.Target target,
			IReadOnlyList<CrossFileReferences.KeyField> keyFields = null)
		{
			return new CrossFileReferences.ReferenceRule("test", "x.cs:1", "", "source.xml", "//none",
				keyFields ?? ByText(), new[] { target });
		}

		private static CrossFileReferences.ReferenceRule WithSource(CrossFileReferences.ReferenceRule rule,
			string sourceXPath)
		{
			return new CrossFileReferences.ReferenceRule(rule.Id, rule.Consumer, rule.Reads, rule.SourceFile,
				sourceXPath, rule.KeyFields, rule.Targets);
		}

		private static CrossFileReferences.Target Target(string fileName, string itemXPath)
		{
			return new CrossFileReferences.Target(fileName, itemXPath);
		}

		private static CrossFileReferences.KeyField[] ByText()
		{
			return new[] { new CrossFileReferences.KeyField(".", "name", false) };
		}

		private static CrossFileReferences.KeyField[] NameAndCategory(bool optionalCategory)
		{
			return new[]
			{
				new CrossFileReferences.KeyField("name", "name", false),
				new CrossFileReferences.KeyField("category", "category", optionalCategory),
			};
		}

		private static string Metatype(string name)
		{
			return "<chummer><metatypes><metatype><name>" + name + "</name></metatype></metatypes></chummer>";
		}

		private static XmlDocument Document(string xml)
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(xml);
			return document;
		}
	}
}
