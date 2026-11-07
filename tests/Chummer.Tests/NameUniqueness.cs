using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Chummer.Tests
{
	// - no two entries of a collection may share the key the application looks them up by
	public static class NameUniqueness
	{
		// - the app keys packs by name AND category at every lookup site
		// - sites: frmSelectPACKSKit.cs:111 and :715, frmCreatePACKSKit.cs:51, frmCreate.cs:20300
		// - anything absent here is keyed by <name> alone
		private static readonly IReadOnlyDictionary<string, string[]> CompositeKeyFields =
			new Dictionary<string, string[]>(StringComparer.Ordinal)
			{
				{ "packs.xml/packs", new[] { "name", "category" } },
			};

		private static readonly string[] NameOnlyKeyFields = { "name" };

		// - a control character: a printable separator could be forged by a legitimate value
		public const string KeyFieldSeparator = "\u001F";

		private static string[] KeyFieldsFor(string xmlFileName, string collectionName)
		{
			string[] fields;
			return CompositeKeyFields.TryGetValue(xmlFileName + "/" + collectionName, out fields)
				? fields
				: NameOnlyKeyFields;
		}

		// - one collection reduced to its items' lookup keys, all the uniqueness check needs
		public sealed class RuleCollection
		{
			public RuleCollection(string filePath, string name, string[] keyFields,
				IReadOnlyList<string> itemKeys)
			{
				FilePath = filePath;
				Name = name;
				KeyFields = keyFields;
				ItemKeys = itemKeys;
			}

			public string FilePath { get; }

			public string Name { get; }

			public string[] KeyFields { get; }

			public IReadOnlyList<string> ItemKeys { get; }
		}

		private static readonly Lazy<IReadOnlyList<RuleCollection>> CachedRuleCollections =
			new Lazy<IReadOnlyList<RuleCollection>>(
				() => RuleCorpus.Files.SelectMany(file => file.Collections).ToArray());

		// - a collection qualifies when its items carry a direct <name> child, read off the data
		// - that rule alone excludes <version>, the declaration blocks and skills.xml's wrappers
		// - top-level only, in step with the schema pairing: "custom content/" is a separate concern
		public static IEnumerable<object[]> TopLevelRuleXmlCollections()
		{
			return CachedRuleCollections.Value
				.Select(collection => new object[] { collection.FilePath, collection.Name });
		}

		public static RuleCollection RuleCollectionFor(string xmlPath, string collectionName)
		{
			return CachedRuleCollectionsByKey.Value[RuleCorpus.IndexKey(xmlPath, collectionName)];
		}

		// - a dictionary, not a scan per call: the allowlist guard would make that quadratic
		// - it also gives file+collection identity one place to be checked, see the throw below
		private static readonly Lazy<IReadOnlyDictionary<string, RuleCollection>> CachedRuleCollectionsByKey =
			new Lazy<IReadOnlyDictionary<string, RuleCollection>>(IndexRuleCollections);

		private static IReadOnlyDictionary<string, RuleCollection> IndexRuleCollections()
		{
			Dictionary<string, RuleCollection> index =
				new Dictionary<string, RuleCollection>(StringComparer.Ordinal);

			foreach (RuleCollection collection in CachedRuleCollections.Value)
			{
				string key = RuleCorpus.IndexKey(collection.FilePath, collection.Name);
				if (index.ContainsKey(key))
				{
					// - two same-named wrappers collide on a case id and xUnit drops the second
					// - the theory would look healthy while checking half the data
					throw new InvalidOperationException(
						"Two <" + collection.Name + "> collections in "
						+ Path.GetFileName(collection.FilePath)
						+ ". The uniqueness check identifies a collection by file and element "
						+ "name, so this needs an unambiguous identity before it can be checked.");
				}

				index.Add(key, collection);
			}

			return index;
		}

		// - inside the single parse, over RuleCorpus.CatalogueCollectionsIn
		internal static IReadOnlyList<RuleCollection> CollectionsIn(string xmlPath, string fileName,
			IEnumerable<XmlNode> catalogueCollections)
		{
			List<RuleCollection> collections = new List<RuleCollection>();

			foreach (XmlNode collection in catalogueCollections)
			{
				string[] keyFields = KeyFieldsFor(fileName, collection.Name);
				IReadOnlyList<string> itemKeys = ItemKeysIn(collection, keyFields);

				if (itemKeys.Count == 0)
					continue;

				collections.Add(new RuleCollection(xmlPath, collection.Name, keyFields, itemKeys));
			}

			return collections;
		}

		// - public so the uniqueness tests can drive it with hand-built XML
		// - the real files show today's data is clean, never that a duplicate would be caught
		public static IReadOnlyList<string> ItemKeysIn(XmlNode collection, string[] keyFields)
		{
			return collection.ChildNodes.Cast<XmlNode>()
				.Where(item => item.NodeType == XmlNodeType.Element && item["name"] != null)
				.Select(item => BuildKey(item, keyFields))
				.ToArray();
		}

		// - one implementation for the real files and the hand-built cases that prove it works
		// - ordinal here for the same reason as in BuildKey below
		public static IEnumerable<KeyValuePair<string, int>> DuplicateItemKeys(
			IReadOnlyList<string> itemKeys)
		{
			return itemKeys
				.GroupBy(key => key, StringComparer.Ordinal)
				.Where(group => group.Count() > 1)
				.Select(group => new KeyValuePair<string, int>(group.Key, group.Count()));
		}

		// - ordinal and verbatim: the application's XPath lookup compares codepoint for codepoint
		// - entries differing in case or padding are separately reachable, not a collision
		// - normalising would look tidy while making the tests disagree with the application
		private static string BuildKey(XmlNode item, string[] keyFields)
		{
			return string.Join(KeyFieldSeparator,
				keyFields.Select(field => item[field]?.InnerText ?? string.Empty));
		}
	}
}
