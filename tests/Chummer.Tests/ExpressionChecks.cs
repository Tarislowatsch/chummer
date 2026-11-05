using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

namespace Chummer.Tests
{
	// - avail and cost are tiny programs: each calculator substitutes, then runs them as XPath
	// - the ports in AvailabilityExpressions/CostExpressions replay one calculator each
	// - a port throws exactly where the application would
	public static class ExpressionChecks
	{
		// - one calculator replayed against one entry at one rating, throwing as the code would
		public delegate void Replay(XmlNode item, int rating);

		public sealed class ExpressionRule
		{
			public ExpressionRule(string entity, string field, Replay replay)
			{
				Entity = entity;
				Field = field;
				Replay = replay;
			}

			public string Entity { get; }

			public string Field { get; }

			public Replay Replay { get; }

			// - the theory's case id, unique per rule
			public string Id => Entity + "." + Field;
		}

		public sealed class ExpressionFailure
		{
			public ExpressionFailure(string itemName, int rating, string value, string error)
			{
				ItemName = itemName;
				Rating = rating;
				Value = value;
				Error = error;
			}

			public string ItemName { get; }

			public int Rating { get; }

			public string Value { get; }

			public string Error { get; }

			// - the allowlist key: one entry's value, independent of the rating that broke it
			public string Key => ItemName + " = " + Value;
		}

		public sealed class ExpressionCheck
		{
			public ExpressionCheck(ExpressionRule rule, string filePath, int itemCount,
				IReadOnlyList<ExpressionFailure> failures)
			{
				Rule = rule;
				FilePath = filePath;
				ItemCount = itemCount;
				Failures = failures;
			}

			public ExpressionRule Rule { get; }

			public string FilePath { get; }

			public int ItemCount { get; }

			public IReadOnlyList<ExpressionFailure> Failures { get; }
		}

		public static IEnumerable<ExpressionRule> AllRules => AvailabilityExpressions.Rules.Concat(CostExpressions.Rules);

		// - stand-in for runtime state the data cannot supply: parent cost, Body, Speed, Accel
		// - a round integer keeps a multiplier like ".1" from inventing a fractional crash
		public const int RuntimeStandIn = 1000;

		// - the culture every calculator passes to Convert.ToDouble
		public static readonly CultureInfo ApplicationCulture = new CultureInfo("en-US");

		// - XmlNode field -> the rating range the selection dialogs offer for that class
		private static readonly IReadOnlyDictionary<string, string[]> RatingFields =
			new Dictionary<string, string[]>(StringComparer.Ordinal)
			{
				{ "ArmorMod", new[] { "maxrating", null } },
				{ "Cyberware", new[] { "rating", "minrating" } },
				{ "Bioware", new[] { "rating", "minrating" } },
				{ "Gear", new[] { "rating", "minrating" } },
				{ "Commlink", new[] { "rating", "minrating" } },
				{ "OperatingSystem", new[] { "rating", "minrating" } },
				{ "VehicleMod", new[] { "rating", null } },
				{ "WeaponMod", new[] { "rating", null } },
			};

		// - the ends of the range plus Gear's avail3/6/10 and cost3/6/10 switch points
		// - every value between behaves like its nearest candidate in these grammars
		public static IReadOnlyList<int> RatingsOf(string entity, XmlNode item)
		{
			string[] fields;
			if (!RatingFields.TryGetValue(entity, out fields))
				return new[] { 0 };

			string maxText = item[fields[0]]?.InnerText;
			if (maxText == null)
				return new[] { 0 };

			// - vehicle mods with rating "qty" are bought by count, rating stays at its default
			int max;
			if (!int.TryParse(maxText, NumberStyles.Integer, CultureInfo.InvariantCulture, out max))
				return new[] { 1 };

			if (max <= 0)
				return new[] { 0 };

			int min = 1;
			string minText = fields[1] == null ? null : item[fields[1]]?.InnerText;
			int declaredMin;
			if (minText != null && int.TryParse(minText, NumberStyles.Integer, CultureInfo.InvariantCulture,
				out declaredMin) && declaredMin > min)
			{
				min = declaredMin;
			}

			return new[] { min, 3, 4, 6, 7, max }
				.Where(rating => rating >= min && rating <= max)
				.Distinct()
				.OrderBy(rating => rating)
				.ToArray();
		}

		private static readonly Regex AnyClass =
			new Regex(@"^\s*public\s+(?:(?:sealed|partial|abstract)\s+)*class\s+(\w+)", RegexOptions.Compiled);

		// - "Class.Member" -> hash of the member's comment-free body, for every key asked for
		// - a port is only as good as its last reading: a changed hash means read it again
		// - a key that matches nothing maps to "(missing)" and fails the pin
		public static IReadOnlyDictionary<string, string> CalculatorFingerprints(IEnumerable<string> keys)
		{
			Dictionary<string, string> fingerprints = keys.Distinct(StringComparer.Ordinal)
				.ToDictionary(key => key, key => "(missing)", StringComparer.Ordinal);

			foreach (string path in DataPaths.ApplicationSourceFiles())
			{
				SourceLines source = SourceLines.Of(File.ReadAllLines(path));
				string className = null;

				for (int i = 0; i < source.Code.Length; i++)
				{
					Match declaration = AnyClass.Match(source.Code[i]);
					if (declaration.Success)
					{
						className = declaration.Groups[1].Value;
						continue;
					}

					Match member = MemberDeclaration.Match(source.Code[i]);
					if (className == null || !member.Success)
						continue;

					string key = className + "." + member.Groups[1].Value;
					if (!fingerprints.ContainsKey(key))
						continue;

					if (fingerprints[key] != "(missing)")
						throw new InvalidOperationException(key + " is declared twice, so its port is ambiguous.");

					fingerprints[key] = DataPaths.Fingerprint(source.BlockFrom(i).Code
						.Select(line => Regex.Replace(line.Trim(), @"\s+", " "))
						.Where(line => line.Length > 0));
				}
			}

			return fingerprints;
		}

		private static readonly Regex MemberDeclaration = new Regex(
			@"^\s*(?:public|private|protected|internal)\s+(?:(?:static|virtual|override)\s+)*[\w<>\[\]]+\s+(\w+)\s*(?:\(|$)",
			RegexOptions.Compiled);

		// - the same two calls every calculator makes on a throwaway document
		public static object Evaluate(string expression)
		{
			XPathNavigator nav = new XmlDocument().CreateNavigator();
			return nav.Evaluate(nav.Compile(expression));
		}

		// - Evaluate(...).ToString() as the code writes it, pinned to the en-US run
		public static string EvaluateToString(string expression)
		{
			return Convert.ToString(Evaluate(expression), ApplicationCulture);
		}

		// - what the field reads in the Create: verbatim text, "" when the element is absent
		public static string FieldOf(XmlNode item, string field)
		{
			return item[field]?.InnerText ?? string.Empty;
		}

		// - public so the detection tests can drive it with hand-built XML
		public static IReadOnlyList<ExpressionFailure> FailuresIn(ExpressionRule rule, IEnumerable<XmlNode> items)
		{
			List<ExpressionFailure> failures = new List<ExpressionFailure>();

			foreach (XmlNode item in items)
			{
				foreach (int rating in RatingsOf(rule.Entity, item))
				{
					try
					{
						rule.Replay(item, rating);
					}
					catch (Exception exception)
					{
						failures.Add(new ExpressionFailure(DataPaths.ItemLabel(item), rating,
							FieldOf(item, rule.Field), exception.GetType().Name));

						// - one failing rating per entry is enough to name the defect
						break;
					}
				}
			}

			return failures;
		}

		// - inside the single parse, scoped by the required-field rules' XPaths
		public static IReadOnlyList<ExpressionCheck> ChecksIn(string xmlPath, XmlDocument document,
			IEnumerable<ExpressionRule> rules)
		{
			string fileName = System.IO.Path.GetFileName(xmlPath);
			Dictionary<string, DataPaths.RequiredFieldRule> entities = DataPaths.RequiredFieldRules
				.ToDictionary(rule => rule.Entity, StringComparer.Ordinal);
			List<ExpressionCheck> checks = new List<ExpressionCheck>();

			foreach (ExpressionRule rule in rules)
			{
				DataPaths.RequiredFieldRule entity = entities[rule.Entity];
				if (!string.Equals(entity.FileName, fileName, StringComparison.Ordinal))
					continue;

				XmlNode[] items = document.SelectNodes(entity.ItemXPath).Cast<XmlNode>().ToArray();
				checks.Add(new ExpressionCheck(rule, xmlPath, items.Length, FailuresIn(rule, items)));
			}

			return checks;
		}
	}
}
