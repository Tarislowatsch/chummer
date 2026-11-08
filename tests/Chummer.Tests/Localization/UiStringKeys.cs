using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Chummer.Tests.Kernel;
using Chummer.Tests.Rulebook;

namespace Chummer.Tests.Localization
{
	// - GetString indexes a dictionary filled from en-us.xml only (clsLanguageManager.cs:140, :440)
	// - an unknown key throws KeyNotFoundException: most calls have no try
	public static class UiStringKeys
	{
		public static string EnglishFile => Path.Combine(DataPaths.ChummerLangDir, "en-us.xml");

		// - every <key>, duplicates kept: the dictionary's Add throws on a repeat
		public static IReadOnlyList<string> EnglishKeys => CachedEnglishKeys.Value;

		private static readonly Lazy<IReadOnlyList<string>> CachedEnglishKeys =
			new Lazy<IReadOnlyList<string>>(() =>
			{
				XmlDocument document = new XmlDocument { XmlResolver = null };
				document.Load(EnglishFile);
				return document.SelectNodes("/chummer/strings/string/key").Cast<XmlNode>()
					.Select(key => key.InnerText)
					.ToArray();
			});

		// - RefreshStrings reads objNode["key"] and objNode["text"] unguarded (clsLanguageManager.cs:139-140)
		// - public so the detection tests can drive it with hand-built XML
		public static IEnumerable<string> EntriesMissingKeyOrText(XmlDocument language)
		{
			return language.SelectNodes("/chummer/strings/string").Cast<XmlNode>()
				.Select((entry, index) => new { entry, index })
				.Where(item => item.entry["key"] == null || item.entry["text"] == null)
				.Select(item => "string #" + (item.index + 1) + " (key '"
					+ (item.entry["key"]?.InnerText ?? "(none)") + "')");
		}

		public static XmlDocument LoadEnglish()
		{
			XmlDocument document = new XmlDocument { XmlResolver = null };
			document.Load(EnglishFile);
			return document;
		}

		// One GetString call, with its argument as written.
		public sealed class Call
		{
			public Call(string site, string argument)
			{
				Site = site;
				Argument = argument;
			}

			public string Site { get; }

			// - whitespace collapsed: the hand-written table must not depend on layout
			public string Argument { get; }
		}

		private const string LanguageManagerFile = "clsLanguageManager.cs";

		private static readonly Regex QualifiedCall = new Regex(@"\bLanguageManager\.Instance\.GetString\(");

		// - inside LanguageManager the call has no receiver
		// - the lookbehind keeps resources.GetString out
		private static readonly Regex BareCall = new Regex(@"(?<![\w.])GetString\(");

		private static readonly Regex Declaration = new Regex(@"\bstring\s+GetString\(");

		// - public so the detection tests can drive it with hand-built source
		// - comments stripped first: a commented-out call is not a call
		public static IEnumerable<Call> CallsIn(string fileName, string[] lines)
		{
			string[] code = SourceLines.Of(lines).Code;
			Regex call = string.Equals(fileName, LanguageManagerFile, StringComparison.Ordinal)
				? BareCall
				: QualifiedCall;

			for (int i = 0; i < code.Length; i++)
			{
				if (Declaration.IsMatch(code[i]))
					continue;

				foreach (Match match in call.Matches(code[i]))
				{
					yield return new Call(fileName + ":" + (i + 1),
						ArgumentAt(code[i], match.Index + match.Length));
				}
			}
		}

		// - up to the paren that closes the call: a nested ToString() stays inside
		// - an argument broken across lines comes back cut off and fails the classification guard
		private static string ArgumentAt(string line, int start)
		{
			int depth = 0;
			bool inString = false;
			for (int i = start; i < line.Length; i++)
			{
				char c = line[i];
				if (c == '"' && line[i - 1] != '\\')
					inString = !inString;
				else if (!inString && c == '(')
					depth++;
				else if (!inString && c == ')')
				{
					if (depth == 0)
						return Regex.Replace(line.Substring(start, i - start).Trim(), @"\s+", " ");
					depth--;
				}
			}

			return Regex.Replace(line.Substring(start).Trim(), @"\s+", " ");
		}

		public static IReadOnlyList<Call> AllCalls => CachedCalls.Value;

		private static readonly Lazy<IReadOnlyList<Call>> CachedCalls =
			new Lazy<IReadOnlyList<Call>>(() => DataPaths.ApplicationSourceFiles()
				.SelectMany(path => CallsIn(Path.GetFileName(path), File.ReadAllLines(path)))
				.ToArray());

		private static readonly Regex Literal = new Regex(@"^""([^""\\]*)""$");

		// - null when the argument is anything but one string literal
		public static string LiteralKey(string argument)
		{
			Match match = Literal.Match(argument);
			return match.Success ? match.Groups[1].Value : null;
		}

		// - a control's Tag used as the key, a separate concern from the keys written in code
		private static readonly Regex TagLookup = new Regex(@"^\w+\.Tag\.ToString\(\)$");

		public static bool IsTagLookup(string argument)
		{
			return TagLookup.IsMatch(argument);
		}

		// One argument shape that builds its key at run time.
		public sealed class ComposedForm
		{
			public ComposedForm(string argument, string prefix, string suffix, string source,
				Func<IEnumerable<string>> values)
			{
				Argument = argument;
				Prefix = prefix;
				Suffix = suffix;
				Source = source;
				Values = values;
			}

			public string Argument { get; }

			public string Prefix { get; }

			public string Suffix { get; }

			// - where the variable part comes from, or why the form needs no check
			public string Source { get; }

			// - null for an exempt form
			public Func<IEnumerable<string>> Values { get; }
		}

		// - hand-written: the variable part's origin has to be traced through the code
		public static readonly IReadOnlyList<ComposedForm> ComposedForms = new[]
		{
			// - frmSelectWeaponAccessory.cs:91 and six copies in frmCreate/frmCareer, split on '/'
			// - every file: PACKS kits copy packs.xml's <mount> too (frmCreate.cs:21063, :21429)
			Form("\"String_Mount\" + strCurrentMount", "String_Mount", "",
				"every data and pack file //mount",
				() => AllDataValues("//mount").SelectMany(MountParts)),
			// SkillControl.cs:649 and :653
			Form("\"String_Attribute\" + _objSkill.Attribute + \"Short\"", "String_Attribute", "Short",
				"skills.xml //skill/attribute", () => DataValues("skills.xml", "//skill/attribute")),
			// Skill.DisplayAttribute, clsUnique.cs:3138
			Form("\"String_Attribute\" + Attribute + \"Short\"", "String_Attribute", "Short",
				"skills.xml //skill/attribute", () => DataValues("skills.xml", "//skill/attribute")),
			// - Skill.Print (clsUnique.cs:1871) passes the skill's attribute
			// - frmSelectAttribute.cs:193 gets bonus lists (clsImprovement.cs:1416, :1502)
			// - frmCreateImprovement only adds and removes fixed attributes in code
			Form("\"String_Attribute\" + strAttribute + \"Short\"", "String_Attribute", "Short",
				"skills.xml //skill/attribute and every data and pack file's selectattribute/swapskillattribute",
				() => DataValues("skills.xml", "//skill/attribute").Concat(AllDataValues(
					"//bonus/selectattribute/attribute | //bonus/swapskillattribute/attribute"))),
			// frmSelectPACKSKit.cs:127, element names upper-cased
			Form("\"String_Attribute\" + objXmlAttribute.Name.ToUpper() + \"Short\"", "String_Attribute", "Short",
				"packs.xml //attributes/* element names",
				() => Nodes("packs.xml", "//attributes/*")
					.Select(node => node.Name.ToUpperInvariant())),
			// clsUnique.cs:7271
			Form("\"String_\" + _objContactType.ToString()", "String_", "",
				"enum ContactType in clsUnique.cs",
				() => EnumMembers(File.ReadAllText(Path.Combine(DataPaths.RepoRoot, "Chummer", "clsUnique.cs")),
					"ContactType")),
			// - both inside a try that falls back to the raw value
			Form("\"String_Attribute\" + _strExtra + \"Short\"", "String_Attribute", "Short",
				"exempt: inside try, clsUnique.cs:7726", null),
			Form("\"String_Attribute\" + _strLocation + \"Short\"", "String_Attribute", "Short",
				"exempt: inside try, clsEquipment.cs:3019", null),
		};

		private static ComposedForm Form(string argument, string prefix, string suffix, string source,
			Func<IEnumerable<string>> values)
		{
			return new ComposedForm(argument, prefix, suffix, source, values);
		}

		// - empty segments skipped, as every call site does (strCurrentMount != "")
		private static IEnumerable<string> MountParts(string mount)
		{
			return mount.Split('/').Where(part => part != "");
		}

		private static IEnumerable<string> AllDataValues(string xpath)
		{
			return Directory.EnumerateFiles(DataPaths.ChummerDataDir, "*.xml", SearchOption.TopDirectoryOnly)
				.SelectMany(path => DataValues(Path.GetFileName(path), xpath));
		}

		private static IEnumerable<string> DataValues(string fileName, string xpath)
		{
			return Nodes(fileName, xpath).Select(node => node.InnerText);
		}

		// - the base file plus every pack file Load would merge into it once copied in
		private static IEnumerable<XmlNode> Nodes(string fileName, string xpath)
		{
			return new[] { Path.Combine(DataPaths.ChummerDataDir, fileName) }
				.Concat(CustomContent.PairedPackFiles()
					.Where(pack => string.Equals(pack.BaseFileName, fileName, StringComparison.Ordinal))
					.Select(pack => pack.FilePath))
				.SelectMany(path => CustomContent.Load(path).SelectNodes(xpath).Cast<XmlNode>());
		}

		// - public so the detection tests can drive it with hand-built source
		// - Enum.ToString() yields the member name, never the number after '='
		public static IEnumerable<string> EnumMembers(string source, string enumName)
		{
			Match body = Regex.Match(source, @"\benum\s+" + Regex.Escape(enumName) + @"\s*\{([^}]*)\}");
			if (!body.Success)
				throw new InvalidOperationException("enum " + enumName + " not found.");

			return body.Groups[1].Value.Split(',')
				.Select(member => member.Split('=')[0].Trim())
				.Where(member => member != "");
		}

		// - one implementation for the real data and the hand-built cases
		public static IEnumerable<string> MissingKeys(ComposedForm form, IEnumerable<string> values,
			IReadOnlyCollection<string> keys)
		{
			HashSet<string> known = new HashSet<string>(keys, StringComparer.Ordinal);
			return values
				.Select(value => form.Prefix + value + form.Suffix)
				.Distinct(StringComparer.Ordinal)
				.Where(key => !known.Contains(key));
		}
	}
}