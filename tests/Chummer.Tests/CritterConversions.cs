using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace Chummer.Tests
{
	// - fields the critter loaders convert directly, outside ExpressionToString
	// - a non-number or a missing unguarded attribute throws into the UI
	public static class CritterConversions
	{
		public enum Form
		{
			Whole,

			// - the one character before "D6", read only where the text contains "D6"
			DigitBeforeD6,
		}

		// One direct conversion in a loader, with the data it reads relative to the metatype.
		public sealed class Conversion
		{
			public Conversion(string site, string path, Form form, bool guarded)
			{
				Site = site;
				Path = path;
				Form = form;
				Guarded = guarded;
			}

			public string Site { get; }

			public string File => Site.Substring(0, Site.LastIndexOf(':'));

			// - e.g. "skills/knowledge/@rating" or "essmax"
			public string Path { get; }

			public Form Form { get; }

			// - checked for null first: a missing attribute is skipped, not converted
			public bool Guarded { get; }

			public string Description => Path + (Form == Form.DigitBeforeD6 ? " (digit before D6)" : "")
				+ (Guarded ? " (if present)" : "");
		}

		// - node variable -> its path below the metatype, for the names the loaders use
		public static readonly IReadOnlyDictionary<string, string> Parents =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "objXmlCritter", "" },
				{ "objXmlMetatype", "" },
				{ "objXmlMetavariant", "metavariants/metavariant" },
			};

		// - the files that load a critter are the ones carrying an ExpressionToString copy
		public static IEnumerable<string> LoaderFiles()
		{
			return CritterExpressions.Calculators.Select(calculator => calculator.Substring(0, calculator.IndexOf('.')) + ".cs");
		}

		public static IReadOnlyList<Conversion> ApplicationConversions => CachedConversions.Value;

		private static readonly Lazy<IReadOnlyList<Conversion>> CachedConversions =
			new Lazy<IReadOnlyList<Conversion>>(() => LoaderFiles()
				.SelectMany(file => ConversionsIn(file, File.ReadAllLines(DataPaths.ApplicationSourceFiles()
					.Single(path => string.Equals(System.IO.Path.GetFileName(path), file, StringComparison.Ordinal)))))
				.ToArray());

		private static readonly Regex Start = new Regex(@"\bConvert\.To\w+\(|\b\w+\.(?:Try)?Parse\(", RegexOptions.Compiled);

		private static readonly Regex Loop = new Regex(
			@"\bforeach\s*\(\s*XmlNode\s+(\w+)\s+in\s+(\w+)\.SelectNodes\(\s*""([^""]+)""\s*\)\s*\)", RegexOptions.Compiled);

		// - as in NumericFields: an indexer, .Attributes or .InnerText marks a node operand
		// - an ExpressionToString argument is CritterExpressions' business
		private static readonly Regex NodeRead = new Regex(@"^\w+(?:\[""|\.Attributes\b|\.InnerText\b)", RegexOptions.Compiled);

		private static readonly Regex AttributeRead =new Regex(@"^(\w+)\.Attributes\[""(\w+)""\]\.InnerText$", RegexOptions.Compiled);

		private static readonly Regex FieldRead = new Regex(
			@"^(\w+)\[""(\w+)""\]\.InnerText(\.Substring\(\s*\w+\s*,\s*1\s*\))?$", RegexOptions.Compiled);

		// - public so the detection tests can drive it with hand-built source
		// - a node read in a shape this scan does not know throws instead of dropping out
		public static IReadOnlyList<Conversion> ConversionsIn(string fileName, string[] lines)
		{
			string[] code = SourceLines.Of(lines).Code;
			Dictionary<string, string> bound = new Dictionary<string, string>(StringComparer.Ordinal);
			List<Conversion> conversions = new List<Conversion>();

			for (int i = 0; i < code.Length; i++)
			{
				foreach (Match loop in Loop.Matches(code[i]))
				{
					if (Parents.TryGetValue(loop.Groups[2].Value, out string parent))
						bound[loop.Groups[1].Value] = Join(parent, loop.Groups[3].Value);
				}

				foreach (Match start in Start.Matches(code[i]))
				{
					string site = fileName + ":" + (i + 1);
					string rest = string.Join(" ", code.Skip(i).Take(MaxCallLines)).Substring(start.Index + start.Length);
					string argument = ArgumentAt(rest);
					if (argument == null)
						throw Unknown(site, code[i].Substring(start.Index));

					argument = Regex.Replace(argument, @"\s+", "");
					if (!NodeRead.IsMatch(argument))
						continue;

					conversions.Add(Understand(site, argument, bound, code, i));
				}
			}

			return conversions;
		}

		private static Conversion Understand(string site, string argument, IReadOnlyDictionary<string, string> bound,
			string[] code, int line)
		{
			Match attribute = AttributeRead.Match(argument);
			if (attribute.Success && bound.TryGetValue(attribute.Groups[1].Value, out string element))
			{
				string guard = Regex.Escape(attribute.Groups[1].Value) + @"\.Attributes\[""" + attribute.Groups[2].Value
					+ @"""\]\s*!=\s*null";
				bool guarded = Regex.IsMatch(code[line], guard) || (line > 0 && Regex.IsMatch(code[line - 1], guard));
				return new Conversion(site, element + "/@" + attribute.Groups[2].Value, Form.Whole, guarded);
			}

			Match field = FieldRead.Match(argument);
			if (field.Success)
			{
				string variable = field.Groups[1].Value;
				string holder;
				if (Parents.TryGetValue(variable, out holder) || bound.TryGetValue(variable, out holder))
				{
					return new Conversion(site, Join(holder, field.Groups[2].Value),
						field.Groups[3].Success ? Form.DigitBeforeD6 : Form.Whole, false);
				}
			}

			throw Unknown(site, argument);
		}

		private static InvalidOperationException Unknown(string site, string text)
		{
			return new InvalidOperationException(site + " converts a node read this scan does not understand: "
				+ text + ". Extend the scan rather than lose that field from the check.");
		}

		// - a call broken across lines is joined, up to this many
		private const int MaxCallLines = 5;

		// - the text up to the matching parenthesis, null when it never closes
		private static string ArgumentAt(string text)
		{
			int depth = 0;
			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] == '(')
					depth++;
				else if (text[i] == ')' && depth-- == 0)
					return text.Substring(0, i);
			}

			return null;
		}

		private static string Join(string parent, string child)
		{
			return parent.Length == 0 ? child : parent + "/" + child;
		}

		// - a missing required attribute comes back with a null value
		public static IEnumerable<(XmlNode Metatype, XmlElement Holder, string Value)> ValuesIn(XmlDocument document,
			string path, Form form, bool required)
		{
			int at = path.IndexOf("/@", StringComparison.Ordinal);
			string elementPath = at < 0 ? path : path.Substring(0, at);
			string attribute = at < 0 ? null : path.Substring(at + 2);

			foreach (XmlNode metatype in document.SelectNodes("/chummer/metatypes/metatype").Cast<XmlNode>())
			{
				foreach (XmlElement holder in metatype.SelectNodes(elementPath).OfType<XmlElement>())
				{
					string value = attribute == null ? holder.InnerText : holder.GetAttributeNode(attribute)?.Value;
					if (value == null && !required)
						continue;
					if (form == Form.DigitBeforeD6 && value != null && value.IndexOf("D6", StringComparison.Ordinal) < 0)
						continue;

					yield return (metatype, holder, value);
				}
			}
		}

		// - null when the loader converts the value cleanly
		public static string ErrorOf(string value, Form form)
		{
			if (value == null)
				return "missing: NullReferenceException";

			if (form == Form.DigitBeforeD6)
			{
				int position = value.IndexOf("D6", StringComparison.Ordinal) - 1;
				if (position < 0)
					return "nothing before D6: ArgumentOutOfRangeException";
				value = value.Substring(position, 1);
			}

			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? null : "FormatException";
		}

		// - "file: metatype path = value: error", over every distinct path the loaders convert
		public static IEnumerable<string> FailuresIn(string fileName, XmlDocument document, IEnumerable<Conversion> conversions)
		{
			foreach (var group in conversions.GroupBy(conversion => (conversion.Path, conversion.Form)))
			{
				bool required = group.Any(conversion => !conversion.Guarded);
				foreach (var found in ValuesIn(document, group.Key.Path, group.Key.Form, required))
				{
					string error = ErrorOf(found.Value, group.Key.Form);
					if (error != null)
					{
						yield return fileName + ": " + RuleCorpus.ItemLabel(found.Metatype) + " " + group.Key.Path
							+ " = " + (found.Value ?? "(none)") + ": " + error;
					}
				}
			}
		}
	}
}
