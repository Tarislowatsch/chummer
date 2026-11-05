using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace Chummer.Tests
{
	// - a Create passes these values straight to Convert: a non-number throws or turns into 0
	// - the field list is read off the source: only the diversions need judgement
	public static class NumericFields
	{
		public enum Conversion
		{
			Int32,
			Boolean,
			Double,
		}

		// One element, or an attribute on it, that a Create hands to Convert.
		public sealed class ConvertedField
		{
			public ConvertedField(string element, string attribute, Conversion conversion)
			{
				Element = element;
				Attribute = attribute;
				Conversion = conversion;
			}

			public string Element { get; }

			// - null for the element's own text
			public string Attribute { get; }

			public Conversion Conversion { get; }

			public string Path => Attribute == null ? Element : Element + "/@" + Attribute;
		}

		public sealed class UnconvertibleValue
		{
			public UnconvertibleValue(string itemName, ConvertedField field, string value)
			{
				ItemName = itemName;
				Field = field;
				Value = value;
			}

			public string ItemName { get; }

			public ConvertedField Field { get; }

			public string Value { get; }
		}

		public sealed class NumericFieldCheck
		{
			public NumericFieldCheck(string entity, string filePath, IReadOnlyList<ConvertedField> fields,
				IReadOnlyList<UnconvertibleValue> unconvertible)
			{
				Entity = entity;
				FilePath = filePath;
				Fields = fields;
				Unconvertible = unconvertible;
			}

			public string Entity { get; }

			public string FilePath { get; }

			public IReadOnlyList<ConvertedField> Fields { get; }

			public IReadOnlyList<UnconvertibleValue> Unconvertible { get; }
		}

		// - the same Create reached with a second file's node, as in the required-field rules
		private static readonly IReadOnlyDictionary<string, string> CreateClassOverrides =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "Bioware", "Cyberware" },
				{ "Echo", "Metamagic" },
			};

		// - values a Create diverts before converting, keyed "Class.path"
		private static readonly IReadOnlyDictionary<string, Func<string, bool>> DivertedValues =
			new Dictionary<string, Func<string, bool>>(StringComparer.Ordinal)
			{
				// clsUnique.cs:5589 and :6176 convert <maxrating> only when it is not ""
				{ "TechProgram.maxrating", value => value.Length == 0 },
				{ "TechProgramOption.maxrating", value => value.Length == 0 },
				// - clsEquipment.cs:976 hands "Variable(...)" to a parser of its own
				{ "Armor.cost", value => value.StartsWith("Variable", StringComparison.Ordinal) },
			};

		// - the only culture a Create passes, to Convert.ToDouble (clsEquipment.cs:4289)
		private static readonly CultureInfo ApplicationCulture = new CultureInfo("en-US");

		public static string CreateClassOf(string entity)
		{
			string className;
			return CreateClassOverrides.TryGetValue(entity, out className) ? className : entity;
		}

		// - class name -> the fields its Create converts, for every class that converts any
		public static IReadOnlyDictionary<string, IReadOnlyList<ConvertedField>> ConvertedFields =>
			CachedConvertedFields.Value;

		private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<ConvertedField>>>
			CachedConvertedFields =
				new Lazy<IReadOnlyDictionary<string, IReadOnlyList<ConvertedField>>>(ReadConvertedFields);

		private static IReadOnlyDictionary<string, IReadOnlyList<ConvertedField>> ReadConvertedFields()
		{
			Dictionary<string, IReadOnlyList<ConvertedField>> fields =
				new Dictionary<string, IReadOnlyList<ConvertedField>>(StringComparer.Ordinal);

			foreach (string path in DataPaths.ApplicationSourceFiles())
			{
				foreach (KeyValuePair<string, IReadOnlyList<ConvertedField>> entry
					in ConvertedFieldsIn(File.ReadAllLines(path)))
				{
					if (fields.ContainsKey(entry.Key))
					{
						throw new InvalidOperationException(
							entry.Key + " declares more than one Create(XmlNode ...), so its "
							+ "converted fields cannot be told apart.");
					}

					fields.Add(entry.Key, entry.Value);
				}
			}

			return fields;
		}

		// - public so the detection tests can drive it with hand-built source
		// - matched over the joined body: a call broken across lines still counts
		public static IReadOnlyDictionary<string, IReadOnlyList<ConvertedField>> ConvertedFieldsIn(
			string[] lines)
		{
			SourceLines source = SourceLines.Of(lines);
			string joined = string.Join("\n", source.Code);
			Dictionary<string, IReadOnlyList<ConvertedField>> fields =
				new Dictionary<string, IReadOnlyList<ConvertedField>>(StringComparer.Ordinal);

			foreach (Match signature in DataPaths.EntityCreateSignature.Matches(joined))
			{
				int line = 0;
				for (int i = 0; i < signature.Index; i++)
				{
					if (joined[i] == '\n')
						line++;
				}

				string className = DataPaths.ClassNameAbove(source.Code, line);
				string body = string.Join("\n", source.BlockFrom(line).Code);
				IReadOnlyList<ConvertedField> converted =
					ConvertedFieldsInBody(className, body, signature.Groups[1].Value);

				if (converted.Count > 0)
					fields.Add(className, converted);
			}

			return fields;
		}

		private static IReadOnlyList<ConvertedField> ConvertedFieldsInBody(string className, string body,
			string parameter)
		{
			string node = Regex.Escape(parameter) + @"\s*\[\s*""(\w+)""\s*\]";
			Regex understood = new Regex(@"\bConvert\.To(Int32|Boolean|Double)\(\s*" + node
				+ @"(?:\s*\.\s*Attributes\s*\[\s*""(\w+)""\s*\])?\s*\.\s*InnerText\b");
			Regex anyConversion = new Regex(@"(?:\bConvert\.To\w+|\.(?:Try)?Parse)\(\s*" + node);

			MatchCollection matches = understood.Matches(body);

			// - a conversion of an unknown shape would drop out of the list without a word
			if (anyConversion.Matches(body).Count != matches.Count)
			{
				throw new InvalidOperationException(
					className + ".Create converts a node read in a shape this scan does not "
					+ "understand. Extend the pattern rather than lose that field from the check.");
			}

			return matches.Cast<Match>()
				.Select(match => new ConvertedField(match.Groups[2].Value,
					match.Groups[3].Success ? match.Groups[3].Value : null,
					(Conversion)Enum.Parse(typeof(Conversion), match.Groups[1].Value)))
				.GroupBy(field => field.Path + ":" + field.Conversion, StringComparer.Ordinal)
				.Select(group => group.First())
				.OrderBy(field => field.Path, StringComparer.Ordinal)
				.ToArray();
		}

		// - inside the single parse, like the required-field contracts it borrows its XPaths from
		public static IReadOnlyList<NumericFieldCheck> ChecksIn(string xmlPath, XmlDocument document)
		{
			string fileName = Path.GetFileName(xmlPath);
			List<NumericFieldCheck> checks = new List<NumericFieldCheck>();

			foreach (DataPaths.RequiredFieldRule rule in DataPaths.RequiredFieldRules
				.Where(rule => string.Equals(rule.FileName, fileName, StringComparison.Ordinal)))
			{
				string className = CreateClassOf(rule.Entity);
				IReadOnlyList<ConvertedField> fields;
				if (!ConvertedFields.TryGetValue(className, out fields))
					continue;

				checks.Add(new NumericFieldCheck(rule.Entity, xmlPath, fields,
					UnconvertibleValuesIn(document.SelectNodes(rule.ItemXPath).Cast<XmlNode>(),
						className, fields)));
			}

			return checks;
		}

		// - public so the detection tests can drive it with hand-built XML
		// - an absent field is skipped: presence is the required-field contract's job
		public static IReadOnlyList<UnconvertibleValue> UnconvertibleValuesIn(IEnumerable<XmlNode> items,
			string className, IReadOnlyList<ConvertedField> fields)
		{
			List<UnconvertibleValue> unconvertible = new List<UnconvertibleValue>();

			foreach (XmlNode item in items)
			{
				foreach (ConvertedField field in fields)
				{
					string value = ValueOf(item, field);
					if (value == null || IsDiverted(className, field, value)
						|| Converts(field.Conversion, value))
					{
						continue;
					}

					unconvertible.Add(new UnconvertibleValue(DataPaths.ItemLabel(item), field, value));
				}
			}

			return unconvertible;
		}

		// - XmlNode's indexer, the call Create makes: first direct child, exact ordinal match
		private static string ValueOf(XmlNode item, ConvertedField field)
		{
			XmlElement element = item[field.Element];
			if (element == null)
				return null;

			if (field.Attribute == null)
				return element.InnerText;

			return element.Attributes[field.Attribute]?.Value;
		}

		private static bool IsDiverted(string className, ConvertedField field, string value)
		{
			Func<string, bool> diverted;
			return DivertedValues.TryGetValue(className + "." + field.Path, out diverted) && diverted(value);
		}

		// - accepts exactly what the Convert overload accepts, whitespace and sign included
		// - Convert.ToInt32(string) parses with the current culture
		public static bool Converts(Conversion conversion, string value)
		{
			switch (conversion)
			{
				case Conversion.Int32:
					int integer;
					return int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out integer);
				case Conversion.Boolean:
					bool flag;
					return bool.TryParse(value, out flag);
				case Conversion.Double:
					double number;
					return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands,
						ApplicationCulture, out number);
				default:
					throw new ArgumentOutOfRangeException(nameof(conversion), conversion, null);
			}
		}

		// - a diversion naming a field no Create converts any more would exempt nothing, unseen
		public static IEnumerable<string> StaleDivertedValueKeys()
		{
			HashSet<string> converted = new HashSet<string>(
				ConvertedFields.SelectMany(entry => entry.Value.Select(field => entry.Key + "." + field.Path)),
				StringComparer.Ordinal);

			return DivertedValues.Keys
				.Where(key => !converted.Contains(key))
				.OrderBy(key => key, StringComparer.Ordinal);
		}

		// - a converting class no rule maps to would have its values go unchecked
		public static IEnumerable<string> ClassesWithoutAnEntity()
		{
			HashSet<string> reached = new HashSet<string>(
				DataPaths.RequiredFieldRules.Select(rule => CreateClassOf(rule.Entity)),
				StringComparer.Ordinal);

			return ConvertedFields.Keys
				.Where(className => !reached.Contains(className))
				.OrderBy(className => className, StringComparer.Ordinal);
		}
	}
}
