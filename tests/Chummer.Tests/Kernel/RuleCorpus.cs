using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Chummer.Tests.Consumption.Bonus;
using Chummer.Tests.Consumption.Expressions;
using Chummer.Tests.Consumption.Fields;
using Chummer.Tests.Rulebook;

namespace Chummer.Tests.Kernel
{
	public static class RuleCorpus
	{
		// - everything the data tests need from one file
		public sealed class RuleFile
		{
			public RuleFile(string filePath, IReadOnlyList<NameUniqueness.RuleCollection> collections,
				IReadOnlyList<BookReferences.BookReference> bookReferences,
				IReadOnlyDictionary<string, IReadOnlyCollection<string>> categoryDeclarations,
				IReadOnlyList<CategoryDeclarations.CategoryUsage> categoryUsages,
				IReadOnlyList<RequiredFields.RequiredFieldContract> requiredFieldContracts,
				IReadOnlyList<BonusVocabulary.BonusTag> bonusTags,
				IReadOnlyList<NumericFields.NumericFieldCheck> numericFieldChecks,
				IReadOnlyList<ExpressionChecks.ExpressionCheck> expressionChecks,
				IReadOnlyList<CrossFileReferences.Reference> crossFileReferences,
				IReadOnlyList<CrossFileReferences.TargetKeys> crossFileTargetKeys)
			{
				FilePath = filePath;
				Collections = collections;
				BookReferences = bookReferences;
				CategoryDeclarations = categoryDeclarations;
				CategoryUsages = categoryUsages;
				RequiredFieldContracts = requiredFieldContracts;
				BonusTags = bonusTags;
				NumericFieldChecks = numericFieldChecks;
				ExpressionChecks = expressionChecks;
				CrossFileReferences = crossFileReferences;
				CrossFileTargetKeys = crossFileTargetKeys;
			}

			public string FilePath { get; }

			public string FileName => Path.GetFileName(FilePath);

			public IReadOnlyList<NameUniqueness.RuleCollection> Collections { get; }

			public IReadOnlyList<BookReferences.BookReference> BookReferences { get; }

			// - declaration block name -> its values, see CategoryDeclarations.IsDeclarationBlock
			// - a file with none is simply absent from the category check
			public IReadOnlyDictionary<string, IReadOnlyCollection<string>> CategoryDeclarations { get; }

			public IReadOnlyList<CategoryDeclarations.CategoryUsage> CategoryUsages { get; }

			// - one per required-field rule declared against this file, already evaluated
			public IReadOnlyList<RequiredFields.RequiredFieldContract> RequiredFieldContracts { get; }

			public IReadOnlyList<BonusVocabulary.BonusTag> BonusTags { get; }

			public IReadOnlyList<NumericFields.NumericFieldCheck> NumericFieldChecks { get; }

			public IReadOnlyList<ExpressionChecks.ExpressionCheck> ExpressionChecks { get; }

			// - two halves: a rule's references and its definitions usually live in different files
			public IReadOnlyList<CrossFileReferences.Reference> CrossFileReferences { get; }

			public IReadOnlyList<CrossFileReferences.TargetKeys> CrossFileTargetKeys { get; }
		}

		public static IReadOnlyList<RuleFile> Files => CachedRuleFiles.Value;

		public static RuleFile FileAt(string xmlPath)
		{
			return CachedRuleFilesByPath.Value[xmlPath];
		}

		// - parsed once: the suite was reparsing the same documents roughly five times over
		// - cached as immutable strings: XmlDocument is not safe under xUnit's parallel classes
		// - every derived check reads these records instead of reopening the corpus
		private static readonly Lazy<IReadOnlyList<RuleFile>> CachedRuleFiles =
			new Lazy<IReadOnlyList<RuleFile>>(LoadTopLevelRuleFiles);

		private static readonly Lazy<IReadOnlyDictionary<string, RuleFile>> CachedRuleFilesByPath =
			new Lazy<IReadOnlyDictionary<string, RuleFile>>(
				() => CachedRuleFiles.Value.ToDictionary(file => file.FilePath, StringComparer.Ordinal));

		private static IReadOnlyList<RuleFile> LoadTopLevelRuleFiles()
		{
			List<RuleFile> files = new List<RuleFile>();

			foreach (string xmlPath in Directory
				.EnumerateFiles(DataPaths.ChummerDataDir, "*.xml", SearchOption.TopDirectoryOnly)
				.OrderBy(path => path, StringComparer.Ordinal))
			{
				XmlDocument document = new XmlDocument
				{
					// - matches the XmlReader tests' default: two loaders must not disagree here
					XmlResolver = null
				};
				document.Load(xmlPath);

				XmlElement root = document.DocumentElement;
				if (root == null)
					continue;

				string fileName = Path.GetFileName(xmlPath);
				IReadOnlyDictionary<string, IReadOnlyCollection<string>> declarations =
					CategoryDeclarations.DeclarationBlocksIn(root, fileName);
				XmlNode[] collections = CatalogueCollectionsIn(root).ToArray();

				files.Add(new RuleFile(xmlPath,
					NameUniqueness.CollectionsIn(xmlPath, fileName, collections),
					BookReferences.In(document),
					declarations,
					collections.SelectMany(CategoryDeclarations.UsagesIn).ToArray(),
					RequiredFields.ContractsIn(fileName, xmlPath, document),
					BonusVocabulary.BonusTagsIn(fileName, document),
					NumericFields.ChecksIn(fileName, xmlPath, document),
					ExpressionChecks.ChecksIn(fileName, xmlPath, document, ExpressionChecks.AllRules),
					CrossFileReferences.ReferencesIn(fileName, document, CrossFileReferences.Rules),
					CrossFileReferences.TargetKeysIn(fileName, document, CrossFileReferences.Rules)));
			}

			return files;
		}

		// - every top-level element that is not a declaration block
		public static IEnumerable<XmlNode> CatalogueCollectionsIn(XmlElement root)
		{
			return root.ChildNodes.Cast<XmlNode>()
				.Where(node => node.NodeType == XmlNodeType.Element
					&& !CategoryDeclarations.IsDeclarationBlock(node.Name));
		}

		// - file name, not path: the hand-written tables have to stay readable
		// - a path would not throw, the lookup would just find nothing
		internal static string TableKey(string fileName, string collectionName)
		{
			return CollectionKey(fileName, collectionName);
		}

		internal static string CacheKey(string xmlPath, string collectionName)
		{
			return CollectionKey(xmlPath, collectionName);
		}

		private static string CollectionKey(string file, string collectionName)
		{
			return file + "/" + collectionName;
		}

		// - the entry's own <name>, else the nearest ancestor's, else "(unnamed)"
		// - a nameless nested reference is reported under the entry a reader would open
		internal static string ItemLabel(XmlNode item)
		{
			return item["name"]?.InnerText ?? NearestItemName(item);
		}

		// - the closest ancestor with a <name>: a metavariant, not the metatype holding it
		// - the more specific of the two is the one a reader would go looking for
		internal static string NearestItemName(XmlNode node)
		{
			for (XmlNode ancestor = node.ParentNode; ancestor != null; ancestor = ancestor.ParentNode)
			{
				XmlElement name = (ancestor as XmlElement)?["name"];
				if (name != null)
					return name.InnerText;
			}

			return "(unnamed)";
		}
	}
}
