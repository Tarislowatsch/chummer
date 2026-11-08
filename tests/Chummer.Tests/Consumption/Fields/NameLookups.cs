using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Chummer.Tests.Kernel;

namespace Chummer.Tests.Consumption.Fields
{
	// - a name shared across collections (Binding, Hardening) is harmless while every lookup is anchored
	// - a "//", "*" or axis step before a name filter lets one collection's item answer for another
	public static class NameLookups
	{
		// One piece of a source line: a string literal (unescaped) or the code between literals.
		public sealed class Token
		{
			public Token(bool isLiteral, string text)
			{
				IsLiteral = isLiteral;
				Text = text;
			}

			public bool IsLiteral { get; }

			public string Text { get; }
		}

		// - a lexer, not a regex: \" inside a filter must not end the literal
		// - char literals stay code: '"' opens no literal
		public static IReadOnlyList<Token> Tokenize(string line)
		{
			List<Token> tokens = new List<Token>();
			StringBuilder current = new StringBuilder();
			int i = 0;

			while (i < line.Length)
			{
				char c = line[i];
				if (c == '\'')
				{
					int end = line.IndexOf('\'', i + (i + 1 < line.Length && line[i + 1] == '\\' ? 3 : 2));
					end = end < 0 ? line.Length - 1 : end;
					current.Append(line, i, end - i + 1);
					i = end + 1;
					continue;
				}

				if (c != '"')
				{
					current.Append(c);
					i++;
					continue;
				}

				bool verbatim = current.Length > 0 && current[current.Length - 1] == '@';
				if (verbatim)
					current.Length--;
				Flush(tokens, current, false);

				i++;
				while (i < line.Length)
				{
					if (!verbatim && line[i] == '\\' && i + 1 < line.Length)
					{
						current.Append(line[i + 1]);
						i += 2;
					}
					else if (verbatim && line[i] == '"' && i + 1 < line.Length && line[i + 1] == '"')
					{
						current.Append('"');
						i += 2;
					}
					else if (line[i] == '"')
					{
						i++;
						break;
					}
					else
					{
						current.Append(line[i++]);
					}
				}

				tokens.Add(new Token(true, current.ToString()));
				current.Clear();
			}

			Flush(tokens, current, false);
			return tokens;
		}

		private static void Flush(List<Token> tokens, StringBuilder current, bool isLiteral)
		{
			if (current.Length > 0)
				tokens.Add(new Token(isLiteral, current.ToString()));
			current.Clear();
		}

		// One XPath name filter in the code, with the path in front of it.
		public sealed class Lookup
		{
			public Lookup(string site, string path, bool isComputed)
			{
				Site = site;
				Path = path;
				IsComputed = isComputed;
			}

			public string Site { get; }

			public string File => Site.Substring(0, Site.LastIndexOf(':'));

			// - literal text as written, code in braces, e.g. "/chummer/{objType.Name}/skill"
			public string Path { get; }

			// - code in the path, or a filter built apart from its path
			public bool IsComputed { get; }

			public bool IsUnanchored => IsUnanchoredPath(Path);
		}

		// - "name =" as a comparison, not "@name", "contains(name" or "skillname ="
		private static readonly Regex NameComparison = new Regex(@"(?<![\w@.\-])name\s*=(?!=)");

		// - public so the detection tests can drive it with hand-built source
		public static IEnumerable<Lookup> LookupsIn(string fileName, SourceLines source)
		{
			string[] code = source.Code;

			for (int i = 0; i < code.Length; i++)
			{
				IReadOnlyList<Token> tokens = Tokenize(code[i]);
				for (int k = 0; k < tokens.Count; k++)
				{
					if (!tokens[k].IsLiteral)
						continue;

					foreach (Match match in NameComparison.Matches(tokens[k].Text))
						yield return LookupAt(fileName + ":" + (i + 1), tokens, k, match.Index);
				}
			}
		}

		// - the expression starts after the call's paren or an assignment, as far back as the line goes
		private static Lookup LookupAt(string site, IReadOnlyList<Token> tokens, int literal, int nameIndex)
		{
			List<string> parts = new List<string> { tokens[literal].Text.Substring(0, nameIndex) };
			string target = null;
			int depth = 0;

			for (int k = literal - 1; k >= 0; k--)
			{
				if (tokens[k].IsLiteral)
				{
					parts.Insert(0, tokens[k].Text);
					continue;
				}

				string text = tokens[k].Text;
				int start = ExpressionStart(text, ref depth, out bool assignment);
				parts.Insert(0, CodePart(start < 0 ? text : text.Substring(start)));
				if (start < 0)
					continue;

				if (assignment)
					target = Regex.Match(text.Substring(0, start), @"(\w+)\s*[+]?=\s*$").Groups[1].Value;
				break;
			}

			string template = string.Concat(parts);
			int open = OpenBracket(template);
			if (open < 0)
				return new Lookup(site, "(filter built in " + (target ?? "?") + ")", true);

			string path = template.Substring(0, open);
			return new Lookup(site, path, StepsOf(path).Contains("{"));
		}

		// - the path without its closed predicates: a value compared there picks no collection
		private static string StepsOf(string path)
		{
			StringBuilder steps = new StringBuilder();
			int brackets = 0;
			int braces = 0;
			foreach (char c in path)
			{
				if (c == '{')
					braces++;
				else if (c == '}')
					braces--;
				else if (braces == 0 && c == '[')
					brackets++;
				else if (braces == 0 && c == ']')
				{
					brackets--;
					continue;
				}

				if (brackets == 0)
					steps.Append(c);
			}

			return steps.ToString();
		}

		// - the concatenation pluses belong to the expression, not to the code
		private static string CodePart(string code)
		{
			string trimmed = code.Trim().Trim('+').Trim();
			return trimmed.Length == 0 ? "" : "{" + trimmed + "}";
		}

		// - index after the paren, "=", "+=" or "," that opens the expression, or -1
		private static int ExpressionStart(string code, ref int depth, out bool assignment)
		{
			assignment = false;
			for (int i = code.Length - 1; i >= 0; i--)
			{
				char c = code[i];
				if (c == ')')
					depth++;
				else if (c == '(' && depth > 0)
					depth--;
				else if (c == '(' || (c == ',' && depth == 0))
					return i + 1;
				else if (c == '=' && depth == 0 && (i == 0 || "=!<>".IndexOf(code[i - 1]) < 0)
					&& (i + 1 >= code.Length || code[i + 1] != '='))
				{
					assignment = true;
					return i + 1;
				}
			}

			return -1;
		}

		// - the "[" still open at the end, skipping code in braces
		private static int OpenBracket(string template)
		{
			Stack<int> open = new Stack<int>();
			int braces = 0;
			for (int i = 0; i < template.Length; i++)
			{
				char c = template[i];
				if (c == '{')
					braces++;
				else if (c == '}')
					braces--;
				else if (braces == 0 && c == '[')
					open.Push(i);
				else if (braces == 0 && c == ']' && open.Count > 0)
					open.Pop();
			}

			return open.Count > 0 ? open.Peek() : -1;
		}

		// - "//" after a URI scheme is no step
		private static readonly Regex UnanchoredLiteral = new Regex(@"(?<!:/*)//|::|/\*");

		// - code in braces is opaque: what it holds is the literal guard's concern
		// - a path may also start with a bare "*"
		public static bool IsUnanchoredPath(string path)
		{
			string steps = Regex.Replace(path, @"\{[^}]*\}", "x");
			return IsUnanchoredLiteral(steps) || steps.StartsWith("*");
		}

		// - a lone "*" in a literal is a cost multiplier, not a step
		public static bool IsUnanchoredLiteral(string literal)
		{
			return UnanchoredLiteral.IsMatch(literal);
		}

		// - "file path" -> calls, hand-traced: each resolves to one fixed collection and item step
		public static readonly IReadOnlyDictionary<string, int> ComputedPaths =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				// cyberwares/cyberware or biowares/bioware
				{ "clsEquipment.cs {strXPath}", 2 },
				// skills or knowledgeskills, metamagics or echoes
				{ "clsUnique.cs {strXPath}", 3 },
				// override merge (:168), same collection as the override file
				{ "clsXmlManager.cs /chummer/{objNode.Name}/{objType.Name}", 1 },
				// translation pass (:193 ff.) and Verify (:659), the file's own collections
				{ "clsXmlManager.cs /chummer/{objType.Name}/{objChild.Name}", 6 },
				// translation pass for skill specs (:244)
				{ "clsXmlManager.cs /chummer/{objType.Name}/skill", 1 },
				// custom-content merge (:353), same collection
				{ "clsXmlManager.cs /chummer/{objChild.ParentNode.Name}/{objChild.Name}", 1 },
				// Verify (:456), relative to the language root
				{ "clsXmlManager.cs {objType.Name}/{objChild.Name}", 1 },
				// cyberware or bioware
				{ "frmCareer.cs /chummer/{strType}s/{strType}", 2 },
				{ "frmCreate.cs /chummer/{strType}s/{strType}", 2 },
				{ "frmSelectCyberware.cs /chummer/{_strNode}s/{_strNode}", 4 },
				{ "frmSelectCyberwareSuite.cs /chummer/{_strType}s/{_strType}", 1 },
				// metamagics/metamagic or echoes/echo
				{ "frmSelectMetamagic.cs /chummer/{_strRoot}/{_strNode}", 1 },
				{ "frmSelectMetamagic.cs /chummer/{strParent}/{strChild}", 2 },
				// - built apart, then used in "/chummer/skills/skill[" + strFilter + "]" (:71)
				{ "frmSelectSkill.cs (filter built in strFilter)", 1 },
			};

		public static IReadOnlyList<Lookup> All => Cached.Value;

		private static readonly Lazy<IReadOnlyList<Lookup>> Cached =
			new Lazy<IReadOnlyList<Lookup>>(() => SourceCorpus.Files
				.SelectMany(file => LookupsIn(file.Name, file.Source))
				.ToArray());

		// One string literal anywhere in the code.
		public sealed class Literal
		{
			public Literal(string site, string text)
			{
				Site = site;
				Text = text;
			}

			public string Site { get; }

			public string File => Site.Substring(0, Site.LastIndexOf(':'));

			public string Text { get; }
		}

		public static IEnumerable<Literal> LiteralsIn(string fileName, SourceLines source)
		{
			string[] code = source.Code;
			for (int i = 0; i < code.Length; i++)
			{
				foreach (Token token in Tokenize(code[i]).Where(token => token.IsLiteral))
					yield return new Literal(fileName + ":" + (i + 1), token.Text);
			}
		}

		// - "file literal" -> count of "/*" steps that carry no name filter
		// - each walks every collection of a file on purpose: load, merge, kit editor, manifest
		public static readonly IReadOnlyDictionary<string, int> KnownWildcardLiterals =
			new Dictionary<string, int>(StringComparer.Ordinal)
			{
				{ "clsXmlManager.cs /chummer/*", 3 },
				{ "frmCreatePACKSKit.cs /chummer/*", 1 },
				{ "frmSelectPACKSKit.cs /chummer/packs/*", 1 },
				{ "frmUpdate.cs /manifest/*", 1 },
			};

		public static IReadOnlyList<Literal> AllLiterals => CachedLiterals.Value;

		private static readonly Lazy<IReadOnlyList<Literal>> CachedLiterals =
			new Lazy<IReadOnlyList<Literal>>(() => SourceCorpus.Files
				.SelectMany(file => LiteralsIn(file.Name, file.Source))
				.ToArray());
	}
}
