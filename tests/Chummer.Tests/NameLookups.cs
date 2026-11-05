using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Chummer.Tests
{
	// - a name shared across collections (Binding, Hardening) is harmless while every lookup is anchored
	// - a "//" or "*" step before a name filter would let one collection's item answer for another
	public static class NameLookups
	{
		// One XPath name filter in the code, with the path written before it.
		public sealed class Lookup
		{
			public Lookup(string site, string path, string dynamicPrefix)
			{
				Site = site;
				Path = path;
				DynamicPrefix = dynamicPrefix;
			}

			public string Site { get; }

			// - the literal text before the filter, "" when the path comes from code
			public string Path { get; }

			// - the code before the literal when Path is "", whitespace collapsed
			public string DynamicPrefix { get; }

			public bool IsUnanchored => Path.Contains("//") || Path.Contains("*");
		}

		// - a literal up to a predicate that compares name, also after "and"
		private static readonly Regex NameFilter =
			new Regex(@"""((?:[^""\\]|\\.)*?)\[(?:[^\]""]*\s)?name\s*=");

		// - public so the detection tests can drive it with hand-built source
		public static IEnumerable<Lookup> LookupsIn(string fileName, string[] lines)
		{
			string[] code = SourceLines.Of(lines).Code;

			for (int i = 0; i < code.Length; i++)
			{
				foreach (Match match in NameFilter.Matches(code[i]))
				{
					string path = match.Groups[1].Value;
					string prefix = path == "" ? PrefixBefore(code[i], match.Index) : null;
					yield return new Lookup(fileName + ":" + (i + 1), path, prefix);
				}
			}
		}

		// - from the call's opening paren to the literal
		private static string PrefixBefore(string line, int literalStart)
		{
			string before = line.Substring(0, literalStart);
			int paren = before.LastIndexOf('(');
			return Regex.Replace(before.Substring(paren + 1).Trim(), @"\s+", " ");
		}

		// - hand-traced: each variable resolves to one fixed collection and item step
		public static readonly IReadOnlyDictionary<string, string> KnownDynamicPrefixes =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "strXPath +", "cyberwares/cyberware or biowares/bioware, skills or knowledgeskills, metamagics or echoes" },
				{ "\"/chummer/\" + objNode.Name + \"/\" + objType.Name +", "translation pass over the file's own collections" },
				{ "\"/chummer/\" + objType.Name + \"/\" + objChild.Name +", "translation pass over the file's own collections" },
				{ "\"/chummer/\" + objChild.ParentNode.Name + \"/\" + objChild.Name +", "custom-content merge, same collection" },
				{ "objType.Name + \"/\" + objChild.Name +", "translation pass, relative to the language root" },
				{ "\"/chummer/\" + strType + \"s/\" + strType +", "cyberware or bioware" },
				{ "\"/chummer/\" + _strNode + \"s/\" + _strNode +", "cyberware or bioware" },
				{ "\"/chummer/\" + _strType + \"s/\" + _strType +", "cyberware or bioware" },
				{ "\"/chummer/\" + _strRoot + \"/\" + _strNode +", "metamagics/metamagic or echoes/echo" },
				{ "\"/chummer/\" + strParent + \"/\" + strChild +", "metamagics/metamagic or echoes/echo" },
			};

		public static IReadOnlyList<Lookup> All => Cached.Value;

		private static readonly Lazy<IReadOnlyList<Lookup>> Cached =
			new Lazy<IReadOnlyList<Lookup>>(() => DataPaths.ApplicationSourceFiles()
				.SelectMany(path => LookupsIn(Path.GetFileName(path), File.ReadAllLines(path)))
				.ToArray());
	}
}
