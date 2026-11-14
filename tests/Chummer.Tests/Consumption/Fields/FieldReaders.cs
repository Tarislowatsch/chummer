using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Chummer.Tests.Consumption.Bonus;
using Chummer.Tests.Kernel;

namespace Chummer.Tests.Consumption.Fields
{
	// - a field no code reads does nothing, yet the data still states a rule
	// - names against every literal, requirement and handler blocks against their consumer
	public static class FieldReaders
	{
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

		// - "file group/tag", or "file required" for a bare word -> why it is no reader in the table
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

		// - keyed by group: a new oneof read in a file listed for allof must fail too
		// - a bare block needs a reader of that block in the same file, or a known extra reader
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
			bool read = mention.Group == null
				? inFile.Any(reader => reader.Group.Split('/')[0] == mention.Block)
				: inFile.Any(reader => reader.Group == mention.Group);
			return read || extraReaders.ContainsKey(file + " " + mention.Display);
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
		public static IReadOnlyCollection<string> TagsReadBy(RequirementReader reader, SourceLines source)
		{
			if (reader.Style == ReadStyle.PathLiterals)
			{
				string prefix = reader.Group + "/";
				return new HashSet<string>(
					NameLookups.LiteralsIn(reader.ConsumerFile, source)
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
				Readers.ToDictionary(reader => reader,
					reader => TagsReadBy(reader, SourceCorpus.File(reader.ConsumerFile).Source)));

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

		// - bonus handlers whose branch reads every field directly off its block, see BranchOf
		// - the name level misses their dead fields: min, max and val are read by other handlers
		// - the rest read through variables or carry only text
		public static readonly IReadOnlyList<string> IndexedHandlers = new[]
		{
			"addattribute", "conditionmonitor", "livingpersona", "selectattribute", "selectskill",
			"selectskillgroup", "swapskillattribute",
		};

		public enum BlockReach
		{
			// - the first block of its name below a root CreateImprovements walks
			First,

			// - the indexer returns the first block only: this one is never read
			LaterSibling,

			// - not a direct child of such a root, e.g. under a vehicle mod
			OutsideRoot,
		}

		// One child of a handler block in the data, e.g. addattribute/min.
		public sealed class HandlerField
		{
			public HandlerField(string file, string handler, BlockReach reach, string tag, string value,
				bool repeated = false, IReadOnlyCollection<string> blockAttributes = null)
			{
				File = file;
				Handler = handler;
				Reach = reach;
				Tag = tag;
				Value = value;
				Repeated = repeated;
				BlockAttributes = blockAttributes ?? new string[0];
			}

			public string File { get; }

			public string Handler { get; }

			public BlockReach Reach { get; }

			// - "@name" for an attribute of the block
			public string Tag { get; }

			// - InnerText untrimmed: the branch compares it as is
			public string Value { get; }

			// - a later child of the same name: an indexer or SelectSingleNode never reaches it
			public bool Repeated { get; }

			// - every attribute of the block, for the else-if chains
			public IReadOnlyCollection<string> BlockAttributes { get; }

			public string Entry => File + " " + Handler + "/" + Tag
				+ (Reach == BlockReach.First ? "" : " (" + Reach + ")")
				+ (Repeated ? " (repeated)" : "");
		}

		public const string AttributePrefix = "@";

		// - public so the detection tests can drive it with hand-built XML
		public static IReadOnlyList<HandlerField> HandlerFieldsIn(string file, string baseFile, XmlDocument document,
			IEnumerable<string> handlers)
		{
			IReadOnlyCollection<XmlNode> roots = BonusVocabulary.CreateImprovementsRoots(baseFile, document);
			return handlers
				.SelectMany(handler => document.SelectNodes("//" + handler).Cast<XmlElement>()
					.SelectMany(block =>
					{
						BlockReach reach = !roots.Contains(block.ParentNode) ? BlockReach.OutsideRoot
							: block.ParentNode[handler] == block ? BlockReach.First
							: BlockReach.LaterSibling;
						string[] attributes = block.Attributes.Cast<XmlAttribute>()
							.Select(attribute => AttributePrefix + attribute.LocalName).ToArray();
						HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
						return block.Attributes.Cast<XmlAttribute>()
							.Select(attribute => new HandlerField(file, handler, reach, AttributePrefix + attribute.LocalName,
								attribute.Value, false, attributes))
							.Concat(block.ChildNodes.Cast<XmlNode>()
								.Select(node => (Tag: TagOf(node), node.InnerText, Element: node is XmlElement))
								.Where(child => child.Tag != null)
								.Select(child => new HandlerField(file, handler, reach, child.Tag, child.InnerText,
									child.Element && !seen.Add(child.Tag), attributes)))
							.ToArray();
					}))
				.ToArray();
		}

		// One handler's branch in CreateImprovements: its literals and the fields its indexer reads.
		public sealed class HandlerBranch
		{
			public HandlerBranch(string handler, IReadOnlyList<NameLookups.Literal> literals,
				IReadOnlyCollection<string> fields, IReadOnlyDictionary<string, IReadOnlyCollection<string>> dispatch,
				IReadOnlyCollection<string> everyCopy = null, IReadOnlyList<IReadOnlyList<string>> chains = null)
			{
				Handler = handler;
				Literals = literals;
				Fields = fields;
				Dispatch = dispatch;
				EveryCopy = everyCopy ?? new string[0];
				Chains = chains ?? new IReadOnlyList<string>[0];
			}

			public string Handler { get; }

			public IReadOnlyList<NameLookups.Literal> Literals { get; }

			public IReadOnlyCollection<string> Fields { get; }

			// - field -> the values its InnerText is compared to with ==
			// - the comparisons have no else: any other value does nothing
			public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Dispatch { get; }

			// - fields read by SelectNodes: the only form that reaches a repeated child
			public IReadOnlyCollection<string> EveryCopy { get; }

			// - attributes read in one if/else-if chain, in order: only the first present one counts
			public IReadOnlyList<IReadOnlyList<string>> Chains { get; }
		}

		// - public so the detection tests can drive it with hand-built source
		public static HandlerBranch BranchOf(string handler, string fileName, SourceLines source)
		{
			(int start, SourceLines block, string parameter) = BonusVocabulary.HandlerBlock(handler, source);
			// - block and field each by indexer or by SelectSingleNode/SelectNodes
			// - a path like "handler/field" is left to the literal guard
			string name = Regex.Escape(handler);
			string blockOf = Regex.Escape(parameter) + @"(?:\[\s*""" + name + @"""\s*\]"
				+ @"|\.SelectSingleNode\(\s*""" + name + @"""\s*\))";
			string fieldRead = blockOf + @"\s*(?:\[\s*""(?<field>[^""]+)""\s*\]"
				+ @"|\.(?<method>SelectSingleNode|SelectNodes)\(\s*""(?<field>\w+)""\s*\)"
				+ @"|\.Attributes\[\s*""(?<attribute>\w+)""\s*\])";
			Regex read = new Regex(fieldRead);
			Regex comparison = new Regex(fieldRead + @"\.InnerText\s*==\s*""(?<value>[^""]*)""");
			Regex condition = new Regex(@"^\s*(?<else>else\s+)?if\s*\(");
			Match[] reads = block.Code.SelectMany(line => read.Matches(line).Cast<Match>()).ToArray();

			string[] sites = Enumerable.Range(start + 1, block.Code.Length)
				.Select(line => fileName + ":" + line).ToArray();
			return new HandlerBranch(handler,
				NameLookups.LiteralsIn(fileName, source).Where(literal => sites.Contains(literal.Site)).ToArray(),
				new HashSet<string>(reads.Select(FieldOf), StringComparer.Ordinal),
				block.Code.SelectMany(line => comparison.Matches(line).Cast<Match>())
					.GroupBy(FieldOf, StringComparer.Ordinal)
					.ToDictionary(group => group.Key,
						group => (IReadOnlyCollection<string>)new HashSet<string>(
							group.Select(match => match.Groups["value"].Value), StringComparer.Ordinal),
						StringComparer.Ordinal),
				new HashSet<string>(reads.Where(match => match.Groups["method"].Value == "SelectNodes").Select(FieldOf),
					StringComparer.Ordinal),
				ChainsIn(block.Code, condition, read));
		}

		private static string FieldOf(Match match)
		{
			return match.Groups["attribute"].Success
				? AttributePrefix + match.Groups["attribute"].Value
				: match.Groups["field"].Value;
		}

		// - a condition line whose next line reads one attribute is a link
		// - else-if right after a link extends its chain, anything else starts a new one
		private static IReadOnlyList<IReadOnlyList<string>> ChainsIn(string[] code, Regex condition, Regex read)
		{
			List<List<string>> chains = new List<List<string>>();
			int lastLink = -3;
			for (int i = 0; i + 1 < code.Length; i++)
			{
				Match test = condition.Match(code[i]);
				Match[] body = read.Matches(code[i + 1]).Cast<Match>()
					.Where(match => match.Groups["attribute"].Success).ToArray();
				if (!test.Success || body.Length != 1)
					continue;

				if (!test.Groups["else"].Success || lastLink != i - 2)
					chains.Add(new List<string>());
				chains[chains.Count - 1].Add(FieldOf(body[0]));
				lastLink = i;
			}

			return chains.Where(chain => chain.Count > 1).Select(chain => (IReadOnlyList<string>)chain).ToArray();
		}

		public static IReadOnlyList<HandlerBranch> HandlerBranches => CachedBranches.Value;

		private static readonly Lazy<IReadOnlyList<HandlerBranch>> CachedBranches =
			new Lazy<IReadOnlyList<HandlerBranch>>(() =>
			{
				SourceLines source = SourceCorpus.File(BonusVocabulary.HandlerConsumerFile).Source;
				return IndexedHandlers
					.Select(handler => BranchOf(handler, BonusVocabulary.HandlerConsumerFile, source))
					.ToArray();
			});

		public static IEnumerable<HandlerField> UnreadHandlerFields(IEnumerable<HandlerField> fields,
			IEnumerable<HandlerBranch> branches)
		{
			Dictionary<string, HandlerBranch> byHandler = branches.ToDictionary(branch => branch.Handler, StringComparer.Ordinal);
			return fields.Where(field => field.Reach != BlockReach.First || !byHandler[field.Handler].Fields.Contains(field.Tag)
				|| (field.Repeated && !byHandler[field.Handler].EveryCopy.Contains(field.Tag))
				|| IsShadowed(field, byHandler[field.Handler]));
		}

		// - an earlier link of its chain is present in the block: the else-if never gets here
		private static bool IsShadowed(HandlerField field, HandlerBranch branch)
		{
			return branch.Chains
				.Where(chain => chain.Contains(field.Tag))
				.Any(chain => chain.TakeWhile(link => link != field.Tag).Any(field.BlockAttributes.Contains));
		}

		// - a read field whose value no comparison matches is just as dead
		// - a later or unreached block is already unread as a whole
		public static IEnumerable<HandlerField> UnhandledHandlerValues(IEnumerable<HandlerField> fields,
			IEnumerable<HandlerBranch> branches)
		{
			Dictionary<string, HandlerBranch> byHandler = branches.ToDictionary(branch => branch.Handler, StringComparer.Ordinal);
			return fields.Where(field => field.Reach == BlockReach.First
				&& byHandler[field.Handler].Dispatch.TryGetValue(field.Tag, out IReadOnlyCollection<string> values)
				&& !values.Contains(field.Value));
		}

		// - a read outside the branch would leave its findings standing while the code uses them
		public static IEnumerable<NameLookups.Literal> HandlerLiteralsOutsideBranch(IEnumerable<NameLookups.Literal> literals,
			HandlerBranch branch)
		{
			return literals.Where(literal => IdentifiersIn(new[] { literal.Text }).Contains(branch.Handler)
				&& !branch.Literals.Any(inside => inside.Site == literal.Site && inside.Text == literal.Text));
		}

		// - a field literal in the branch the indexer pattern missed, e.g. SelectSingleNode("val")
		public static IReadOnlyCollection<string> UnextractedFields(HandlerBranch branch, IEnumerable<string> dataTags)
		{
			HashSet<string> tags = new HashSet<string>(dataTags.Select(Unprefixed), StringComparer.Ordinal);
			HashSet<string> fields = new HashSet<string>(branch.Fields.Select(Unprefixed), StringComparer.Ordinal);
			return new HashSet<string>(
				IdentifiersIn(branch.Literals.Select(literal => literal.Text))
					.Where(identifier => tags.Contains(identifier) && !fields.Contains(identifier)),
				StringComparer.Ordinal);
		}

		// - a literal spells an attribute without the prefix
		private static string Unprefixed(string tag)
		{
			return tag.StartsWith(AttributePrefix, StringComparison.Ordinal) ? tag.Substring(AttributePrefix.Length) : tag;
		}

		public static IReadOnlyList<HandlerField> AllHandlerFields => CachedCorpus.Value.Fields;

		// - XmlDocument is not safe under xUnit's parallel classes: only immutable records leave here
		private static readonly Lazy<(IReadOnlyList<ElementName> Names, IReadOnlyList<RequirementUse> Uses,
			IReadOnlyList<HandlerField> Fields)> CachedCorpus =
			new Lazy<(IReadOnlyList<ElementName>, IReadOnlyList<RequirementUse>, IReadOnlyList<HandlerField>)>(() =>
			{
				List<ElementName> names = new List<ElementName>();
				List<RequirementUse> uses = new List<RequirementUse>();
				List<HandlerField> fields = new List<HandlerField>();
				foreach (DataPaths.DataFile file in DataPaths.DataFilesWithPacks)
				{
					XmlDocument document = Xml.Load(file.FilePath);
					names.AddRange(ElementNamesIn(file.Label, document));
					uses.AddRange(RequirementUsesIn(file.Label, file.BaseFileName, document));
					fields.AddRange(HandlerFieldsIn(file.Label, file.BaseFileName, document, IndexedHandlers));
				}

				return (names, uses, fields);
			});

		// - path below the document element, e.g. "mods/mod" for vehicles.xml
		private static string PathOf(XmlElement element)
		{
			return string.Join("/", element.SelectNodes("ancestor-or-self::*").Cast<XmlElement>()
				.Skip(1).Select(ancestor => ancestor.LocalName));
		}
	}
}
