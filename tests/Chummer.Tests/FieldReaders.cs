using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace Chummer.Tests
{
	// - a field no code reads does nothing, yet the data still states a rule
	// - element names are checked against every literal, requirement blocks against their consumer
	public static class FieldReaders
	{
		// One data file as Load merges it: top-level, or a pack file merged into its base.
		public sealed class DataFile
		{
			public DataFile(string path, string baseFileName)
			{
				FilePath = path;
				BaseFileName = baseFileName;
			}

			public string FilePath { get; }

			public string BaseFileName { get; }

			public string Label => FilePath.Substring(DataPaths.ChummerDataDir.Length + 1).Replace('\\', '/');
		}

		public static IReadOnlyList<DataFile> DataFiles => CachedDataFiles.Value;

		private static readonly Lazy<IReadOnlyList<DataFile>> CachedDataFiles =
			new Lazy<IReadOnlyList<DataFile>>(() => Directory
				.EnumerateFiles(DataPaths.ChummerDataDir, "*.xml", SearchOption.TopDirectoryOnly)
				.OrderBy(path => path, StringComparer.Ordinal)
				.Select(path => new DataFile(path, Path.GetFileName(path)))
				.Concat(CustomContent.PairedPackFiles()
					.Select(pack => new DataFile(pack.FilePath, pack.BaseFileNames.Single())))
				.ToArray());

		// One element name in one data file, with the path of its first occurrence.
		public sealed class ElementName
		{
			public ElementName(string file, string name, string firstPath, int count)
			{
				File = file;
				Name = name;
				FirstPath = firstPath;
				Count = count;
			}

			public string File { get; }

			public string Name { get; }

			public string FirstPath { get; }

			public int Count { get; }

			public string Entry => File + " " + Name;
		}

		private static readonly Regex Identifier = new Regex(@"[A-Za-z_][A-Za-z0-9_]*");

		// - any literal that spells the name counts as its reader
		// - a lower bound: a name read in one file hides an unread use in another
		public static IReadOnlyCollection<string> IdentifiersIn(IEnumerable<string> literals)
		{
			return new HashSet<string>(
				literals.SelectMany(literal => Identifier.Matches(literal).Cast<Match>().Select(match => match.Value)),
				StringComparer.Ordinal);
		}

		public static IReadOnlyCollection<string> LiteralIdentifiers => CachedLiteralIdentifiers.Value;

		private static readonly Lazy<IReadOnlyCollection<string>> CachedLiteralIdentifiers =
			new Lazy<IReadOnlyCollection<string>>(
				() => IdentifiersIn(NameLookups.AllLiterals.Select(literal => literal.Text)));

		// - public so the detection tests can drive it with hand-built XML
		public static IReadOnlyList<ElementName> ElementNamesIn(string file, XmlDocument document)
		{
			return document.SelectNodes("//*").Cast<XmlElement>()
				.GroupBy(element => element.LocalName, StringComparer.Ordinal)
				.Select(group => new ElementName(file, group.Key, PathOf(group.First()), group.Count()))
				.ToArray();
		}

		public static IEnumerable<ElementName> UnreadNames(IEnumerable<ElementName> names,
			IReadOnlyCollection<string> identifiers)
		{
			return names.Where(name => !identifiers.Contains(name.Name));
		}

		public static IReadOnlyList<ElementName> AllElementNames => CachedCorpus.Value.Names;

		public enum ReadStyle
		{
			// - one loop walks the group and compares each child's .Name to a literal
			NameDispatch,

			// - one literal per tag, written as "<group>/<tag>"
			PathLiterals,
		}

		// One consumer that reads one requirement group of one file.
		public sealed class RequirementReader
		{
			public RequirementReader(string file, string itemPath, string group, string consumer, ReadStyle style)
			{
				File = file;
				ItemPath = itemPath;
				Group = group;
				Consumer = consumer;
				Style = style;
			}

			public string File { get; }

			public string ItemPath { get; }

			public string Group { get; }

			// - "file.cs:line" of the loop or the first literal it is read off
			public string Consumer { get; }

			public string ConsumerFile => Consumer.Substring(0, Consumer.IndexOf(':'));

			public ReadStyle Style { get; }

			public string Key => File + " " + Group;
		}

		// - read off by hand: which consumer walks which group of which file
		// - the tags each group accepts come from the consumer source, not from this table
		public static readonly IReadOnlyList<RequirementReader> Readers = new[]
		{
			new RequirementReader("echoes.xml", "echoes/echo", "required/allof",
				"frmSelectMetamagic.cs:264", ReadStyle.PathLiterals),
			new RequirementReader("metamagic.xml", "metamagics/metamagic", "required/allof",
				"frmSelectMetamagic.cs:264", ReadStyle.PathLiterals),
			new RequirementReader("powers.xml", "powers/power", "required/allof",
				"frmSelectPower.cs:255", ReadStyle.PathLiterals),
			new RequirementReader("powers.xml", "powers/power", "required/oneof",
				"frmSelectPower.cs:274", ReadStyle.PathLiterals),
			new RequirementReader("qualities.xml", "qualities/quality", "forbidden/oneof",
				"frmSelectQuality.cs:429", ReadStyle.NameDispatch),
			new RequirementReader("qualities.xml", "qualities/quality", "required/oneof",
				"frmSelectQuality.cs:529", ReadStyle.NameDispatch),
			new RequirementReader("qualities.xml", "qualities/quality", "required/allof",
				"frmSelectQuality.cs:833", ReadStyle.NameDispatch),
		};

		// One place a literal names a requirement block: bare, as a group, or with a tag below it.
		public sealed class RequirementMention
		{
			public RequirementMention(string block, string group, string tag)
			{
				Block = block;
				Group = group;
				Tag = tag;
			}

			public string Block { get; }

			// - null for a bare "required": InnerXml.Contains and ["required"] name no group
			// - the block itself for a direct child such as "required/bodymin"
			public string Group { get; }

			public string Tag { get; }

			public string Display => Group == null ? Block : Tag == null ? Group : Group + "/" + Tag;
		}

		private static readonly Regex RequirementPath = new Regex(
			@"(?<![A-Za-z0-9_])(required|forbidden)(?:/([A-Za-z0-9_]+)(?:/([A-Za-z0-9_]+))?)?(?![A-Za-z0-9_])");

		// - public so the detection tests can drive it with made-up literals
		public static IReadOnlyList<RequirementMention> RequirementMentionsIn(string literal)
		{
			return RequirementPath.Matches(literal).Cast<Match>()
				.Select(match =>
				{
					string block = match.Groups[1].Value;
					string step = match.Groups[2].Success ? match.Groups[2].Value : null;
					string below = match.Groups[3].Success ? match.Groups[3].Value : null;
					if (step == null)
						return new RequirementMention(block, null, null);
					return GroupNames.Contains(step)
						? new RequirementMention(block, block + "/" + step, below)
						: new RequirementMention(block, block, step);
				})
				.ToArray();
		}

		// - "file group/tag" -> why that consumer is no reader in the table
		// - leaving a reader out can only add findings, never hide one
		// - keyed down to the tag: a whole group in frmCreate.cs would wave any new check through
		public static readonly IReadOnlyDictionary<string, string> KnownExtraReaders =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "frmCreate.cs required/allof/metatype", "ChangeMetatype re-checks a quality's metatype" },
				{ "frmCreate.cs required/allof/metavariant", "ChangeMetatype re-checks a quality's metavariant" },
				{ "frmCreate.cs required/oneof/metatype", "ChangeMetatype re-checks a quality's metatype" },
				{ "frmCreate.cs required/oneof/metavariant", "ChangeMetatype re-checks a quality's metavariant" },
			};

		// - a mention needs a reader for its file and group, or a known extra reader for its tag
		// - keyed by group: a new oneof read in a file listed for allof must fail too
		// - a bare block needs a reader of that block in the same file
		public static IEnumerable<NameLookups.Literal> UnlistedRequirementLiterals(
			IEnumerable<NameLookups.Literal> literals, IEnumerable<RequirementReader> readers,
			IReadOnlyDictionary<string, string> extraReaders)
		{
			RequirementReader[] all = readers.ToArray();
			return literals.Where(literal => RequirementMentionsIn(literal.Text)
				.Any(mention => !IsListed(literal.File, mention, all, extraReaders)));
		}

		private static bool IsListed(string file, RequirementMention mention, RequirementReader[] readers,
			IReadOnlyDictionary<string, string> extraReaders)
		{
			IEnumerable<RequirementReader> inFile = readers.Where(reader => reader.ConsumerFile == file);
			if (mention.Group == null)
				return inFile.Any(reader => reader.Group.Split('/')[0] == mention.Block);
			return inFile.Any(reader => reader.Group == mention.Group)
				|| extraReaders.ContainsKey(file + " " + mention.Display);
		}

		// - the line in Consumer must still hold the group's literal: a shifted file leaves a dead pointer
		public static bool ConsumerLineSelectsGroup(RequirementReader reader, IEnumerable<NameLookups.Literal> literals)
		{
			return literals.Any(literal => literal.Site == reader.Consumer
				&& (literal.Text == reader.Group || literal.Text.StartsWith(reader.Group + "/", StringComparison.Ordinal)));
		}

		private static readonly Regex NameComparison = new Regex(@"\.Name\s*==\s*""([^""]+)""");

		// - public so the detection tests can drive it with hand-built source
		// - NameDispatch takes the loop that follows the one literal equal to the group
		public static IReadOnlyCollection<string> TagsReadBy(RequirementReader reader, string[] lines)
		{
			SourceLines source = SourceLines.Of(lines);
			if (reader.Style == ReadStyle.PathLiterals)
			{
				string prefix = reader.Group + "/";
				return new HashSet<string>(
					NameLookups.LiteralsIn(reader.ConsumerFile, lines)
						.Where(literal => literal.Text.StartsWith(prefix, StringComparison.Ordinal))
						.Select(literal => Identifier.Match(literal.Text.Substring(prefix.Length)).Value)
						.Where(tag => tag.Length > 0),
					StringComparer.Ordinal);
			}

			int[] starts = Enumerable.Range(0, source.Code.Length)
				.SelectMany(i => NameLookups.Tokenize(source.Code[i])
					.Where(token => token.IsLiteral && token.Text == reader.Group)
					.Select(token => i))
				.ToArray();
			if (starts.Length != 1)
			{
				throw new InvalidOperationException(reader.Consumer + " must select \"" + reader.Group
					+ "\" exactly once, found " + starts.Length);
			}

			return new HashSet<string>(
				source.BlockFrom(starts[0]).Code
					.SelectMany(line => NameComparison.Matches(line).Cast<Match>())
					.Select(match => match.Groups[1].Value),
				StringComparer.Ordinal);
		}

		public static IReadOnlyCollection<string> TagsReadBy(RequirementReader reader)
		{
			return CachedTags.Value[reader];
		}

		private static readonly Lazy<IReadOnlyDictionary<RequirementReader, IReadOnlyCollection<string>>> CachedTags =
			new Lazy<IReadOnlyDictionary<RequirementReader, IReadOnlyCollection<string>>>(() =>
			{
				// - Single: a file name shared by two folders must not pick a consumer silently
				ILookup<string, string> sources = DataPaths.ApplicationSourceFiles()
					.ToLookup(Path.GetFileName, StringComparer.Ordinal);
				return Readers.ToDictionary(reader => reader,
					reader => TagsReadBy(reader, File.ReadAllLines(sources[reader.ConsumerFile].Single())));
			});

		// One child of a requirement group, or of the block itself when it has no group.
		public sealed class RequirementUse
		{
			public RequirementUse(string file, string baseFile, string itemPath, string itemName,
				string group, string tag)
			{
				File = file;
				BaseFile = baseFile;
				ItemPath = itemPath;
				ItemName = itemName;
				Group = group;
				Tag = tag;
			}

			public string File { get; }

			public string BaseFile { get; }

			public string ItemPath { get; }

			public string ItemName { get; }

			public string Group { get; }

			// - "#text" for text directly in a block or group: no .Name dispatch can read it
			public string Tag { get; }

			public string Entry => File + " | " + ItemName + " | " + Group + "/" + Tag;
		}

		public const string TextTag = "#text";

		private static readonly string[] GroupNames = { "allof", "oneof" };

		// - public so the detection tests can drive it with hand-built XML
		public static IReadOnlyList<RequirementUse> RequirementUsesIn(string file, string baseFile, XmlDocument document)
		{
			List<RequirementUse> uses = new List<RequirementUse>();
			foreach (XmlElement block in document.SelectNodes("//required | //forbidden").Cast<XmlElement>())
			{
				XmlElement item = (XmlElement)block.ParentNode;
				string itemPath = PathOf(item);
				string itemName = item["name"]?.InnerText ?? "(no name)";

				foreach (XmlNode child in block.ChildNodes)
				{
					if (child is XmlElement group && GroupNames.Contains(group.LocalName))
					{
						string groupPath = block.LocalName + "/" + group.LocalName;
						uses.AddRange(group.ChildNodes.Cast<XmlNode>()
							.Select(TagOf).Where(tag => tag != null)
							.Select(tag => new RequirementUse(file, baseFile, itemPath, itemName, groupPath, tag)));
					}
					else if (TagOf(child) is string tag)
					{
						uses.Add(new RequirementUse(file, baseFile, itemPath, itemName, block.LocalName, tag));
					}
				}
			}

			return uses;
		}

		private static string TagOf(XmlNode node)
		{
			if (node is XmlElement element)
				return element.LocalName;
			bool isText = node.NodeType == XmlNodeType.Text || node.NodeType == XmlNodeType.CDATA;
			return isText && node.Value.Trim().Length > 0 ? TextTag : null;
		}

		// - public so the detection tests can pass readers and tags of their own
		public static IEnumerable<RequirementUse> UnreadRequirements(IEnumerable<RequirementUse> uses,
			IEnumerable<RequirementReader> readers, Func<RequirementReader, IReadOnlyCollection<string>> tagsReadBy)
		{
			RequirementReader[] all = readers.ToArray();
			return uses.Where(use => !all.Any(reader =>
				reader.File == use.BaseFile && reader.ItemPath == use.ItemPath && reader.Group == use.Group
				&& tagsReadBy(reader).Contains(use.Tag)));
		}

		public static IReadOnlyList<RequirementUse> AllRequirementUses => CachedCorpus.Value.Uses;

		// - XmlDocument is not safe under xUnit's parallel classes: only immutable records leave here
		private static readonly Lazy<(IReadOnlyList<ElementName> Names, IReadOnlyList<RequirementUse> Uses)> CachedCorpus =
			new Lazy<(IReadOnlyList<ElementName>, IReadOnlyList<RequirementUse>)>(() =>
			{
				List<ElementName> names = new List<ElementName>();
				List<RequirementUse> uses = new List<RequirementUse>();
				foreach (DataFile file in DataFiles)
				{
					XmlDocument document = new XmlDocument();
					document.Load(file.FilePath);
					names.AddRange(ElementNamesIn(file.Label, document));
					uses.AddRange(RequirementUsesIn(file.Label, file.BaseFileName, document));
				}

				return (names, uses);
			});

		// - path below the document element, e.g. "mods/mod" for vehicles.xml
		private static string PathOf(XmlElement element)
		{
			return string.Join("/", element.SelectNodes("ancestor-or-self::*").Cast<XmlElement>()
				.Skip(1).Select(ancestor => ancestor.LocalName));
		}
	}
}
