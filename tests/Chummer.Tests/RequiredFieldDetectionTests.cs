using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// - a detection gone blind would leave all 29 data theory cases passing unchanged
	// - the real data holds no near-miss that would notice the difference
	// - hand-built XML pins each rule to something that fails when it changes
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

		// - empty satisfies on purpose: clsUnique.cs:5589 tests the text for "" before converting
		// - 47 program <maxrating> and 33 spell <descriptor> elements are empty in the real data
		// - reporting those would make the test disagree with the behaviour it describes
		[Fact]
		public void PresentButEmptyElementSatisfiesTheContract()
		{
			Assert.Empty(AbsentFieldsIn("<program><name>Analyze</name><maxrating></maxrating></program>",
				"name", "maxrating"));
		}

		// - XmlNode's indexer is case-sensitive: a Create genuinely does not see a <Name>
		// - accepting it here would describe a tolerance the application has not got
		[Fact]
		public void FieldNameIsMatchedCaseSensitively()
		{
			Assert.Equal(new[] { "name" }, AbsentFieldsIn("<gear><Name>Commlink</Name></gear>", "name"));
		}

		// - the indexer reads direct children only: an entry's <name> is not its reference's
		// - a gear entry whose only <name> sits under <gears> is exactly the shape to reject
		[Fact]
		public void FieldNestedInsideAWrapperDoesNotCount()
		{
			Assert.Equal(new[] { "name" },
				AbsentFieldsIn("<gear><gears><gear><name>Copy Protection</name></gear></gears></gear>",
					"name"));
		}

		// - an overlap would double-report an entry
		// - a gap would leave a whole category unchecked
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
			// - "Nameless" falls to Gear rather than out of the check: the third rule is a negation
			Assert.Equal(new[] { "Novacoke", "Nameless" }, MatchedNames("Gear", document));
		}

		// - CreateChildren recurses into its own result (clsEquipment.cs:9968)
		// - a rule anchored to one level would leave deeper <usegear> references unchecked
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

		// - clsEquipment.cs:9823 selects "gears/gear" in place, without CreateChildren's recursion
		// - the two sibling rules carry opposite axis decisions on purpose
		// - a later "make these consistent" pass would flatten exactly this pair
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

			// - the deeper <gear> is neither counted nor faulted: no Create ever reads it
			Assert.Equal(2, contract.ItemCount);
			Assert.Empty(contract.MissingFields);
		}

		// - clsEquipment.cs:4346 reads <mount> only for an accessory some weapon builds in
		// - the rule is a predicate over references, not over the collection
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

		// - the one label branch with no ancestor to borrow a name from
		[Fact]
		public void ATopLevelEntryWithoutANameIsReportedAsUnnamed()
		{
			DataPaths.MissingField missing = DataPaths.MissingRequiredFieldsIn(
					Document("<gear><avail>4</avail></gear>").DocumentElement, new[] { "name" })
				.Single();

			Assert.Equal("(unnamed)", missing.ItemName);
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

		// - a rule whose file is never scanned drops its entity type out of the suite in silence
		// - driven with a hand-built table: the real one holds no orphan to exercise this
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

		// - the entity name is the theory's case id: xUnit silently drops a duplicated case
		// - the only other symptom is an ArgumentException that on net48 does not even name the key
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

		// - the pinned hashes only prove what today's sources hash to, never what a change would do
		// - these pin the sensitivity itself: what must move the hash, what must not
		// - a rewrite of the extraction could otherwise lose a property and get re-pinned green
		[Fact]
		public void DeletingAReadChangesTheFingerprint()
		{
			Assert.NotEqual(
				FingerprintOf(
					"_name = objXmlSample[\"name\"].InnerText;",
					"_avail = objXmlSample[\"avail\"].InnerText;"),
				FingerprintOf(
					"_name = objXmlSample[\"name\"].InnerText;"));
		}

		// - a read moved into a try relaxes the contract without touching the read itself
		[Fact]
		public void WrappingAReadInTryChangesTheFingerprint()
		{
			Assert.NotEqual(
				FingerprintOf("_avail = objXmlSample[\"avail\"].InnerText;"),
				FingerprintOf(
					"try",
					"{",
					"_avail = objXmlSample[\"avail\"].InnerText;",
					"}",
					"catch { }"));
		}

		// - the false-alarm side of the claim: cosmetic edits must not demand a re-pin
		[Fact]
		public void CommentsAndReindentationDoNotChangeTheFingerprint()
		{
			Assert.Equal(
				FingerprintOf("_avail = objXmlSample[\"avail\"].InnerText;"),
				FingerprintOf(
					"// a comment the hash must not see",
					"        _avail  =  objXmlSample[\"avail\"].InnerText;"));
		}

		// - the sites scan matches a signature broken across lines
		// - a stricter scan here would drop the class while the count guard stayed right
		[Fact]
		public void ASignatureBrokenAfterTheParenIsStillFingerprinted()
		{
			string[] lines =
			{
				"public class Sample",
				"{",
				"	public void Create(",
				"		XmlNode objXmlSample)",
				"	{",
				"		_name = objXmlSample[\"name\"].InnerText;",
				"	}",
				"}",
			};

			Assert.Equal(
				FingerprintOf("_name = objXmlSample[\"name\"].InnerText;"),
				DataPaths.FingerprintsIn(lines)["Sample"]);
		}

		// - a second Create in one class would silently shadow the first under the pinned table's key
		[Fact]
		public void TwoCreatesInOneClassAreRejected()
		{
			string[] lines =
			{
				"public class Sample",
				"{",
				"	public void Create(XmlNode objXmlSample)",
				"	{",
				"	}",
				"	public void Create(XmlNode objXmlSample, bool blnExtra)",
				"	{",
				"	}",
				"}",
			};

			InvalidOperationException error = Assert.Throws<InvalidOperationException>(
				() => DataPaths.FingerprintsIn(lines));

			Assert.Contains("Sample", error.Message);
		}

		private static string FingerprintOf(params string[] createBody)
		{
			List<string> lines = new List<string>
			{
				"public class Sample",
				"{",
				"	public void Create(XmlNode objXmlSample)",
				"	{",
			};
			lines.AddRange(createBody);
			lines.Add("	}");
			lines.Add("}");

			return DataPaths.FingerprintsIn(lines.ToArray())["Sample"];
		}

		// Runs the real rule, not a copy: the XPath under test is the one the suite uses.
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

		// Mirrors the shape weapons.xml uses for a built-in accessory.
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
