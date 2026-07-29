using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using Xunit;

namespace Chummer.Tests
{
	// - the 27 schemas in Chummer/data are used by zero lines of application code (no XmlSchemaSet, no ValidationType anywhere in the repo)
	// - well-formedness alone only proves the data is well-formed XML, not that it matches its own schema
	// - each pair is its own test case, so a single bad pair does not hide the rest
	// - this proves the real data validates against its schema, but it does not exercise synthetic invalid shapes to prove the schema still rejects garbage; that broader guarantee is a meaningfully bigger scope than "make the schema match the data" and is left for later if it turns out to matter
	public class XsdSchemaValidationTests
	{
		// - long enough to show every error this repo has today (worst case is qualities.xml at 15), without an unbounded message if a future file somehow racks up hundreds
		private const int MaxErrorsInMessage = 20;

		[Theory]
		[MemberData(nameof(DataPaths.TopLevelRuleXmlWithSchemaFiles), MemberType = typeof(DataPaths))]
		public void RuleXmlMatchesItsSchema(string xmlPath, string xsdPath)
		{
			List<string> errors = new List<string>();

			XmlReaderSettings settings = new XmlReaderSettings
			{
				ValidationType = ValidationType.Schema
			};
			settings.Schemas.Add(null, xsdPath);
			settings.ValidationEventHandler += (sender, e) => errors.Add(e.Severity + ": " + e.Message);

			using (XmlReader reader = XmlReader.Create(xmlPath, settings))
			{
				while (reader.Read())
				{
				}
			}

			if (errors.Count > 0)
			{
				StringBuilder message = new StringBuilder();
				message.Append(xmlPath).Append(" does not validate against ").Append(xsdPath)
					.Append(" (").Append(errors.Count).Append(" error(s)):");
				foreach (string error in errors.Take(MaxErrorsInMessage))
				{
					message.Append("\n  ").Append(error);
				}
				if (errors.Count > MaxErrorsInMessage)
				{
					message.Append("\n  ... and ").Append(errors.Count - MaxErrorsInMessage).Append(" more");
				}
				Assert.Fail(message.ToString());
			}
		}
	}
}
