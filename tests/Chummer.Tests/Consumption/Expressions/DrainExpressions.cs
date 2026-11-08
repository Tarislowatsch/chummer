using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Chummer.Tests.Kernel;
using Chummer.Tests.Localization;
using Chummer.Tests.Rulebook;

namespace Chummer.Tests.Consumption.Expressions
{
	// - a drain is XPath once its consumer has swapped each attribute code for a value
	// - PrintToStream evaluates it without a try: a code left in throws on print
	// - the forms evaluate it inside a try: a failure silently leaves the shown value stale
	public static class DrainExpressions
	{
		public sealed class Consumer
		{
			public Consumer(string site, string dataFile)
			{
				Site = site;
				DataFile = dataFile;
			}

			// - file:line of the chain's first Replace
			public string Site { get; }

			public string DataFile { get; }
		}

		// - traditions feed the MAG chains, streams the RES chains
		// - the forms read the label filled from <drain>, not the node itself
		public static readonly IReadOnlyList<Consumer> Consumers = new[]
		{
			new Consumer("clsCharacter.cs:1650", "traditions.xml"),
			new Consumer("clsCharacter.cs:1676", "streams.xml"),
			new Consumer("frmCareer.cs:992", "traditions.xml"),
			new Consumer("frmCareer.cs:1021", "streams.xml"),
			new Consumer("frmCareer.cs:19112", "traditions.xml"),
			new Consumer("frmCareer.cs:21968", "traditions.xml"),
			new Consumer("frmCareer.cs:22005", "streams.xml"),
			new Consumer("frmCreate.cs:13679", "traditions.xml"),
			new Consumer("frmCreate.cs:16487", "traditions.xml"),
			new Consumer("frmCreate.cs:16513", "streams.xml"),
		};

		// - every other chain wide enough to look like a drain consumer
		public static readonly IReadOnlyDictionary<string, string> OtherChains =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "clsImprovement.cs:1065", "ValueToInt bonus values" },
				{ "frmCareer.cs:19125", "drain tooltip text" },
				{ "frmCareer.cs:19664", "localizes the drain label" },
				{ "frmCareer.cs:19701", "localizes the fading label" },
				{ "frmCareer.cs:22019", "fading tooltip text" },
				{ "frmCreate.cs:13692", "drain tooltip text" },
				{ "frmCreate.cs:13979", "localizes the drain label" },
				{ "frmCreate.cs:16527", "fading tooltip text" },
				{ "frmSelectQuality.cs:664", "attributetotal requirements" },
				{ "frmSelectQuality.cs:969", "attributetotal requirements" },
			};

		// - fewer codes is a single-purpose formula: weapon damage STR, spell range MAG
		public const int MinimumChainCodes = 5;

		public sealed class Chain
		{
			public Chain(string site, IReadOnlyList<string> codes)
			{
				Site = site;
				Codes = codes;
			}

			public string Site { get; }

			// - in the order the consumer substitutes them
			public IReadOnlyList<string> Codes { get; }
		}

		private static readonly Regex Substitution = new Regex(
			@"\.Replace\(\s*(?:""([A-Z]{3})""|LanguageManager\.Instance\.GetString\(\s*""(String_Attribute[A-Z]{3}Short)""\s*\))\s*,",
			RegexOptions.Compiled);

		// - consecutive lines with at least one attribute Replace form one chain
		// - GetString resolves through en-us.xml, the language the tests replay
		// - public so the detection tests can drive it with hand-built source
		public static IEnumerable<Chain> ChainsIn(string fileName, string[] lines,
			IReadOnlyDictionary<string, string> english)
		{
			string[] code = SourceLines.Of(lines).Code;
			List<string> codes = new List<string>();
			int start = -1;

			for (int i = 0; i <= code.Length; i++)
			{
				MatchCollection matches = i < code.Length ? Substitution.Matches(code[i]) : null;
				if (matches != null && matches.Count > 0)
				{
					if (start < 0)
						start = i;

					codes.AddRange(matches.Cast<Match>().Select(match => match.Groups[1].Success
						? match.Groups[1].Value
						: english[match.Groups[2].Value]));
					continue;
				}

				if (start >= 0)
					yield return new Chain(fileName + ":" + (start + 1), codes.ToArray());

				start = -1;
				codes.Clear();
			}
		}

		public static IReadOnlyDictionary<string, string> EnglishStrings()
		{
			return UiStringKeys.LoadEnglish().SelectNodes("/chummer/strings/string").Cast<XmlNode>()
				.ToDictionary(entry => entry["key"].InnerText, entry => entry["text"].InnerText, StringComparer.Ordinal);
		}

		public static IReadOnlyList<Chain> ApplicationChains()
		{
			IReadOnlyDictionary<string, string> english = EnglishStrings();

			return DataPaths.ApplicationSourceFiles()
				.SelectMany(path => ChainsIn(Path.GetFileName(path), File.ReadAllLines(path), english))
				.ToArray();
		}

		// - "file:line" of every ["drain"] read, the source of both the node and the label route
		public static IEnumerable<string> DrainReadSites()
		{
			foreach (string path in DataPaths.ApplicationSourceFiles())
			{
				string[] code = SourceLines.Of(File.ReadAllLines(path)).Code;
				for (int i = 0; i < code.Length; i++)
				{
					if (code[i].Contains("[\"drain\"]"))
						yield return Path.GetFileName(path) + ":" + (i + 1);
				}
			}
		}

		// - the merged view: a pack's traditions join the base file's
		public static IEnumerable<string> DrainValuesFor(string dataFile)
		{
			IEnumerable<string> files = new[] { Path.Combine(DataPaths.ChummerDataDir, dataFile) }
				.Concat(CustomContent.PackFiles
					.Where(pack => pack.BaseFileNames.Contains(dataFile, StringComparer.Ordinal))
					.Select(pack => pack.FilePath));

			return files
				.SelectMany(path => Xml.Load(path).SelectNodes("/chummer/traditions/tradition/drain")
					.Cast<XmlNode>())
				.Select(drain => drain.InnerText)
				.Distinct(StringComparer.Ordinal);
		}

		private static readonly Regex LeftoverCode = new Regex(@"[A-Z]{2,}", RegexOptions.Compiled);

		// - distinct values, shifted per round: a division may only break on some attributes
		// - integral, not just finite: the forms parse the result as Int32 text
		// - null when the drain passes, otherwise why it fails
		public static string FailureOf(string drain, IReadOnlyList<string> codes)
		{
			for (int shift = 1; shift <= 6; shift++)
			{
				string expression = drain;
				for (int k = 0; k < codes.Count; k++)
					expression = expression.Replace(codes[k], (shift + k).ToString(CultureInfo.InvariantCulture));

				Match leftover = LeftoverCode.Match(expression);
				if (leftover.Success)
					return "leaves " + leftover.Value + " unreplaced";

				object result;
				try
				{
					result = ExpressionChecks.Evaluate(expression);
				}
				catch (Exception exception)
				{
					return exception.GetType().Name + " on '" + expression + "'";
				}

				if (!(result is double))
					return "'" + expression + "' is no number";

				double number = (double)result;
				if (double.IsNaN(number) || double.IsInfinity(number) || number != Math.Floor(number))
					return "'" + expression + "' = " + number.ToString(CultureInfo.InvariantCulture);
			}

			return null;
		}
	}
}
