using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Chummer.Tests.Kernel;

namespace Chummer.Tests.Localization
{
	// - a port of the translation pass in XmlManager.Load (clsXmlManager.cs:184-313)
	// - a translation whose lookup misses is dropped without a word: the item shows in English
	// - completeness is not checked: an untranslated item is unfinished work, not a defect
	public static class TranslationReferences
	{
		public enum Kind
		{
			File,
			Item,
			Text,
			Spec,
			Metavariant,
			Advantage,
			Unreached,
		}

		public enum Outcome
		{
			Applied,
			Dropped,
			Crashes,
		}

		public sealed class Entry
		{
			public Entry(string language, string fileName, Kind kind, string key, Outcome outcome)
			{
				Language = language;
				FileName = fileName;
				Kind = kind;
				Key = key;
				Outcome = outcome;
			}

			public string Language { get; }

			public string FileName { get; }

			public Kind Kind { get; }

			public string Key { get; }

			public Outcome Outcome { get; }

			// - the allowlist key and the failure text
			public string Display => Language + " " + FileName + " " + Kind.ToString().ToLowerInvariant() + ": " + Key;
		}

		// - the language data files LanguageManager loads, one per language
		public static IReadOnlyList<string> Languages => CachedEntries.Value.Keys.OrderBy(l => l, StringComparer.Ordinal).ToArray();

		public static IReadOnlyList<Entry> EntriesFor(string language)
		{
			return CachedEntries.Value[language];
		}

		public static IEnumerable<Entry> AllEntries => CachedEntries.Value.Values.SelectMany(entries => entries);

		private static readonly string LanguageDir = Path.Combine(DataPaths.RepoRoot, "Chummer", "lang");

		private const string LanguageDataSuffix = "_data.xml";

		// - one pass up front: XmlDocument is not safe under xUnit's parallel classes
		private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<Entry>>> CachedEntries =
			new Lazy<IReadOnlyDictionary<string, IReadOnlyList<Entry>>>(LoadEntries);

		// - Load matches @file exactly, unlike File.Exists on Windows
		public static bool IsDataFile(string fileName)
		{
			return DataFileNames.Value.Contains(fileName);
		}

		private static readonly Lazy<HashSet<string>> DataFileNames = new Lazy<HashSet<string>>(() =>
			new HashSet<string>(Directory.EnumerateFiles(DataPaths.ChummerDataDir, "*.xml").Select(Path.GetFileName),
				StringComparer.Ordinal));

		private static IReadOnlyDictionary<string, IReadOnlyList<Entry>> LoadEntries()
		{
			Dictionary<string, XmlDocument> english = new Dictionary<string, XmlDocument>(StringComparer.Ordinal);
			Func<string, XmlDocument> englishFor = fileName =>
			{
				if (!english.ContainsKey(fileName))
				{
					english[fileName] = IsDataFile(fileName)
						? Load(Path.Combine(DataPaths.ChummerDataDir, fileName))
						: null;
				}

				return english[fileName];
			};

			return Directory.EnumerateFiles(LanguageDir, "*" + LanguageDataSuffix)
				.ToDictionary(LanguageOf, path => EntriesIn(LanguageOf(path), Load(path), englishFor),
					StringComparer.Ordinal);
		}

		private static string LanguageOf(string path)
		{
			string fileName = Path.GetFileName(path);
			return fileName.Substring(0, fileName.Length - LanguageDataSuffix.Length);
		}

		// - public so the detection tests can drive it with hand-built XML
		// - englishFor returns null for a file the data directory does not have
		public static IReadOnlyList<Entry> EntriesIn(string language, XmlDocument languageDoc,
			Func<string, XmlDocument> englishFor)
		{
			List<Entry> entries = new List<Entry>();
			HashSet<XmlNode> read = new HashSet<XmlNode>();

			foreach (XmlElement block in languageDoc.SelectNodes("/chummer/chummer[@file]").OfType<XmlElement>())
			{
				string fileName = block.GetAttribute("file");
				XmlDocument englishDoc = englishFor(fileName);

				// - Load only ever asks for files that exist: a block for any other is never read
				entries.Add(new Entry(language, fileName, Kind.File, fileName,
					englishDoc == null ? Outcome.Dropped : Outcome.Applied));
				if (englishDoc == null)
				{
					read.UnionWith(TranslationsUnder(block));
					continue;
				}

				foreach (XmlElement type in block.ChildNodes.OfType<XmlElement>())
				{
					foreach (XmlElement child in type.ChildNodes.OfType<XmlElement>())
						entries.AddRange(EntriesOf(language, fileName, englishDoc, type, child, read));
				}
			}

			// - a translation outside every lookup is lost as surely as one whose lookup misses
			entries.AddRange(TranslationsUnder(languageDoc.DocumentElement)
				.Where(node => !read.Contains(node))
				.Select(node => new Entry(language, BlockFileOf(node), Kind.Unreached, PathOf(node), Outcome.Dropped)));

			return entries;
		}

		// - a <translate> element or an element carrying translate="…"
		private static IEnumerable<XmlNode> TranslationsUnder(XmlNode root)
		{
			return root.SelectNodes(".//translate | .//*[@translate]").Cast<XmlNode>();
		}

		private static string BlockFileOf(XmlNode node)
		{
			return (node.SelectSingleNode("ancestor::chummer[@file][1]") as XmlElement)?.GetAttribute("file") ?? "(no block)";
		}

		// - element path below the block, then the text a reader would search for
		private static string PathOf(XmlNode node)
		{
			XmlNode owner = node.Name == "translate" ? node.ParentNode : node;
			List<string> path = new List<string>();
			for (XmlNode step = owner; step != null && !(step is XmlElement element && element.HasAttribute("file"))
				&& step.ParentNode is XmlElement; step = step.ParentNode)
			{
				path.Insert(0, step.Name);
			}

			string text = owner["name"]?.InnerText ?? owner.InnerText;
			return string.Join("/", path) + ": " + text;
		}

		private static readonly string[] ItemFields = { "translate", "page", "code", "advantage", "disadvantage" };

		// - mentor and paragon choices are left out: a translated one would show up as unreached
		private static IEnumerable<Entry> EntriesOf(string language, string fileName, XmlDocument english,
			XmlElement type, XmlElement child, HashSet<XmlNode> read)
		{
			Func<Kind, string, string, Entry> entry = (kind, key, xPath) =>
				new Entry(language, fileName, kind, key, Resolve(english, xPath, key));
			Func<Kind, string, Entry> crash = (kind, key) =>
				new Entry(language, fileName, kind, key + " (no name)", Outcome.Crashes);

			if (child["translate"] != null)
				read.Add(child["translate"]);

			if (ItemFields.Any(field => child[field] != null))
			{
				// - Load reads the name unguarded for each of these fields
				if (child["name"] == null)
					yield return crash(Kind.Item, type.Name + "/" + child.Name);
				else
				{
					string name = Decoded(child["name"]);
					yield return entry(Kind.Item, name,
						"/chummer/" + type.Name + "/" + child.Name + "[name = \"" + name + "\"]");
				}
			}

			if (child.Attributes["translate"] != null)
			{
				read.Add(child);
				string text = child.InnerXml.Replace("&amp;", "&");
				yield return entry(Kind.Text, text, "/chummer/" + type.Name + "/" + child.Name + "[. = \"" + text + "\"]");
			}

			// - the nested lookups below take InnerXml as is, without &amp; decoding
			if (fileName == "skills.xml" && child["specs"] != null)
			{
				foreach (XmlElement spec in child.SelectNodes("specs/spec[@translate]").OfType<XmlElement>())
				{
					read.Add(spec);
					string skill = child["name"]?.InnerXml;
					yield return skill == null
						? crash(Kind.Spec, spec.InnerXml)
						: entry(Kind.Spec, skill + ": " + spec.InnerXml, "/chummer/" + type.Name + "/skill[name = \""
							+ skill + "\"]/specs/spec[. = \"" + spec.InnerXml + "\"]");
				}
			}

			if (fileName == "metatypes.xml" && child["metavariants"] != null)
			{
				foreach (XmlElement metavariant in child.SelectNodes("metavariants/metavariant").OfType<XmlElement>())
				{
					if (metavariant["translate"] != null)
						read.Add(metavariant["translate"]);
					if (metavariant["translate"] == null && metavariant["altpage"] == null)
						continue;

					string metatype = child["name"]?.InnerXml;
					string name = metavariant["name"]?.InnerXml;
					if (metatype == null || name == null)
					{
						yield return crash(Kind.Metavariant, (metatype ?? "?") + "/" + (name ?? "?"));
						continue;
					}

					string key = metatype + "/" + name;
					Entry found = entry(Kind.Metavariant, key, "/chummer/metatypes/metatype[name = \"" + metatype
						+ "\"]/metavariants/metavariant[name = \"" + name + "\"]");

					// - clsXmlManager.cs:268 tests <altpage> but reads <page>: a hit without <page> throws
					bool pageCrashes = metavariant["altpage"] != null && metavariant["page"] == null
						&& found.Outcome == Outcome.Applied;
					yield return pageCrashes ? new Entry(language, fileName, Kind.Metavariant, key + " (altpage, no page)",
						Outcome.Crashes) : found;
				}
			}

			if (fileName == "martialarts.xml" && child["advantages"] != null)
			{
				foreach (XmlElement advantage in child.SelectNodes("advantages/advantage[@translate]")
					.OfType<XmlElement>())
				{
					read.Add(advantage);
					string art = child["name"]?.InnerXml;
					yield return art == null
						? crash(Kind.Advantage, advantage.InnerXml)
						: entry(Kind.Advantage, art + ": " + advantage.InnerXml,
							"/chummer/martialarts/martialart[name = \"" + art + "\"]/advantages/advantage[. = \""
							+ advantage.InnerXml + "\"]");
				}
			}
		}

		// - Load concatenates the key into a double-quoted XPath literal: a quote breaks the query
		private static Outcome Resolve(XmlDocument english, string xPath, string key)
		{
			if (key.Contains("\""))
				return Outcome.Crashes;

			return english.SelectSingleNode(xPath) == null ? Outcome.Dropped : Outcome.Applied;
		}

		private static string Decoded(XmlNode name)
		{
			return name.InnerXml.Replace("&amp;", "&");
		}

		private static XmlDocument Load(string path)
		{
			XmlDocument document = new XmlDocument { XmlResolver = null };
			document.Load(path);
			return document;
		}
	}
}
