using System;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// - the theory next door finds nothing: all 29 of its cases would pass unchanged if the detection underneath stopped detecting
	// - and the real data holds no near-miss that would notice the difference - no entry with a <Name> instead of a <name>, none hiding a required element one level deeper
	// - these drive the same code with hand-built XML instead, so each rule is pinned by something that fails when it changes
	//
	// - the boundary worth holding down hardest is presence against content
	// - an empty element satisfies the contract on purpose, because that is what the code tolerates
	// - tightening it to "non-empty" would look like a stricter check while reporting 274 entries the application handles perfectly well
	public class RequiredFieldDetectionTests
	{
		[Fact]
		public void AbsentElementIsReported()
		{
			Assert.Equal(new[] { "avail" },
				AbsentFieldsIn("<gear><name>Commlink</name></gear>", "name", "avail"));
		}

		[Fact]
		public void PresentElementIsAccepted()
		{
			Assert.Empty(AbsentFieldsIn("<gear><name>Commlink</name><avail>4</avail></gear>",
				"name", "avail"));
		}

		// - the deliberate line between this check and a value check
		// - 40 programs and 7 program options carry an empty <maxrating>
		// - clsUnique.cs:5589 reads that element and tests its text for "" before converting, so empty is the data's way of saying "no maximum"
		// - 33 spells likewise carry an empty <descriptor>
		// - reporting those would make the test disagree with the behaviour it describes
		[Fact]
		public void PresentButEmptyElementSatisfiesTheContract()
		{
			Assert.Empty(AbsentFieldsIn("<program><name>Analyze</name><maxrating></maxrating></program>",
				"name", "maxrating"));
		}

		// - XmlNode's indexer matches the element name exactly, so a Create reading node["name"] genuinely does not see a <Name>
		// - accepting it here would describe a tolerance the application has not got
		[Fact]
		public void FieldNameIsMatchedCaseSensitively()
		{
			Assert.Equal(new[] { "name" }, AbsentFieldsIn("<gear><Name>Commlink</Name></gear>", "name"));
		}

		// - the same indexer looks at direct children only, which is what separates a catalogue entry's own <name> from the <name> of a reference nested inside it
		// - a gear entry whose only <name> sits under <gears> is exactly the shape this has to reject
		[Fact]
		public void FieldNestedInsideAWrapperDoesNotCount()
		{
			Assert.Equal(new[] { "name" },
				AbsentFieldsIn("<gear><gears><gear><name>Copy Protection</name></gear></gears></gear>",
					"name"));
		}

		// - gear.xml is split across three Create methods by category
		// - the three rules have to cover the collection exactly once each
		// - an overlap would double-report
		// - a gap would leave a category unchecked, which is the likelier of the two
		// - the third rule is written as "not the other two" precisely so a new category lands somewhere
		[Fact]
		public void TheThreeGearRulesPartitionTheCollection()
		{
			XmlDocument document = Document(
				"<chummer><gears>"
				+ "<gear><name>Meta Link</name><category>Commlink</category></gear>"
				+ "<gear><name>Iris Orb</name><category>Commlink Operating System</category></gear>"
				+ "<gear><name>Novacoke</name><category>Drugs</category></gear>"
				+ "<gear><name>Nameless</name></gear>"
				+ "</gears></chummer>");

			Assert.Equal(new[] { "Meta Link" }, MatchedNames("Commlink", document));
			Assert.Equal(new[] { "Iris Orb" }, MatchedNames("OperatingSystem", document));
			// - an entry with no <category> at all falls to Gear rather than out of the check, which is the whole point of writing the third rule as a negation
			Assert.Equal(new[] { "Novacoke", "Nameless" }, MatchedNames("Gear", document));
		}

		// - CreateChildren recurses into its own result (clsEquipment.cs:9968), so a <usegear> is read the same way however deep it sits
		// - anchoring the rule to one level would leave the deeper ones unchecked while the case still looked healthy
		[Fact]
		public void TheUsegearRuleReachesNestedChildren()
		{
			XmlDocument document = Document(
				"<chummer><gears><gear><name>Suite</name><gears>"
				+ "<usegear><name>Analyze</name><category>Matrix Programs</category></usegear>"
				+ "<usegear><name>Browse</name><category>Matrix Programs</category>"
				+ "<gears><usegear><name>Optimization</name></usegear></gears>"
				+ "</usegear>"
				+ "</gears></gear></gears></chummer>");

			DataPaths.RequiredFieldContract contract = Evaluate("Gear child <usegear> reference", document);

			Assert.Equal(3, contract.ItemCount);
			Assert.Equal(new[] { "category" },
				contract.MissingFields.Select(missing => missing.Field).ToArray());
		}

		// - the mirror image of the test above, and the reason both exist
		// - clsEquipment.cs:9823 selects "gears/gear" on the catalogue entry and reads it in place, without recursing the way CreateChildren does two lines further down
		// - so the two sibling rules carry opposite axis decisions on purpose
		// - that is exactly the pair a later "let us make these consistent" pass would flatten, and only this test would notice
		[Fact]
		public void TheChildGearRuleStopsAtTheFirstLevel()
		{
			XmlDocument document = Document(
				"<chummer><gears><gear><name>Suite</name><gears>"
				+ "<gear><name>Analyze</name><category>Matrix Programs</category></gear>"
				+ "<gear><name>Browse</name><category>Matrix Programs</category>"
				+ "<gears><gear><name>Optimization</name></gear></gears>"
				+ "</gear>"
				+ "</gears></gear></gears></chummer>");

			DataPaths.RequiredFieldContract contract = Evaluate("Gear child <gear> reference", document);

			// - the deeper <gear> is neither counted nor faulted for its absent <category>, because no Create ever reads it
			Assert.Equal(2, contract.ItemCount);
			Assert.Empty(contract.MissingFields);
		}

		// - <mount> is required of an accessory some weapon builds in and of no other, because clsEquipment.cs:4346 reads it only on that path
		// - the rule is therefore a predicate over references rather than over the collection
		// - its two halves have to be tested apart
		[Fact]
		public void AReferencedAccessoryWithoutMountIsReported()
		{
			DataPaths.RequiredFieldContract contract =
				Evaluate("WeaponAccessory built into a weapon", WeaponsDocument("Smartgun System"));

			Assert.Equal(1, contract.ItemCount);
			Assert.Equal("Smartgun System", contract.MissingFields.Single().ItemName);
		}

		[Fact]
		public void AnAccessoryNoWeaponReferencesIsNotHeldToMount()
		{
			DataPaths.RequiredFieldContract contract =
				Evaluate("WeaponAccessory built into a weapon", WeaponsDocument("Silencer"));

			Assert.Equal(0, contract.ItemCount);
			Assert.Empty(contract.MissingFields);
		}

		// - a nested reference missing its <name> has no name of its own to report
		// - "(unnamed)" would send a reader hunting through the file
		// - the catalogue entry holding it is the thing to open
		[Fact]
		public void ANamelessNestedReferenceIsAttributedToTheEntryHoldingIt()
		{
			XmlDocument document = Document(
				"<chummer><gears><gear><name>Suite: Basic</name>"
				+ "<gears><usegear><category>Matrix Programs</category></usegear></gears>"
				+ "</gear></gears></chummer>");

			DataPaths.RequiredFieldContract contract = Evaluate("Gear child <usegear> reference", document);

			Assert.Equal("Suite: Basic", contract.MissingFields.Single().ItemName);
		}

		// - a rule whose file is never scanned produces no contract, no theory case and no failure, so its entity type drops out of the suite in silence
		// - the real table is correct, which is why this drives the check with a table of its own rather than waiting for a run to reach it
		[Fact]
		public void ARuleNamingAFileThatWasNotScannedIsRejected()
		{
			DataPaths.RequiredFieldRule orphaned =
				new DataPaths.RequiredFieldRule("Vehicle", "vehicels.xml", "/chummer/vehicles/vehicle",
					new[] { "name" });

			InvalidOperationException error = Assert.Throws<InvalidOperationException>(
				() => DataPaths.EnsureEveryRuleWasEvaluated(
					new[] { orphaned }, new DataPaths.RequiredFieldContract[0]));

			Assert.Contains("Vehicle -> vehicels.xml", error.Message);
		}

		[Fact]
		public void ARuleWhoseFileWasScannedIsAccepted()
		{
			DataPaths.RequiredFieldRule rule =
				new DataPaths.RequiredFieldRule("Vehicle", "vehicles.xml", "/chummer/vehicles/vehicle",
					new[] { "name" });

			DataPaths.EnsureEveryRuleWasEvaluated(
				new[] { rule },
				new[] { new DataPaths.RequiredFieldContract(rule, "vehicles.xml", 0,
					new DataPaths.MissingField[0]) });
		}

		// - the entity name is the theory's case id, so a duplicate makes xUnit drop one case and one rule stops being checked
		// - without this the only symptom is the entity lookup throwing ArgumentException, which on net48 does not even name the key
		// - driven with a table of its own for the same reason as the orphan check above
		[Fact]
		public void TwoRulesSharingAnEntityNameAreRejected()
		{
			InvalidOperationException error = Assert.Throws<InvalidOperationException>(
				() => DataPaths.EnsureEntityNamesAreUnique(new[]
				{
					RuleNamed("Metamagic", "metamagic.xml"),
					RuleNamed("Metamagic", "echoes.xml"),
				}));

			Assert.Contains("'Metamagic' declared 2 times", error.Message);
		}

		[Fact]
		public void RulesWithDistinctEntityNamesAreAccepted()
		{
			DataPaths.EnsureEntityNamesAreUnique(new[]
			{
				RuleNamed("Metamagic", "metamagic.xml"),
				RuleNamed("Echo", "echoes.xml"),
			});
		}

		private static DataPaths.RequiredFieldRule RuleNamed(string entity, string fileName)
		{
			return new DataPaths.RequiredFieldRule(entity, fileName, "/chummer", new[] { "name" });
		}

		// Runs the real rule, not a copy of it, so the XPath the suite uses is the
		// one under test here.
		private static DataPaths.RequiredFieldContract Evaluate(string entity, XmlDocument document)
		{
			DataPaths.RequiredFieldRule rule =
				DataPaths.RequiredFieldRules.Single(candidate => candidate.Entity == entity);

			return DataPaths.EvaluateRequiredFields(rule, rule.FileName, document);
		}

		private static string[] MatchedNames(string entity, XmlDocument document)
		{
			DataPaths.RequiredFieldRule rule =
				DataPaths.RequiredFieldRules.Single(candidate => candidate.Entity == entity);

			return document.SelectNodes(rule.ItemXPath).Cast<XmlNode>()
				.Select(node => node["name"].InnerText)
				.ToArray();
		}

		private static string[] AbsentFieldsIn(string itemXml, params string[] requiredFields)
		{
			return DataPaths.MissingRequiredFieldsIn(Document(itemXml).DocumentElement, requiredFields)
				.Select(missing => missing.Field)
				.ToArray();
		}

		// One catalogue accessory with no <mount>, referenced by a weapon under the
		// name given - the shape weapons.xml uses for a built-in accessory.
		private static XmlDocument WeaponsDocument(string referencedAccessory)
		{
			return Document(
				"<chummer>"
				+ "<weapons><weapon><name>Ares Predator</name>"
				+ "<accessories><accessory>" + referencedAccessory + "</accessory></accessories>"
				+ "</weapon></weapons>"
				+ "<accessories><accessory><name>Smartgun System</name></accessory></accessories>"
				+ "</chummer>");
		}

		private static XmlDocument Document(string xml)
		{
			XmlDocument document = new XmlDocument { XmlResolver = null };
			document.LoadXml(xml);

			return document;
		}
	}
}
