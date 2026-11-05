using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace Chummer.Tests
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

		private static IReadOnlyDictionary<string, IReadOnlyList<Entry>> LoadEntries()
		{
			Dictionary<string, XmlDocument> english = new Dictionary<string, XmlDocument>(StringComparer.Ordinal);
			Func<string, XmlDocument> englishFor = fileName =>
			{
				if (!english.ContainsKey(fileName))
				{
					string path = Path.Combine(DataPaths.ChummerDataDir, fileName);
					english[fileName] = File.Exists(path) ? Load(path) : null;
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

			foreach (XmlElement block in languageDoc.SelectNodes("/chummer/chummer[@file]").OfType<XmlElement>())
			{
				string fileName = block.GetAttribute("file");
				XmlDocument englishDoc = englishFor(fileName);

				// - Load only ever asks for files that exist: a block for any other is never read
				entries.Add(new Entry(language, fileName, Kind.File, fileName,
					englishDoc == null ? Outcome.Dropped : Outcome.Applied));
				if (englishDoc == null)
					continue;

				foreach (XmlElement type in block.ChildNodes.OfType<XmlElement>())
				{
					foreach (XmlElement child in type.ChildNodes.OfType<XmlElement>())
						entries.AddRange(EntriesOf(language, fileName, englishDoc, type, child));
				}
			}

			return entries;
		}

		private static readonly string[] ItemFields = { "translate", "page", "code", "advantage", "disadvantage" };

		// - mentor and paragon choices are left out: no language file translates one
		private static IEnumerable<Entry> EntriesOf(string language, string fileName, XmlDocument english,
			XmlElement type, XmlElement child)
		{
			Func<Kind, string, string, Entry> entry = (kind, key, xPath) =>
				new Entry(language, fileName, kind, key, Resolve(english, xPath, key));

			if (ItemFields.Any(field => child[field] != null))
			{
				// - Load reads the name unguarded for each of these fields
				if (child["name"] == null)
					yield return new Entry(language, fileName, Kind.Item, type.Name + "/" + child.Name + " (no name)",
						Outcome.Crashes);
				else
				{
					string name = Decoded(child["name"]);
					yield return entry(Kind.Item, name,
						"/chummer/" + type.Name + "/" + child.Name + "[name = \"" + name + "\"]");
				}
			}

			if (child.Attributes["translate"] != null)
			{
				string text = child.InnerXml.Replace("&amp;", "&");
				yield return entry(Kind.Text, text, "/chummer/" + type.Name + "/" + child.Name + "[. = \"" + text + "\"]");
			}

			// - the nested lookups below take InnerXml as is, without &amp; decoding
			if (fileName == "skills.xml" && child["specs"] != null)
			{
				foreach (XmlElement spec in child.SelectNodes("specs/spec[@translate]").OfType<XmlElement>())
				{
					string skill = child["name"]?.InnerXml;
					yield return skill == null
						? new Entry(language, fileName, Kind.Spec, spec.InnerXml + " (no skill name)", Outcome.Crashes)
						: entry(Kind.Spec, skill + ": " + spec.InnerXml, "/chummer/" + type.Name + "/skill[name = \""
							+ skill + "\"]/specs/spec[. = \"" + spec.InnerXml + "\"]");
				}
			}

			// - Load reads a metavariant page only under <altpage>: no language file has one
			if (fileName == "metatypes.xml" && child["metavariants"] != null)
			{
				foreach (XmlElement metavariant in child.SelectNodes("metavariants/metavariant[translate]")
					.OfType<XmlElement>())
				{
					string metatype = child["name"].InnerXml;
					string name = metavariant["name"].InnerXml;
					yield return entry(Kind.Metavariant, metatype + "/" + name,
						"/chummer/metatypes/metatype[name = \"" + metatype + "\"]/metavariants/metavariant[name = \""
						+ name + "\"]");
				}
			}

			if (fileName == "martialarts.xml" && child["advantages"] != null)
			{
				foreach (XmlElement advantage in child.SelectNodes("advantages/advantage[@translate]")
					.OfType<XmlElement>())
				{
					string art = child["name"].InnerXml;
					yield return entry(Kind.Advantage, art + ": " + advantage.InnerXml,
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
