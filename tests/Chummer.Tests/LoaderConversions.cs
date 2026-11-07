using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace Chummer.Tests
{
	// - fields a critter, vessel or quality loader converts directly, outside ExpressionToString
	// - a non-number or a missing unguarded attribute throws into the UI
	public static class LoaderConversions
	{
		public enum Form
		{
			Whole,

			// - the one character before "D6", read only where the text contains "D6"
			DigitBeforeD6,
		}

		// One loader's data: base files, their packs, the item a root variable stands for.
		public sealed class Corpus
		{
			public Corpus(string name, string[] baseFiles, string itemXPath)
			{
				Name = name;
				BaseFiles = baseFiles;
				ItemXPath = itemXPath;
			}

			public string Name { get; }

			public IReadOnlyList<string> BaseFiles { get; }

			public string ItemXPath { get; }

			public IEnumerable<string> Files()
			{
				return BaseFiles.Select(file => System.IO.Path.Combine(DataPaths.ChummerDataDir, file))
					.Concat(CustomContent.PackFiles
						.Where(pack => pack.BaseFileNames.Intersect(BaseFiles, StringComparer.Ordinal).Any())
						.Select(pack => pack.FilePath));
			}
		}

		public static readonly Corpus Critters =
			new Corpus("critters", new[] { "metatypes.xml", "critters.xml" }, "/chummer/metatypes/metatype");

		public static readonly Corpus Vessels = new Corpus("vessels", new[] { "vessels.xml" }, "/chummer/metatypes/metatype");

		public static readonly Corpus Qualities = new Corpus("qualities", new[] { "qualities.xml" }, "/chummer/qualities/quality");

		public static IReadOnlyList<Corpus> Corpora => new[] { Critters, Vessels, Qualities };

		// A root variable: an item of its corpus, or a node at a fixed path below one.
		public sealed class Root
		{
			public Root(Corpus corpus, string prefix, string loopPath = null)
			{
				Corpus = corpus;
				Prefix = prefix;
				LoopPath = loopPath;
			}

			public Corpus Corpus { get; }

			public string Prefix { get; }

			// - the SelectNodes path of the loop that bound it, null for a root itself
			public string LoopPath { get; }
		}

		// Code that loads such an item: a whole file, or one method of a larger form.
		public sealed class Scope
		{
			public Scope(string file, string method, IReadOnlyDictionary<string, Root> roots)
			{
				File = file;
				Method = method;
				Roots = roots;
			}

			public string File { get; }

			// - null for the whole file
			public string Method { get; }

			public IReadOnlyDictionary<string, Root> Roots { get; }
		}

		public static readonly IReadOnlyDictionary<string, Root> CritterRoots =
			new Dictionary<string, Root>(StringComparer.Ordinal)
			{
				{ "objXmlCritter", new Root(Critters, "") },
				{ "objXmlMetatype", new Root(Critters, "") },
				{ "objXmlMetavariant", new Root(Critters, "metavariants/metavariant") },
			};

		private static readonly IReadOnlyDictionary<string, Root> QualityRoots =
			new Dictionary<string, Root>(StringComparer.Ordinal) { { "objXmlQuality", new Root(Qualities, "") } };

		// - read off the code by hand: the loop guard finds a missing loader only by its loops
		// - one reading only root fields, like objSelected["bodmin"], stays invisible
		public static readonly IReadOnlyList<Scope> Scopes = new[]
		{
			new Scope("SpiritControl.cs", null, CritterRoots),
			new Scope("frmMetatype.cs", null, CritterRoots),
			new Scope("frmTest.cs", null, CritterRoots),
			new Scope("frmCareer.cs", "mnuSpecialPossessInanimate_Click",
				new Dictionary<string, Root>(StringComparer.Ordinal) { { "objSelected", new Root(Vessels, "") } }),
			new Scope("frmCareer.cs", "cmdAddQuality_Click", QualityRoots),
			new Scope("frmCreate.cs", "cmdAddQuality_Click", QualityRoots),
		};

		// One direct conversion, with the data it reads relative to an item of its corpus.
		public sealed class Conversion
		{
			public Conversion(string site, Corpus corpus, string path, Form form, bool guarded, string loopPath = null)
			{
				Site = site;
				Corpus = corpus;
				Path = path;
				Form = form;
				Guarded = guarded;
				LoopPath = loopPath;
			}

			public string LoopPath { get; }

			public string Site { get; }

			public string File => Site.Substring(0, Site.LastIndexOf(':'));

			public Corpus Corpus { get; }

			// - e.g. "skills/knowledge/@rating" or "essmax"
			public string Path { get; }

			public Form Form { get; }

			// - checked for null first: a missing attribute is skipped, not converted
			public bool Guarded { get; }

			// - what the data side checks: one key per corpus, path and form
			public string DataKey => Corpus.Name + ": " + Path + (Form == Form.DigitBeforeD6 ? " (digit before D6)" : "");

			public string Description => DataKey + (Guarded ? " (if present)" : "");
		}

		public static IReadOnlyList<Conversion> ApplicationConversions => CachedConversions.Value;

		private static readonly Lazy<IReadOnlyList<Conversion>> CachedConversions =
			new Lazy<IReadOnlyList<Conversion>>(() => Scopes
				.SelectMany(scope => ConversionsIn(scope, SourceOf(scope.File)))
				.ToArray());

		private static string[] SourceOf(string fileName)
		{
			return File.ReadAllLines(DataPaths.ApplicationSourceFiles()
				.Single(path => string.Equals(System.IO.Path.GetFileName(path), fileName, StringComparison.Ordinal)));
		}

		private static readonly Regex Start = new Regex(@"\b(Convert\.To\w+|\w+\.(?:Try)?Parse)\(", RegexOptions.Compiled);

		// - ErrorOf checks as int: ToBoolean or ToByte would throw on values that pass here
		private static readonly string[] IntegerConversions = { "Convert.ToInt32", "int.Parse", "Int32.Parse" };

		// - any rebinding drops the old binding: a stale one would resolve a foreign read
		// - foreach, assignment or declaration, out or ref argument, is pattern
		private static readonly Regex Rebinding = new Regex(
			@"\bforeach\s*\(\s*[\w.<>\[\]]+\s+(?<v>\w+)\s+in\b|(?<![\w.])(?<v>\w+)\s*=(?!=)"
			+ @"|\b(?:out|ref)\s+(?:[\w.<>\[\]]+\s+)?(?<v>\w+)\b|\bis\s+[\w.<>\[\]]+\s+(?<v>\w+)\b", RegexOptions.Compiled);

		private static readonly Regex Loop = new Regex(
			@"\bforeach\s*\(\s*XmlNode\s+(\w+)\s+in\s+(\w+)\.SelectNodes\(\s*""([^""]+)""\s*\)\s*\)", RegexOptions.Compiled);

		// - as in NumericFields: an indexer, .Attributes or .InnerText marks a node operand
		// - an ExpressionToString argument is CritterExpressions' business
		private static readonly Regex NodeRead = new Regex(@"^(\w+)(?:\[""|\.Attributes\b|\.InnerText\b)", RegexOptions.Compiled);

		private static readonly Regex AttributeRead = new Regex(@"^(\w+)\.Attributes\[""(\w+)""\]\.InnerText$", RegexOptions.Compiled);

		private static readonly Regex FieldRead = new Regex(
			@"^(\w+)\[""(\w+)""\]\.InnerText(?:\.Substring\((\w+),1\))?$", RegexOptions.Compiled);

		// - the scope's lines and the index of its first line in the file
		private static (string[] Code, int Offset) Extent(Scope scope, string[] lines)
		{
			SourceLines source = SourceLines.Of(lines);
			if (scope.Method == null)
				return (source.Code, 0);

			Regex declaration = new Regex(@"^\s*(?:(?:private|public|protected|internal|static|override|async)\s+)+[\w<>\[\],\s]+\s"
				+ Regex.Escape(scope.Method) + @"\s*\(");
			int start = Array.FindIndex(source.Code, line => declaration.IsMatch(line));
			if (start < 0)
				throw new InvalidOperationException(scope.File + " has no method " + scope.Method + ": update Scopes.");

			return (source.BlockFrom(start).Code, start);
		}

		// - public so the detection tests can drive it with hand-built source
		// - a node read in a shape this scan does not know throws instead of dropping out
		public static IReadOnlyList<Conversion> ConversionsIn(Scope scope, string[] lines)
		{
			(string[] code, int offset) = Extent(scope, lines);
			Dictionary<string, Root> bound = new Dictionary<string, Root>(StringComparer.Ordinal);
			List<Conversion> conversions = new List<Conversion>();

			for (int i = 0; i < code.Length; i++)
			{
				foreach (Match rebinding in Rebinding.Matches(code[i]))
					bound.Remove(rebinding.Groups["v"].Value);

				foreach (Match loop in Loop.Matches(code[i]))
				{
					Root parent;
					if (scope.Roots.TryGetValue(loop.Groups[2].Value, out parent) || bound.TryGetValue(loop.Groups[2].Value, out parent))
					{
						bound[loop.Groups[1].Value] =
							new Root(parent.Corpus, Join(parent.Prefix, loop.Groups[3].Value), loop.Groups[3].Value);
					}
				}

				foreach (Match start in Start.Matches(code[i]))
				{
					string site = scope.File + ":" + (offset + i + 1);
					string rest = string.Join(" ", code.Skip(i).Take(MaxCallLines)).Substring(start.Index + start.Length);
					string argument = ArgumentAt(rest);
					if (argument == null)
						throw Unknown(site, code[i].Substring(start.Index));

					argument = Regex.Replace(argument, @"\s+", "");
					if (!NodeRead.IsMatch(argument))
						continue;
					if (!IntegerConversions.Contains(start.Groups[1].Value, StringComparer.Ordinal))
						throw Unknown(site, start.Groups[1].Value + "(" + argument + ")");

					conversions.Add(Understand(site, argument, scope.Roots, bound, code, i, start.Index));
				}
			}

			return conversions;
		}

		private static Conversion Understand(string site, string argument, IReadOnlyDictionary<string, Root> roots,
			IReadOnlyDictionary<string, Root> bound, string[] code, int line, int column)
		{
			Root holder;
			Match attribute = AttributeRead.Match(argument);
			if (attribute.Success && bound.TryGetValue(attribute.Groups[1].Value, out holder))
			{
				// - a null check ending in another statement guards nothing
				string guard = @"\bif\s*\(\s*" + Regex.Escape(attribute.Groups[1].Value) + @"\.Attributes\[""" + attribute.Groups[2].Value
					+ @"""\]\s*!=\s*null\s*\)";
				bool guarded = Regex.Matches(code[line], guard).Cast<Match>().Any(check => GuardsColumn(check, code[line], column))
					|| (line > 0 && Regex.IsMatch(code[line - 1], "^\\s*" + guard + "\\s*$"));
				return new Conversion(site, holder.Corpus, Join(holder.Prefix, "@" + attribute.Groups[2].Value), Form.Whole, guarded,
					holder.LoopPath);
			}

			Match field = FieldRead.Match(argument);
			if (field.Success && (roots.TryGetValue(field.Groups[1].Value, out holder) || bound.TryGetValue(field.Groups[1].Value, out holder)))
			{
				if (!field.Groups[3].Success)
					return new Conversion(site, holder.Corpus, Join(holder.Prefix, field.Groups[2].Value), Form.Whole, false,
						holder.LoopPath);

				// - the index must be the one frmMetatype computes, not any Substring(x, 1)
				Regex index = new Regex(@"\b" + Regex.Escape(field.Groups[3].Value) + @"\s*=\s*" + Regex.Escape(field.Groups[1].Value)
					+ @"\[""" + field.Groups[2].Value + @"""\]\.InnerText\.IndexOf\(\s*""D6""\s*\)\s*-\s*1\s*;");
				if (code.Take(line).Any(earlier => index.IsMatch(earlier)))
					return new Conversion(site, holder.Corpus, Join(holder.Prefix, field.Groups[2].Value), Form.DigitBeforeD6, false,
						holder.LoopPath);
			}

			throw Unknown(site, argument);
		}

		// - the check ends before the conversion, and no statement or block closes in between
		private static bool GuardsColumn(Match check, string line, int column)
		{
			int end = check.Index + check.Length;
			return end <= column && line.Substring(end, column - end).IndexOfAny(new[] { ';', '}' }) < 0;
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

		// - "file:line path": a loop outside every scope whose body converts a read of its variable
		// - a new loader of the same data shows up here instead of hiding
		public static IEnumerable<string> UnscannedLoops(string fileName, string[] lines, IReadOnlyCollection<string> paths,
			IEnumerable<(int First, int Last)> scanned)
		{
			SourceLines source = SourceLines.Of(lines);
			(int First, int Last)[] ranges = scanned.ToArray();

			for (int i = 0; i < source.Code.Length; i++)
			{
				foreach (Match loop in Loop.Matches(source.Code[i]))
				{
					if (!paths.Contains(loop.Groups[3].Value) || ranges.Any(range => i >= range.First && i <= range.Last))
						continue;

					string variable = loop.Groups[1].Value;
					string[] body = source.BlockFrom(i).Code;
					bool converts = body.Select((line, at) => (line, at)).Any(entry => Start.Matches(entry.line).Cast<Match>()
						.Select(start => ArgumentAt(string.Join(" ", body.Skip(entry.at).Take(MaxCallLines))
							.Substring(start.Index + start.Length)))
						.Where(argument => argument != null)
						.Select(argument => NodeRead.Match(Regex.Replace(argument, @"\s+", "")))
						.Any(read => read.Success && read.Groups[1].Value == variable));
					if (converts)
						yield return fileName + ":" + (i + 1) + " " + loop.Groups[3].Value;
				}
			}
		}

		public static IReadOnlyList<string> ApplicationUnscannedLoops()
		{
			// - paths converted directly only: the rest goes through ExpressionToString
			string[] paths = ApplicationConversions.Select(conversion => conversion.LoopPath).Where(path => path != null)
				.Distinct(StringComparer.Ordinal).ToArray();

			return DataPaths.ApplicationSourceFiles()
				.SelectMany(path =>
				{
					string fileName = System.IO.Path.GetFileName(path);
					string[] lines = File.ReadAllLines(path);
					IEnumerable<(int, int)> scanned = Scopes
						.Where(scope => string.Equals(scope.File, fileName, StringComparison.Ordinal))
						.Select(scope => RangeOf(scope, lines));
					return UnscannedLoops(fileName, lines, paths, scanned);
				})
				.ToArray();
		}

		private static (int First, int Last) RangeOf(Scope scope, string[] lines)
		{
			(string[] code, int offset) = Extent(scope, lines);
			return (offset, offset + code.Length - 1);
		}

		// - a missing required attribute comes back with a null value
		public static IEnumerable<(XmlNode Item, string Value)> ValuesIn(XmlDocument document, string itemXPath,
			string path, Form form, bool required)
		{
			int at = path.IndexOf("@", StringComparison.Ordinal);
			string elementPath = at < 0 ? path : at == 0 ? "." : path.Substring(0, at - 1);
			string attribute = at < 0 ? null : path.Substring(at + 1);

			foreach (XmlNode item in document.SelectNodes(itemXPath).Cast<XmlNode>())
			{
				foreach (XmlElement holder in item.SelectNodes(elementPath).OfType<XmlElement>())
				{
					string value = attribute == null ? holder.InnerText : holder.GetAttributeNode(attribute)?.Value;
					if (value == null && !required)
						continue;
					if (form == Form.DigitBeforeD6 && value != null && value.IndexOf("D6", StringComparison.Ordinal) < 0)
						continue;

					yield return (item, value);
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

		// - "file: item path = value: error", over every distinct path converted for this corpus
		public static IEnumerable<string> FailuresIn(string fileName, XmlDocument document, Corpus corpus,
			IEnumerable<Conversion> conversions)
		{
			foreach (var group in conversions.Where(conversion => conversion.Corpus == corpus)
				.GroupBy(conversion => (conversion.Path, conversion.Form)))
			{
				bool required = group.Any(conversion => !conversion.Guarded);
				foreach (var found in ValuesIn(document, corpus.ItemXPath, group.Key.Path, group.Key.Form, required))
				{
					string error = ErrorOf(found.Value, group.Key.Form);
					if (error != null)
					{
						yield return fileName + ": " + RuleCorpus.ItemLabel(found.Item) + " " + group.Key.Path
							+ " = " + (found.Value ?? "(none)") + ": " + error;
					}
				}
			}
		}
	}
}
