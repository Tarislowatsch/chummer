using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Chummer.Tests.Kernel
{
	// - en-us.xml, the language the tests replay: parsed once for every reader
	public static class EnglishStrings
	{
		public sealed class Entry
		{
			public Entry(int number, string key, string text)
			{
				Number = number;
				Key = key;
				Text = text;
			}

			// - 1-based position among the <string> entries
			public int Number { get; }

			// - null when the element is absent
			public string Key { get; }

			public string Text { get; }
		}

		// - public so the detection tests can drive it with hand-built XML
		public static IReadOnlyList<Entry> EntriesIn(XmlDocument language)
		{
			return language.SelectNodes("/chummer/strings/string").Cast<XmlNode>()
				.Select((entry, index) => new Entry(index + 1, entry["key"]?.InnerText, entry["text"]?.InnerText))
				.ToArray();
		}

		// - kept as strings: XmlDocument is not safe under xUnit's parallel classes
		public static IReadOnlyList<Entry> All => CachedAll.Value;

		// - throws on a repeated key, like the dictionary Add in RefreshStrings
		public static IReadOnlyDictionary<string, string> TextByKey => CachedTextByKey.Value;

		private static readonly Lazy<IReadOnlyList<Entry>> CachedAll =
			new Lazy<IReadOnlyList<Entry>>(() => EntriesIn(Xml.Load(DataPaths.EnglishLanguageFile)));

		private static readonly Lazy<IReadOnlyDictionary<string, string>> CachedTextByKey =
			new Lazy<IReadOnlyDictionary<string, string>>(() => CachedAll.Value
				.Where(entry => entry.Key != null && entry.Text != null)
				.ToDictionary(entry => entry.Key, entry => entry.Text, StringComparer.Ordinal));
	}
}
