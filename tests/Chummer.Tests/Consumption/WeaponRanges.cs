using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;
using Chummer.Tests.Kernel;

namespace Chummer.Tests.Consumption
{
	// - Weapon.Range (clsEquipment.cs:6566) reads one band of one range row
	// - a missing band lands in the catch and shows as -1
	// - the band's formula runs after the try: a bad one throws into the UI
	public static class WeaponRanges
	{
		// - every band a caller asks for (clsEquipment.cs:6655 ff.)
		public static readonly IReadOnlyList<string> Bands = new[] { "min", "short", "medium", "long", "extreme" };

		public static XmlDocument Load()
		{
			XmlDocument document = new XmlDocument { XmlResolver = null };
			document.Load(Path.Combine(DataPaths.ChummerDataDir, "ranges.xml"));
			return document;
		}

		public static IEnumerable<XmlNode> Rows(XmlDocument ranges)
		{
			return ranges.SelectNodes("/chummer/ranges/range").Cast<XmlNode>();
		}

		public static string CategoryOf(XmlNode row)
		{
			return row["category"]?.InnerText ?? "(none)";
		}

		// - port of the evaluation: substitute, compile, evaluate, convert, floor
		// - throws exactly where the application would
		public static int Evaluate(string formula, int strength, int body)
		{
			string expression = formula
				.Replace("STR", strength.ToString(CultureInfo.InvariantCulture))
				.Replace("BOD", body.ToString(CultureInfo.InvariantCulture));

			XPathNavigator navigator = new XmlDocument().CreateNavigator();
			object result = navigator.Evaluate(navigator.Compile(expression));
			return Convert.ToInt32(Math.Floor(Convert.ToDouble(result.ToString(), CultureInfo.InvariantCulture)));
		}

		private static readonly Regex SetRangeLiteral = new Regex(@"\.SetRange\(""([^""\\]*)""\)");

		// - public so the detection tests can drive it with hand-built source
		public static IEnumerable<string> SetRangeLiteralsIn(string[] lines)
		{
			return SourceLines.Of(lines).Code
				.SelectMany(line => SetRangeLiteral.Matches(line).Cast<Match>())
				.Select(match => match.Groups[1].Value);
		}
	}
}
