using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

namespace Chummer.Tests
{
	// - CreateImprovements turns bonus values into numbers through ValueToInt, outside any try
	// - each replay mirrors one handler branch: its guards, its reads, its conversions
	public static class BonusValues
	{
		public static readonly IReadOnlyList<string> Calculators = new[]
		{
			"ImprovementManager.ValueToInt",
			"ImprovementManager.NodeExists",
		};

		// - ValueToInt prints the XPath result in the thread culture: the app never sets one
		// - the parse back is en-US: under de-DE "1,5" reads as 15
		// - GetCultureInfo ignores this machine's overrides of its own culture
		public static readonly CultureInfo LocalCulture = CultureInfo.GetCultureInfo("de-DE");

		// - stand-ins for the character's TotalValue: no bonus value names an attribute today
		// - 0 is a mundane's MAG, an odd value exposes a fraction
		public static readonly IReadOnlyList<int> AttributeStandIns = new[] { 0, 1, 2, 3 };

		// - the order ValueToInt replaces them in
		private static readonly string[] AttributeCodes =
			{ "BOD", "AGI", "REA", "STR", "CHA", "INT", "LOG", "WIL", "EDG", "MAG", "RES" };

		// - branches whose only ValueToInt argument is nodBonus["handler"].InnerText
		public static readonly IReadOnlyList<string> SimpleHandlers = new[]
		{
			"adeptpowerpoints", "armorencumbrancepenalty", "basicbiowareessmultiplier", "basiclifestylecost",
			"biowareessmultiplier", "complexformlimit", "composure", "concealability", "cyberwareessmultiplier",
			"damageresistance", "drainresist", "essencemax", "fadingresist", "flypercent", "flyspeed",
			"freenegativequalities", "freepositivequalities", "freespiritpowerpoints", "genetechcostmultiplier",
			"initiation", "initiative", "initiativepass", "initiativepassadd", "judgeintentions", "liftandcarry",
			"lifestylecost", "matrixinitiative", "matrixinitiativepass", "matrixinitiativepassadd", "memory",
			"movementpercent", "notoriety", "nuyenamt", "nuyenmaxbp", "reach", "restricteditemcount", "skillwire",
			"spelllimit", "submersion", "swimpercent", "throwrange", "throwstr", "transgenicsgenetechcost",
			"unarmedap", "unarmeddv",
		};

		// - hand ports of the branches that read more than one plain value
		// - a value is only replayed past the guards that let the code reach it
		public static readonly IReadOnlyDictionary<string, Action<XmlNode, Reader>> PortedBranches =
			new Dictionary<string, Action<XmlNode, Reader>>(StringComparer.Ordinal)
			{
				{ "selectskill", (bonus, reader) =>
					{
						XmlNode block = bonus.SelectSingleNode("selectskill");
						if (block.InnerXml.Contains("val"))
							reader.Value("selectskill/val", () => bonus["selectskill"]["val"].InnerText);
						if (block.InnerXml.Contains("max"))
							reader.Value("selectskill/max", () => bonus["selectskill"]["max"].InnerText);
					}
				},
				{ "selectskillgroup", (bonus, reader) =>
					reader.Value("selectskillgroup/bonus", () => bonus["selectskillgroup"]["bonus"].InnerText)
				},
				{ "selectattribute", (bonus, reader) =>
					{
						foreach (string tag in new[] { "min", "val", "max", "aug" })
						{
							if (bonus["selectattribute"].InnerXml.Contains(tag))
								reader.Value("selectattribute/" + tag, () => bonus["selectattribute"][tag].InnerXml);
						}
					}
				},
				{ "specificattribute", SpecificAttribute },
				{ "conditionmonitor", (bonus, reader) =>
					{
						XmlNode block = bonus.SelectSingleNode("conditionmonitor");
						foreach (string tag in new[] { "physical", "stun" })
						{
							if (block.InnerXml.Contains(tag))
								reader.Value("conditionmonitor/" + tag, () => block[tag].InnerText);
						}

						foreach (string tag in new[] { "threshold", "thresholdoffset" })
						{
							if (NodeExists(block, tag))
								reader.Value("conditionmonitor/" + tag, () => block[tag].InnerText);
						}

						if (block.InnerXml.Contains("overflow"))
							reader.Value("conditionmonitor/overflow", () => block["overflow"].InnerText);
					}
				},
				{ "livingpersona", (bonus, reader) =>
					{
						XmlNode block = bonus.SelectSingleNode("livingpersona");
						foreach (string tag in new[] { "response", "signal", "firewall", "system", "biofeedback" })
						{
							if (block.InnerXml.Contains(tag))
								reader.Value("livingpersona/" + tag, () => block[tag].InnerText);
						}
					}
				},
				{ "specificskill", (bonus, reader) =>
					{
						foreach (XmlNode skill in bonus.SelectNodes("specificskill"))
						{
							if (skill["bonus"] != null)
								reader.Value("specificskill/bonus", () => skill["bonus"].InnerXml);
							if (skill["max"] != null)
								reader.Value("specificskill/max", () => skill["max"].InnerText);
						}
					}
				},
				{ "skillcategory", (bonus, reader) => EveryBlock(bonus, reader, "skillcategory", "bonus", true) },
				{ "skillgroup", (bonus, reader) => EveryBlock(bonus, reader, "skillgroup", "bonus", true) },
				{ "skillattribute", (bonus, reader) => EveryBlock(bonus, reader, "skillattribute", "bonus", true) },
				{ "weaponcategorydv", (bonus, reader) => EveryBlock(bonus, reader, "weaponcategorydv", "bonus", true) },
				{ "spellcategory", (bonus, reader) => EveryBlock(bonus, reader, "spellcategory", "val", false) },
				{ "skillarticulation", (bonus, reader) =>
					{
						XmlNode block = bonus.SelectSingleNode("skillarticulation");
						reader.Value("skillarticulation/bonus", () => block["bonus"].InnerText);
					}
				},
				{ "armor", (bonus, reader) =>
					{
						XmlNode block = bonus.SelectSingleNode("armor");
						foreach (string tag in new[] { "b", "i" })
						{
							if (NodeExists(block, tag))
								reader.Value("armor/" + tag, () => block[tag].InnerText);
						}
					}
				},
			};

		// - clsImprovement.cs:1568, ESS and "-natural" bypass ValueToInt with a plain Convert.ToInt32
		private static void SpecificAttribute(XmlNode bonus, Reader reader)
		{
			foreach (XmlNode attribute in bonus.SelectNodes("specificattribute"))
			{
				if (reader.Decide("specificattribute/name", () => attribute["name"].InnerText) == "ESS")
				{
					reader.Integer("specificattribute/val (ESS)", () => attribute["val"].InnerText);
					continue;
				}

				if (attribute.InnerXml.Contains("min"))
					reader.Value("specificattribute/min", () => attribute["min"].InnerXml);
				if (attribute.InnerXml.Contains("val"))
					reader.Value("specificattribute/val", () => attribute["val"].InnerXml);
				if (attribute.InnerXml.Contains("max"))
				{
					if (reader.Decide("specificattribute/max", () => attribute["max"].InnerText.Contains("-natural")))
					{
						reader.Integer("specificattribute/max (-natural)",
							() => attribute["max"].InnerText.Replace("-natural", string.Empty));
					}
					else
					{
						reader.Value("specificattribute/max", () => attribute["max"].InnerXml);
					}
				}

				if (attribute.InnerXml.Contains("aug"))
					reader.Value("specificattribute/aug", () => attribute["aug"].InnerXml);
			}
		}

		// - SelectNodes reaches every block, the indexer only the first child of that name
		private static void EveryBlock(XmlNode bonus, Reader reader, string handler, string tag, bool innerXml)
		{
			foreach (XmlNode block in bonus.SelectNodes(handler))
			{
				reader.Value(handler + "/" + tag, innerXml
					? (Func<string>)(() => block[tag].InnerXml)
					: () => block[tag].InnerText);
			}
		}

		public static Action<XmlNode, Reader> ReplayOf(string handler)
		{
			if (PortedBranches.TryGetValue(handler, out Action<XmlNode, Reader> replay))
				return replay;
			if (SimpleHandlers.Contains(handler))
				return (bonus, reader) => reader.Value(handler, () => bonus[handler].InnerText);
			return null;
		}

		public static IEnumerable<string> ReplayedHandlers =>
			PortedBranches.Keys.Concat(SimpleHandlers).OrderBy(handler => handler, StringComparer.Ordinal);

		// clsImprovement.cs:1106
		public static bool NodeExists(XmlNode node, string name)
		{
			try
			{
				return node.SelectSingleNode(name) != null;
			}
			catch
			{
				return false;
			}
		}

		// - clsImprovement.cs:1060, with the thread culture made explicit
		// - attribute stands in for every TotalValue the code substitutes
		public static int ValueToInt(string value, int rating, int attribute, CultureInfo threadCulture)
		{
			if (value.Contains("Rating") || AttributeCodes.Any(value.Contains))
			{
				foreach (string code in AttributeCodes)
					value = value.Replace(code, attribute.ToString(threadCulture));

				XPathNavigator nav = new XmlDocument().CreateNavigator();
				XPathExpression expression = nav.Compile(value.Replace("Rating", rating.ToString(threadCulture)));
				decimal number = Convert.ToDecimal(Convert.ToString(nav.Evaluate(expression), threadCulture),
					ExpressionChecks.ApplicationCulture);
				return Convert.ToInt32(Math.Floor(number));
			}

			if (value.Contains("FixedValues"))
			{
				string[] values = value.Replace("FixedValues(", string.Empty).Replace(")", string.Empty).Split(',');
				return Convert.ToInt32(values[rating - 1], threadCulture);
			}

			return Convert.ToInt32(value, threadCulture);
		}

		// - en-US converts, de-DE gives another number or throws: the defect is in ValueToInt
		public sealed class CultureDependent : Exception
		{
			public CultureDependent(int english, string local)
				: base("en-US gives " + english + ", de-DE " + local)
			{
			}
		}

		// - a failure at one read, labelled with its path below the bonus root
		public sealed class ReadFailed : Exception
		{
			public ReadFailed(string path, string value, Exception inner)
				: base(path + " = " + (value ?? "(missing)") + ": " + inner.Message, inner)
			{
				Path = path;
				Value = value;
			}

			public string Path { get; }

			// - null when the read itself threw, e.g. a guard passed on a missing element
			public string Value { get; }
		}

		// - one replay of one branch at one rating
		// - a failing value is recorded and the replay goes on: a known one must not hide the next
		public sealed class Reader
		{
			private readonly List<(string Path, string Value)> _reached = new List<(string Path, string Value)>();

			private readonly List<ReadFailed> _failures = new List<ReadFailed>();

			public Reader(int rating)
			{
				Rating = rating;
			}

			public int Rating { get; }

			public IReadOnlyList<(string Path, string Value)> Reached => _reached;

			public IReadOnlyList<ReadFailed> Failures => _failures;

			public void Value(string path, Func<string> read)
			{
				Record(path, read, (value, attribute, culture) => ValueToInt(value, Rating, attribute, culture));
			}

			// - a direct Convert.ToInt32 in the same branch, also in the thread culture
			public void Integer(string path, Func<string> read)
			{
				Record(path, read, (value, attribute, culture) => System.Convert.ToInt32(value, culture));
			}

			private void Record(string path, Func<string> read, Func<string, int, CultureInfo, int> convert)
			{
				try
				{
					string value = Read(path, read);
					_reached.Add((path, value));
					ConvertEach(path, value, (attribute, culture) => convert(value, attribute, culture));
				}
				catch (ReadFailed failure)
				{
					_failures.Add(failure);
				}
			}

			// - a read that decides what ValueToInt gets, e.g. the attribute's name
			public T Decide<T>(string path, Func<T> read)
			{
				try
				{
					return read();
				}
				catch (Exception exception)
				{
					throw new ReadFailed(path, null, exception);
				}
			}

			private static string Read(string path, Func<string> read)
			{
				try
				{
					return read();
				}
				catch (Exception exception)
				{
					throw new ReadFailed(path, null, exception);
				}
			}

			private static void ConvertEach(string path, string value, Func<int, CultureInfo, int> convert)
			{
				IEnumerable<int> attributes = AttributeCodes.Any(value.Contains) ? AttributeStandIns : new[] { 0 };
				foreach (int attribute in attributes)
				{
					int english;
					try
					{
						english = convert(attribute, ExpressionChecks.ApplicationCulture);
					}
					catch (Exception exception)
					{
						throw new ReadFailed(path, value, exception);
					}

					// - "0,333…" reads as 333333333333333 and overflows: still the culture, not the data
					string local;
					try
					{
						int number = convert(attribute, LocalCulture);
						local = number == english ? null : "gives " + number;
					}
					catch (Exception exception)
					{
						local = "throws " + exception.GetType().Name;
					}

					if (local != null)
						throw new ReadFailed(path, value, new CultureDependent(english, local));
				}
			}
		}

		// - min..max of the owner's rating or levels, minrating taken as is (frmSelectGear.cs:1097)
		// - no number: 0 for unrated cyberware (frmSelectCyberware.cs:254), up to MAG for "levels yes"
		public static IReadOnlyList<int> RatingsOf(XmlNode root)
		{
			XmlNode owner = root.ParentNode;
			foreach (string field in new[] { "rating", "maxrating", "levels" })
			{
				string text = owner[field]?.InnerText;
				if (text == null)
					continue;

				if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int max))
					break;
				if (max <= 0)
					return new[] { 0 };

				int min = int.TryParse(owner["minrating"]?.InnerText, NumberStyles.Integer, CultureInfo.InvariantCulture,
					out int declared) ? Math.Max(0, declared) : 1;

				return Enumerable.Range(min, Math.Max(0, Math.Min(max, ExpressionChecks.ExhaustiveRatingLimit) - min + 1))
					.Concat(new[] { max })
					.Distinct()
					.ToArray();
			}

			return Enumerable.Range(0, ExpressionChecks.ExhaustiveRatingLimit + 1).ToArray();
		}

		public sealed class BonusValueFailure
		{
			public BonusValueFailure(string file, string itemName, string handler, int rating, ReadFailed failure)
			{
				File = file;
				ItemName = itemName;
				Handler = handler;
				Rating = rating;
				Path = failure.Path;
				Value = failure.Value;
				Error = failure.InnerException is CultureDependent
					? failure.InnerException.Message
					: failure.InnerException.GetType().Name;
			}

			public string File { get; }

			public string ItemName { get; }

			public string Handler { get; }

			public int Rating { get; }

			public string Path { get; }

			public string Value { get; }

			public string Error { get; }

			// - the allowlist key: independent of the rating that broke it
			public string Key => File + " | " + ItemName + " | " + Path + " = " + (Value ?? "(missing)");
		}

		public sealed class BonusValueCheck
		{
			public BonusValueCheck(IReadOnlyList<BonusValueFailure> failures,
				IReadOnlyList<(string File, string Path, string Value)> reached, IReadOnlyList<string> cutShort)
			{
				Failures = failures;
				Reached = reached;
				CutShort = cutShort;
			}

			public IReadOnlyList<BonusValueFailure> Failures { get; }

			// - every value a replay converted at its first rating, for the coverage pins
			public IReadOnlyList<(string File, string Path, string Value)> Reached { get; }

			// - "file: item (rating)" for owners rated above the limit, replayed at the top end only
			public IReadOnlyList<string> CutShort { get; }
		}

		// - public so the detection tests can drive it with hand-built XML
		// - one failure per value, at its first failing rating
		public static BonusValueCheck CheckIn(string file, string baseFile, XmlDocument document)
		{
			List<BonusValueFailure> failures = new List<BonusValueFailure>();
			List<(string, string, string)> reached = new List<(string, string, string)>();
			List<string> cutShort = new List<string>();

			foreach (XmlNode root in BonusVocabulary.CreateImprovementsRoots(baseFile, document))
			{
				IReadOnlyList<int> ratings = RatingsOf(root);
				string itemName = DataPaths.ItemLabel(root.ParentNode);
				if (ratings[ratings.Count - 1] > ExpressionChecks.ExhaustiveRatingLimit)
					cutShort.Add(file + ": " + itemName + " (" + ratings[ratings.Count - 1] + ")");

				foreach (string handler in ReplayedHandlers.Where(handler => NodeExists(root, handler)))
				{
					Action<XmlNode, Reader> replay = ReplayOf(handler);
					HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);
					for (int i = 0; i < ratings.Count; i++)
					{
						Reader reader = new Reader(ratings[i]);
						List<ReadFailed> found = new List<ReadFailed>();

						// - only a Decide read still throws: what follows it depends on its result
						try
						{
							replay(root, reader);
						}
						catch (ReadFailed failure)
						{
							found.Add(failure);
						}

						found.InsertRange(0, reader.Failures);
						foreach (BonusValueFailure failure in found
							.Select(each => new BonusValueFailure(file, itemName, handler, ratings[i], each))
							.Where(failure => reported.Add(failure.Key)))
						{
							failures.Add(failure);
						}

						if (i == 0)
							reached.AddRange(reader.Reached.Select(value => (file, value.Path, value.Value)));
					}
				}
			}

			return new BonusValueCheck(failures, reached, cutShort.Distinct(StringComparer.Ordinal).ToArray());
		}

		// - XmlDocument is not safe under xUnit's parallel classes: only immutable records leave here
		public static BonusValueCheck Corpus => CachedCorpus.Value;

		private static readonly Lazy<BonusValueCheck> CachedCorpus = new Lazy<BonusValueCheck>(() =>
		{
			BonusValueCheck[] checks = FieldReaders.DataFiles.Select(file =>
			{
				XmlDocument document = new XmlDocument();
				document.Load(file.FilePath);
				return CheckIn(file.Label, file.BaseFileName, document);
			}).ToArray();

			return new BonusValueCheck(
				checks.SelectMany(check => check.Failures).ToArray(),
				checks.SelectMany(check => check.Reached).ToArray(),
				checks.SelectMany(check => check.CutShort).ToArray());
		});

		// One ValueToInt call in the source: the branch it sits in and its arguments as written.
		public sealed class Call
		{
			public Call(int line, string handler, string arguments)
			{
				Line = line;
				Handler = handler;
				Arguments = arguments;
			}

			// - 1-based
			public int Line { get; }

			// - "(no branch)" outside every NodeExists(<bonus>, "handler") block
			public string Handler { get; }

			// - both arguments: the replays take the rating as intRating
			public string Arguments { get; }

			public string Key => Handler + " | " + Arguments;
		}

		public const string NoBranch = "(no branch)";

		// - this. is the only qualified form a private method can take
		private static readonly Regex CallStart =
			new Regex(@"(?<![\w.])(?:this\s*\.\s*)?ValueToInt\s*\(", RegexOptions.Compiled);

		// - public so the detection tests can drive it with hand-built source
		// - the innermost branch wins: a block nested in another is the one that guards the call
		public static IReadOnlyList<Call> CallsIn(string[] lines, IEnumerable<string> handlers)
		{
			string[] code = SourceLines.Of(lines).Code;
			string[] masked = MaskLiterals(code);
			(string Handler, int Start, int Length)[] branches = handlers
				.Select(handler =>
				{
					(int start, SourceLines block, string unused) = BonusVocabulary.HandlerBlock(handler, lines);
					return (handler, start, block.Code.Length);
				})
				.ToArray();

			List<Call> calls = new List<Call>();
			for (int i = 0; i < code.Length; i++)
			{
				foreach (Match match in CallStart.Matches(masked[i]))
				{
					string argument = ArgumentsOf(code, i, match.Index + match.Length);
					if (argument.StartsWith("string ", StringComparison.Ordinal))
						continue;

					string handler = branches
						.Where(branch => i >= branch.Start && i < branch.Start + branch.Length)
						.OrderBy(branch => branch.Length)
						.Select(branch => branch.Handler)
						.FirstOrDefault() ?? NoBranch;
					calls.Add(new Call(i + 1, handler, argument));
				}
			}

			return calls;
		}

		// - literal contents blanked, columns kept: a "ValueToInt(" inside a string is no call
		// - Bare drops literals instead, so its columns do not match Code
		private static string[] MaskLiterals(string[] code)
		{
			string[] masked = new string[code.Length];
			bool verbatim = false;
			for (int i = 0; i < code.Length; i++)
			{
				char[] line = code[i].ToCharArray();
				char quote = verbatim ? '"' : '\0';
				for (int j = 0; j < line.Length; j++)
				{
					char current = line[j];
					if (quote == '\0')
					{
						if (current == '"' || current == '\'')
						{
							quote = current;
							verbatim = current == '"' && j > 0 && line[j - 1] == '@';
						}

						continue;
					}

					if (!verbatim && current == '\\' && j + 1 < line.Length)
					{
						line[j] = ' ';
						line[++j] = ' ';
					}
					else if (verbatim && current == '"' && j + 1 < line.Length && line[j + 1] == '"')
					{
						line[j] = ' ';
						line[++j] = ' ';
					}
					else if (current == quote)
					{
						quote = '\0';
						verbatim = false;
					}
					else
					{
						line[j] = ' ';
					}
				}

				masked[i] = new string(line);
			}

			return masked;
		}

		// - up to the closing parenthesis, across lines, whitespace collapsed
		private static string ArgumentsOf(string[] code, int line, int column)
		{
			StringBuilder argument = new StringBuilder();
			int depth = 0;
			char quote = '\0';
			for (int i = line; i < code.Length; i++, column = 0)
			{
				string text = code[i];
				for (int j = column; j < text.Length; j++)
				{
					char current = text[j];
					if (quote != '\0')
					{
						argument.Append(current);
						if (current == '\\' && j + 1 < text.Length)
							argument.Append(text[++j]);
						else if (current == quote)
							quote = '\0';
						continue;
					}

					if (current == '"' || current == '\'')
						quote = current;
					else if (current == '(' || current == '[')
						depth++;
					else if (current == ')' || current == ']')
						depth--;

					if (depth < 0)
						return Regex.Replace(argument.ToString().Trim(), @"\s+", " ");
					argument.Append(current);
				}

				argument.Append(' ');
			}

			throw new InvalidOperationException("Unterminated ValueToInt call at line " + (line + 1));
		}

		public static IReadOnlyList<Call> ApplicationCalls => CachedCalls.Value;

		private static readonly Lazy<IReadOnlyList<Call>> CachedCalls = new Lazy<IReadOnlyList<Call>>(() =>
		{
			string[] lines = File.ReadAllLines(DataPaths.ApplicationSourceFiles()
				.Single(path => Path.GetFileName(path) == FieldReaders.HandlerConsumerFile));
			return CallsIn(lines, BonusVocabulary.HandledTagsIn(lines));
		});

		// - "handler" -> hash of its branch, comment-free and whitespace-collapsed
		public static IReadOnlyDictionary<string, string> BranchFingerprints(IEnumerable<string> handlers)
		{
			string[] lines = File.ReadAllLines(DataPaths.ApplicationSourceFiles()
				.Single(path => Path.GetFileName(path) == FieldReaders.HandlerConsumerFile));
			return handlers.ToDictionary(handler => handler, handler => DataPaths.Fingerprint(
					BonusVocabulary.HandlerBlock(handler, lines).Block.Code
						.Select(line => Regex.Replace(line.Trim(), @"\s+", " "))
						.Where(line => line.Length > 0)),
				StringComparer.Ordinal);
		}
	}
}
