using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using Chummer.Tests.Consumption.Expressions;
using Chummer.Tests.Kernel;

namespace Chummer.Tests.Consumption
{
	// - every Convert.ToDecimal/ToDouble on data passes en-US, where a comma groups thousands
	// - "0,5" converts without a throw, to 5
	// - field-agnostic on purpose: no consumer table to fall behind a new direct conversion
	public static class DecimalCommas
	{
		// - exactly the two Convert overloads' styles: one of them turning a comma into a number is enough
		public static bool ReadsAsNumber(string value)
		{
			if (value.IndexOf(',') < 0)
				return false;

			decimal asDecimal;
			double asDouble;
			return decimal.TryParse(value, NumberStyles.Number, ExpressionChecks.ApplicationCulture, out asDecimal)
				|| double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands,
					ExpressionChecks.ApplicationCulture, out asDouble);
		}

		// - leaf elements and attributes: frmSelectCritterPower.cs:483 converts an attribute
		// - public so the detection tests can drive it with hand-built XML
		public static IEnumerable<string> CommaNumbersIn(XmlDocument document)
		{
			foreach (XmlElement leaf in document.SelectNodes("//*[not(*)]").Cast<XmlElement>())
			{
				if (ReadsAsNumber(leaf.InnerText))
					yield return RuleCorpus.ItemLabel(leaf.ParentNode) + " " + leaf.Name + " = '" + leaf.InnerText + "'";
			}

			foreach (XmlAttribute attribute in document.SelectNodes("//@*").Cast<XmlAttribute>())
			{
				if (ReadsAsNumber(attribute.Value))
				{
					yield return RuleCorpus.ItemLabel(attribute.OwnerElement) + " " + attribute.OwnerElement.Name
						+ "/@" + attribute.Name + " = '" + attribute.Value + "'";
				}
			}
		}
	}
}
