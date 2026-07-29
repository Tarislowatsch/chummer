using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using Xunit;

namespace Chummer.Tests
{
	// - the application itself never validates: no XmlSchemaSet, no ValidationType anywhere
	// - per-pair cases keep one bad pair from hiding the rest
	// - proves the data matches the schema, not that the schema still rejects garbage
	public class XsdSchemaValidationTests
	{
		// Fits today's worst case (qualities.xml at 15) without an unbounded message.
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
