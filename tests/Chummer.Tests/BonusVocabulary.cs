using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace Chummer.Tests
{
	// - CreateImprovements dispatches on tag names with no else: an unknown tag does nothing
	// - the item stays buyable and silently grants nothing
	public static class BonusVocabulary
	{
		public enum Consumer
		{
			CreateImprovements,
			VehicleMod,
			EnabledTab,
		}

		// - one tag name met in the data, with the code path that has to understand it
		public sealed class BonusTag
		{
			public BonusTag(Consumer consumer, string tag, string itemName)
			{
				Consumer = consumer;
				Tag = tag;
				ItemName = itemName;
			}

			public Consumer Consumer { get; }

			public string Tag { get; }

			public string ItemName { get; }
		}

		// - Vehicle sums these itself (clsEquipment.cs:15129-15829), bypassing CreateImprovements
		// - improvesensor and selecttext are read by the forms (frmCreate.cs:8916, frmCareer.cs:10223)
		// - hand-written: the reads are scattered over three files
		public static readonly IReadOnlyCollection<string> VehicleModTags =
			new HashSet<string>(StringComparer.Ordinal)
			{
				"accel",
				"armor",
				"body",
				"devicerating",
				"handling",
				"improvesensor",
				"selecttext",
				"speed",
			};

		// - only in vehicles.xml: armor.xml has the same path for its CreateImprovements mods
		private const string VehicleModFile = "vehicles.xml";

		private const string VehicleModBonusXPath = "/chummer/mods/mod/bonus";

		// - frmCreateImprovement.cs:291 writes <internal> as the tag under a fresh <bonus>
		private const string CustomImprovementFile = "improvements.xml";

		private const string CustomImprovementTagXPath = "/chummer/improvements/improvement/internal";

		// - not //bonus: specificskill and friends carry a <bonus> value field of their own
		public const string BonusRootXPath = "//bonus[not(ancestor::bonus)]";

		private static readonly string ImprovementManagerSource =
			Path.Combine(DataPaths.RepoRoot, "Chummer", "clsImprovement.cs");

		public static IReadOnlyCollection<string> CreateImprovementsTags => CachedCreateImprovementsTags.Value;

		public static IReadOnlyCollection<string> EnabledTabs => CachedEnabledTabs.Value;

		private static readonly Lazy<IReadOnlyCollection<string>> CachedCreateImprovementsTags =
			new Lazy<IReadOnlyCollection<string>>(
				() => HandledTagsIn(File.ReadAllLines(ImprovementManagerSource)));

		private static readonly Lazy<IReadOnlyCollection<string>> CachedEnabledTabs =
			new Lazy<IReadOnlyCollection<string>>(
				() => EnabledTabsIn(File.ReadAllLines(ImprovementManagerSource)));

		public static IReadOnlyCollection<string> VocabularyFor(Consumer consumer)
		{
			switch (consumer)
			{
				case Consumer.CreateImprovements:
					return CreateImprovementsTags;
				case Consumer.VehicleMod:
					return VehicleModTags;
				case Consumer.EnabledTab:
					return EnabledTabs;
				default:
					throw new ArgumentOutOfRangeException(nameof(consumer), consumer, null);
			}
		}

		// - public so the detection tests can pass a vocabulary of their own
		public static IEnumerable<BonusTag> UnhandledTags(IEnumerable<BonusTag> tags,
			Func<Consumer, IReadOnlyCollection<string>> vocabularyFor)
		{
			return tags.Where(tag => !vocabularyFor(tag.Consumer).Contains(tag.Tag));
		}

		// - public so the detection tests can drive it with hand-built XML
		// - direct children only: XmlNode lookups by tag name never look deeper
		public static IReadOnlyList<BonusTag> BonusTagsIn(string fileName, XmlDocument document)
		{
			HashSet<XmlNode> vehicleModRoots = VehicleModRoots(fileName, document);

			List<BonusTag> tags = new List<BonusTag>();

			foreach (XmlNode root in document.SelectNodes(BonusRootXPath))
			{
				Consumer consumer = vehicleModRoots.Contains(root)
					? Consumer.VehicleMod
					: Consumer.CreateImprovements;
				string itemName = DataPaths.ItemLabel(root.ParentNode);

				foreach (XmlElement child in root.ChildNodes.OfType<XmlElement>())
				{
					tags.Add(new BonusTag(consumer, child.Name, itemName));

					// - the switch at clsImprovement.cs:1219 has no default either
					if (consumer == Consumer.CreateImprovements && child.Name == "enabletab")
					{
						tags.AddRange(child.ChildNodes.OfType<XmlElement>()
							.Select(tab => new BonusTag(Consumer.EnabledTab, tab.InnerText, itemName)));
					}
				}
			}

			if (string.Equals(fileName, CustomImprovementFile, StringComparison.Ordinal))
			{
				tags.AddRange(document.SelectNodes(CustomImprovementTagXPath).Cast<XmlNode>()
					.Select(tag => new BonusTag(Consumer.CreateImprovements, tag.InnerText,
						DataPaths.ItemLabel(tag.ParentNode))));
			}

			return tags;
		}

		// - fileName is the base file: a pack shares its consumer
		public static IReadOnlyCollection<XmlNode> CreateImprovementsRoots(string fileName, XmlDocument document)
		{
			HashSet<XmlNode> vehicleModRoots = VehicleModRoots(fileName, document);
			return new HashSet<XmlNode>(
				document.SelectNodes(BonusRootXPath).Cast<XmlNode>().Where(root => !vehicleModRoots.Contains(root)));
		}

		private static HashSet<XmlNode> VehicleModRoots(string fileName, XmlDocument document)
		{
			return new HashSet<XmlNode>(
				string.Equals(fileName, VehicleModFile, StringComparison.Ordinal)
					? document.SelectNodes(VehicleModBonusXPath).Cast<XmlNode>()
					: Enumerable.Empty<XmlNode>());
		}

		// - the block behind NodeExists(<bonus>, "handler"), with its 0-based first line in the file
		internal static (int Start, SourceLines Block, string Parameter) HandlerBlock(string handler, string[] lines)
		{
			SourceLines body = CreateImprovementsBody(lines, out string parameter, out int bodyStart);
			Regex check = NodeExistsCheck(parameter, Regex.Escape(handler));
			int start = Array.FindIndex(body.Code, line => check.IsMatch(line));
			if (start < 0)
				throw new InvalidOperationException("CreateImprovements has no branch for \"" + handler + "\"");

			return (bodyStart + start, body.BlockFrom(start), parameter);
		}

		private static readonly Regex CreateImprovementsSignature = new Regex(
			@"\bbool\s+CreateImprovements\s*\([^)]*\bXmlNode\s+(\w+)", RegexOptions.Compiled);

		// - public so the detection tests can drive it with hand-built source
		// - matched over the joined body: a call broken across lines still counts
		public static IReadOnlyCollection<string> HandledTagsIn(string[] lines)
		{
			SourceLines body = CreateImprovementsBody(lines, out string parameter, out _);
			Regex check = NodeExistsCheck(parameter, @"([^""]+)");

			return new HashSet<string>(
				check.Matches(string.Join("\n", body.Code)).Cast<Match>().Select(match => match.Groups[1].Value),
				StringComparer.Ordinal);
		}

		// - only the case labels inside the enabletab branch, not other switches in the method
		public static IReadOnlyCollection<string> EnabledTabsIn(string[] lines)
		{
			SourceLines body = CreateImprovementsBody(lines, out string parameter, out _);
			Regex check = NodeExistsCheck(parameter, "enabletab");
			Regex label = new Regex(@"\bcase\s+""([^""]+)""\s*:");

			int start = Array.FindIndex(body.Code, line => check.IsMatch(line));
			if (start < 0)
				return new HashSet<string>(StringComparer.Ordinal);

			return new HashSet<string>(
				label.Matches(string.Join("\n", body.BlockFrom(start).Code)).Cast<Match>()
					.Select(match => match.Groups[1].Value),
				StringComparer.Ordinal);
		}

		private static Regex NodeExistsCheck(string parameter, string tagPattern)
		{
			return new Regex(@"\bNodeExists\(\s*" + Regex.Escape(parameter) + @"\s*,\s*""" + tagPattern + @"""\s*\)");
		}

		// - throws rather than returns empty: no method would make every data tag look unhandled
		private static SourceLines CreateImprovementsBody(string[] lines, out string parameter, out int start)
		{
			SourceLines source = SourceLines.Of(lines);
			int[] signatures = Enumerable.Range(0, source.Code.Length)
				.Where(i => CreateImprovementsSignature.IsMatch(source.Code[i]))
				.ToArray();

			if (signatures.Length != 1)
			{
				throw new InvalidOperationException(
					"Expected exactly one CreateImprovements(..., XmlNode ...) declaration, found "
					+ signatures.Length + ". The handled-tag list is read off that one method.");
			}

			parameter = CreateImprovementsSignature.Match(source.Code[signatures[0]]).Groups[1].Value;
			start = signatures[0];
			return source.BlockFrom(signatures[0]);
		}

		// - blind spot: matches any .Bonus read, not one on a vehicle mod specifically
		public static IReadOnlyList<string> VehicleModTagsNoLongerRead()
		{
			string[] sources = DataPaths.ApplicationSourceFiles().Select(File.ReadAllText).ToArray();

			return VehicleModTags
				.Where(tag =>
				{
					Regex read = new Regex(@"\.Bonus(?:\[""" + Regex.Escape(tag)
						+ @"""\]|\.InnerXml\.Contains\(""<" + Regex.Escape(tag) + @">""\))");
					return !sources.Any(source => read.IsMatch(source));
				})
				.OrderBy(tag => tag, StringComparer.Ordinal)
				.ToArray();
		}
	}
}
