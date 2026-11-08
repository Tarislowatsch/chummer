using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;
using Chummer.Tests.Kernel;

namespace Chummer.Tests.Consumption.Expressions
{
	// - ExpressionToString turns a critter's "F+2" or "2D6-1" into a number for one Force
	// - Compile runs outside its try: a malformed form throws into the UI
	// - a non-integral result lands in the catch and silently becomes 1
	public static class CritterExpressions
	{
		// - three identical copies: one port, three pins
		public static readonly IReadOnlyList<string> Calculators = new[]
		{
			"SpiritControl.ExpressionToString",
			"frmMetatype.ExpressionToString",
			"frmTest.ExpressionToString",
		};

		// - every caller binds these to loops below /chummer/metatypes/metatype
		// - skills/knowledge is missing on purpose: LoaderConversions checks its direct Convert.ToInt32
		public static readonly IReadOnlyDictionary<string, string[]> LoopArguments =
			new Dictionary<string, string[]>(StringComparer.Ordinal)
			{
				{ "objXmlSkill.Attributes[\"rating\"].InnerText", new[] { "skills/skill/@rating", "skills/group/@rating" } },
				{ "objXmlComplexForm.Attributes[\"rating\"].InnerText", new[] { "complexforms/complexform/@rating" } },
				{ "objXmlComplexForm.Attributes[\"optionrating\"].InnerText", new[] { "complexforms/complexform/@optionrating" } },
				{ "objXmlGear.Attributes[\"rating\"].InnerText", new[] { "gears/gear/@rating" } },
			};

		private static readonly Regex MetatypeField = new Regex(@"^objXmlMetatype\[""(\w+)""\]\.InnerText$", RegexOptions.Compiled);

		// - frmMetatype.cs:287 allows Force up to 100, SpiritControl twice the MAG or RES
		// - 0 is what every caller passes for a critter without Force
		public const int MaximumForce = 100;

		private static readonly Regex Call = new Regex(@"ExpressionToString\(([^,]+),", RegexOptions.Compiled);

		// - the first argument as written, whitespace collapsed
		// - public so the detection tests can drive it with hand-built source
		public static IEnumerable<string> ArgumentsIn(SourceLines source)
		{
			return source.Code
				.SelectMany(line => Call.Matches(line).Cast<Match>())
				.Select(match => Regex.Replace(match.Groups[1].Value.Trim(), @"\s+", " "))
				.Where(argument => !argument.StartsWith("string ", StringComparison.Ordinal));
		}

		public static IReadOnlyList<string> ApplicationArguments()
		{
			return SourceCorpus.Files
				.SelectMany(file => ArgumentsIn(file.Source))
				.Distinct(StringComparer.Ordinal)
				.OrderBy(argument => argument, StringComparer.Ordinal)
				.ToArray();
		}

		// - XPaths below a metatype, none for an argument nothing maps
		public static IEnumerable<string> XPathsFor(string argument)
		{
			Match field = MetatypeField.Match(argument);
			if (field.Success)
				return new[] { field.Groups[1].Value };

			string[] paths;
			return LoopArguments.TryGetValue(argument, out paths) ? paths : Enumerable.Empty<string>();
		}

		public static IEnumerable<string> DataFiles()
		{
			return DataPaths.FilesWithPacks("metatypes.xml", "critters.xml");
		}

		public sealed class Failure
		{
			public Failure(string key, int force, string error)
			{
				Key = key;
				Force = force;
				Error = error;
			}

			// - "file: metatype path = value", the allowlist key
			public string Key { get; }

			public int Force { get; }

			public string Error { get; }
		}

		// - one failure per value: the lowest Force that breaks it names the defect
		// - memoized per text: thousands of values share a few dozen forms
		public static IEnumerable<Failure> FailuresIn(string fileName, XmlDocument document, IEnumerable<string> xpaths)
		{
			string[] paths = xpaths.ToArray();
			Dictionary<string, Tuple<int, string>> verdicts = new Dictionary<string, Tuple<int, string>>(StringComparer.Ordinal);

			foreach (XmlNode metatype in document.SelectNodes("/chummer/metatypes/metatype").Cast<XmlNode>())
			{
				foreach (XmlNode value in paths.SelectMany(path => metatype.SelectNodes(path).Cast<XmlNode>()))
				{
					Tuple<int, string> verdict;
					if (!verdicts.TryGetValue(value.InnerText, out verdict))
						verdicts[value.InnerText] = verdict = FirstFailure(value.InnerText);

					if (verdict != null)
					{
						yield return new Failure(fileName + ": " + RuleCorpus.ItemLabel(metatype) + " " + Describe(value)
							+ " = " + value.InnerText, verdict.Item1, verdict.Item2);
					}
				}
			}
		}

		private static Tuple<int, string> FirstFailure(string value)
		{
			for (int force = 0; force <= MaximumForce; force++)
			{
				string error = ErrorOf(value, force);
				if (error != null)
					return Tuple.Create(force, error);
			}

			return null;
		}

		// - an attribute names its element's text too: two complex forms share "@rating"
		private static string Describe(XmlNode value)
		{
			XmlAttribute attribute = value as XmlAttribute;
			if (attribute == null)
				return value.Name;

			return attribute.OwnerElement.ParentNode.Name + "/" + attribute.OwnerElement.Name
				+ "[" + attribute.OwnerElement.InnerText + "]/@" + attribute.Name;
		}

		// - null when the value evaluates cleanly at this Force
		public static string ErrorOf(string value, int force)
		{
			try
			{
				ExpressionToString(value, force, 0);
				return null;
			}
			catch (FallsBackToOne fallback)
			{
				return "silently 1 (" + fallback.InnerException.GetType().Name + ")";
			}
			catch (Exception exception)
			{
				return exception.GetType().Name;
			}
		}

		public sealed class FallsBackToOne : Exception
		{
			public FallsBackToOne(Exception swallowed)
				: base("the application swallows this and uses 1", swallowed)
			{
			}
		}

		// - SpiritControl.cs:831, throwing FallsBackToOne where the original swallows the error
		// - public so the detection tests can drive it with hand-built values
		public static string ExpressionToString(string strIn, int intForce, int intOffset)
		{
			int intValue = 0;
			XmlDocument objXmlDocument = new XmlDocument();
			XPathNavigator nav = objXmlDocument.CreateNavigator();
			XPathExpression xprAttribute = nav.Compile(strIn.Replace("/", " div ").Replace("F", intForce.ToString())
				.Replace("1D6", intForce.ToString()).Replace("2D6", intForce.ToString()));
			try
			{
				intValue = Convert.ToInt32(nav.Evaluate(xprAttribute).ToString());
			}
			catch (Exception exception)
			{
				throw new FallsBackToOne(exception);
			}
			intValue += intOffset;
			if (intForce > 0)
			{
				if (intValue < 1)
					intValue = 1;
			}
			else
			{
				if (intValue < 0)
					intValue = 0;
			}
			return intValue.ToString();
		}
	}
}
