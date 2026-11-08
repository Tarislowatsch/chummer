using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Chummer.Tests.Kernel;
using Chummer.Tests.Rulebook;

namespace Chummer.Tests.Consumption.Fields
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

		// One element or attribute, on the entry or a node nested in it, that a Create converts.
		public sealed class ConvertedField
		{
			public ConvertedField(string element, string attribute, Conversion conversion)
				: this(null, element, attribute, conversion)
			{
			}

			public ConvertedField(string scope, string element, string attribute, Conversion conversion)
			{
				Scope = scope;
				Element = element;
				Attribute = attribute;
				Conversion = conversion;
			}

			// - the SelectNodes path a Create loops over, null for the entry itself
			public string Scope { get; }

			// - null when the attribute sits on the scoped node itself
			public string Element { get; }

			public string Attribute { get; }

			public Conversion Conversion { get; }

			public string Path => string.Join("/", new[] { Scope, Element, Attribute == null ? null : "@" + Attribute }
				.Where(part => part != null));
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
			public NumericFieldCheck(string entity, string filePath, int itemCount,
				IReadOnlyList<ConvertedField> fields, IReadOnlyList<UnconvertibleValue> unconvertible)
			{
				Entity = entity;
				FilePath = filePath;
				ItemCount = itemCount;
				Fields = fields;
				Unconvertible = unconvertible;
			}

			public string Entity { get; }

			public string FilePath { get; }

			public int ItemCount { get; }

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

			foreach (Match signature in RequiredFields.EntityCreateSignature.Matches(joined))
			{
				int line = 0;
				for (int i = 0; i < signature.Index; i++)
				{
					if (joined[i] == '\n')
						line++;
				}

				string className = RequiredFields.ClassNameAbove(source.Code, line);
				string body = string.Join("\n", source.BlockFrom(line).Code);
				IReadOnlyList<ConvertedField> converted =
					ConvertedFieldsInBody(className, body, signature.Groups[1].Value);

				if (converted.Count > 0)
					fields.Add(className, converted);
			}

			return fields;
		}

		// - a string-keyed indexer, .Attributes or .InnerText marks the operand as an XmlNode
		// - a numeric indexer like strValues[0] is a string array, not a node read
		private static readonly Regex AnyNodeConversion = new Regex(
			@"(?:\bConvert\.To\w+|\.(?:Try)?Parse)\(\s*\w+\s*(?:\[\s*""|\.\s*Attributes\b|\.\s*InnerText\b)");

		private static IReadOnlyList<ConvertedField> ConvertedFieldsInBody(string className, string body,
			string parameter)
		{
			List<ConvertedField> fields = new List<ConvertedField>();
			int understood = 0;

			foreach (KeyValuePair<string, string> node in NodeVariablesIn(className, body, parameter))
			{
				Regex conversion = new Regex(@"\bConvert\.To(Int32|Boolean|Double)\(\s*" + Regex.Escape(node.Key)
					+ @"(?:\s*\[\s*""(\w+)""\s*\])?(?:\s*\.\s*Attributes\s*\[\s*""(\w+)""\s*\])?"
					+ @"\s*\.\s*InnerText\b");

				foreach (Match match in conversion.Matches(body))
				{
					understood++;
					fields.Add(new ConvertedField(node.Value,
						match.Groups[2].Success ? match.Groups[2].Value : null,
						match.Groups[3].Success ? match.Groups[3].Value : null,
						(Conversion)Enum.Parse(typeof(Conversion), match.Groups[1].Value)));
				}
			}

			// - a conversion of an unknown shape or node would drop out of the list without a word
			if (AnyNodeConversion.Matches(body).Count != understood)
			{
				throw new InvalidOperationException(
					className + ".Create converts a node read in a shape this scan does not "
					+ "understand. Extend the pattern rather than lose that field from the check.");
			}

			return fields
				.GroupBy(field => field.Path + ":" + field.Conversion, StringComparer.Ordinal)
				.Select(group => group.First())
				.OrderBy(field => field.Path, StringComparer.Ordinal)
				.ToArray();
		}

		// - variable name -> SelectNodes path, null for the parameter itself
		// - only loops directly over the parameter: a deeper loop surfaces as an unknown node
		private static IReadOnlyDictionary<string, string> NodeVariablesIn(string className, string body,
			string parameter)
		{
			string select = Regex.Escape(parameter) + @"\s*\.\s*SelectNodes\(\s*""([^""]+)""\s*\)";
			Dictionary<string, string> lists = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (Match list in new Regex(@"\bXmlNodeList\s+(\w+)\s*=\s*" + select).Matches(body))
				lists[list.Groups[1].Value] = list.Groups[2].Value;

			Dictionary<string, string> nodes = new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ parameter, null },
			};

			foreach (Match loop in new Regex(@"\bforeach\s*\(\s*XmlNode\s+(\w+)\s+in\s+(?:"
				+ select + @"|(\w+))\s*\)").Matches(body))
			{
				string path;
				if (loop.Groups[2].Success)
					path = loop.Groups[2].Value;
				else if (!lists.TryGetValue(loop.Groups[3].Value, out path))
					continue;

				string variable = loop.Groups[1].Value;
				string known;
				if (nodes.TryGetValue(variable, out known) && !string.Equals(known, path, StringComparison.Ordinal))
				{
					throw new InvalidOperationException(
						className + ".Create reuses '" + variable + "' for two node paths, so its "
						+ "conversions cannot be attributed to one of them.");
				}

				nodes[variable] = path;
			}

			return nodes;
		}

		public static IEnumerable<object[]> Entities()
		{
			return CachedChecksByEntity.Value.Keys
				.OrderBy(entity => entity, StringComparer.Ordinal)
				.Select(entity => new object[] { entity });
		}

		public static NumericFieldCheck CheckFor(string entity)
		{
			return CachedChecksByEntity.Value[entity];
		}

		private static readonly Lazy<IReadOnlyDictionary<string, NumericFieldCheck>>
			CachedChecksByEntity =
				new Lazy<IReadOnlyDictionary<string, NumericFieldCheck>>(
					() => RuleCorpus.Files.SelectMany(file => file.NumericFieldChecks)
						.ToDictionary(check => check.Entity, StringComparer.Ordinal));

		// - inside the single parse, like the required-field contracts it borrows its XPaths from
		public static IReadOnlyList<NumericFieldCheck> ChecksIn(string fileName, string xmlPath,
			XmlDocument document)
		{
			List<NumericFieldCheck> checks = new List<NumericFieldCheck>();

			foreach (RequiredFields.RequiredFieldRule rule in RequiredFields.Rules
				.Where(rule => string.Equals(rule.FileName, fileName, StringComparison.Ordinal)))
			{
				string className = CreateClassOf(rule.Entity);
				IReadOnlyList<ConvertedField> fields;
				if (!ConvertedFields.TryGetValue(className, out fields))
					continue;

				XmlNode[] items = document.SelectNodes(rule.ItemXPath).Cast<XmlNode>().ToArray();
				checks.Add(new NumericFieldCheck(rule.Entity, xmlPath, items.Length, fields,
					UnconvertibleValuesIn(items, className, fields)));
			}

			return checks;
		}

		public static IEnumerable<object[]> PackEntities()
		{
			return CustomContent.PairedPackFiles()
				.SelectMany(file => PackChecksIn(file.FilePath))
				.Select(check => new object[] { check.FilePath, check.Entity });
		}

		public static NumericFieldCheck PackCheckFor(string packPath, string entity)
		{
			return PackChecksIn(packPath)
				.Single(check => string.Equals(check.Entity, entity, StringComparison.Ordinal));
		}

		// - only rules that reach an entry, as for the required-field contracts
		private static IEnumerable<NumericFieldCheck> PackChecksIn(string packPath)
		{
			return ChecksIn(CustomContent.PackFileFor(packPath).BaseFileName, packPath, CustomContent.Load(packPath))
				.Where(check => check.ItemCount > 0);
		}

		// - public so the detection tests can drive it with hand-built XML
		// - absent is skipped: the Create guards that read or the required-field contract pins it
		public static IReadOnlyList<UnconvertibleValue> UnconvertibleValuesIn(IEnumerable<XmlNode> items,
			string className, IReadOnlyList<ConvertedField> fields)
		{
			List<UnconvertibleValue> unconvertible = new List<UnconvertibleValue>();

			foreach (XmlNode item in items)
			{
				foreach (ConvertedField field in fields)
				{
					foreach (string value in ValuesOf(item, field))
					{
						if (IsDiverted(className, field, value) || Converts(field.Conversion, value))
							continue;

						unconvertible.Add(new UnconvertibleValue(RuleCorpus.ItemLabel(item), field, value));
					}
				}
			}

			return unconvertible;
		}

		// - XmlNode's indexer, the call Create makes: first direct child, exact ordinal match
		private static IEnumerable<string> ValuesOf(XmlNode item, ConvertedField field)
		{
			IEnumerable<XmlNode> nodes = field.Scope == null
				? new[] { item }
				: item.SelectNodes(field.Scope).Cast<XmlNode>();

			foreach (XmlNode node in nodes)
			{
				XmlNode holder = field.Element == null ? node : node[field.Element];
				if (holder == null)
					continue;

				string value = field.Attribute == null
					? holder.InnerText
					: holder.Attributes?[field.Attribute]?.Value;

				if (value != null)
					yield return value;
			}
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
				RequiredFields.Rules.Select(rule => CreateClassOf(rule.Entity)),
				StringComparer.Ordinal);

			return ConvertedFields.Keys
				.Where(className => !reached.Contains(className))
				.OrderBy(className => className, StringComparer.Ordinal);
		}
	}
}
