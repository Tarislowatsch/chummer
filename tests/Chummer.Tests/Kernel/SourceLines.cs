using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Chummer.Tests.Kernel
{
	// - a commented-out read must not count: that would hide what the code really does
	// - Bare also drops literals: a "{0}" placeholder must not move the brace depth
	public sealed class SourceLines
	{
		public SourceLines(string[] code, string[] bare)
		{
			Code = code;
			Bare = bare;
			joinedCode = new Lazy<string>(() => string.Join("\n", Code));
		}

		// Comments removed, literals kept: tag and field names are literals.
		public string[] Code { get; }

		// - Code as one string for multi-line regexes, joined once per instance
		public string JoinedCode => joinedCode.Value;

		private readonly Lazy<string> joinedCode;

		public string[] Bare { get; }

		// - a lexer, not a regex: "//" inside a string literal is no comment
		public static SourceLines Of(string[] lines)
		{
			string[] code = new string[lines.Length];
			string[] bare = new string[lines.Length];
			bool inBlockComment = false;
			bool inVerbatim = false;

			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i];
				StringBuilder kept = new StringBuilder();
				StringBuilder stripped = new StringBuilder();
				char quote = '\0';

				for (int j = 0; j < line.Length; j++)
				{
					char current = line[j];
					char next = j + 1 < line.Length ? line[j + 1] : '\0';

					if (inBlockComment)
					{
						if (current == '*' && next == '/')
						{
							inBlockComment = false;
							j++;
						}
						continue;
					}

					if (inVerbatim)
					{
						kept.Append(current);
						if (current == '"' && next == '"')
						{
							kept.Append(next);
							j++;
						}
						else if (current == '"')
						{
							inVerbatim = false;
						}
						continue;
					}

					if (quote != '\0')
					{
						kept.Append(current);
						if (current == '\\' && next != '\0')
						{
							kept.Append(next);
							j++;
						}
						else if (current == quote)
						{
							quote = '\0';
						}
						continue;
					}

					if (current == '/' && next == '/')
						break;

					if (current == '/' && next == '*')
					{
						inBlockComment = true;
						j++;
						continue;
					}

					if (current == '@' && next == '"')
					{
						kept.Append(current).Append(next);
						inVerbatim = true;
						j++;
						continue;
					}

					if (current == '"' || current == '\'')
					{
						kept.Append(current);
						quote = current;
						continue;
					}

					kept.Append(current);
					stripped.Append(current);
				}

				code[i] = kept.ToString();
				bare[i] = stripped.ToString();
			}

			return new SourceLines(code, bare);
		}

		// - extent by brace depth, like the Create fingerprints in RequiredFields
		public SourceLines BlockFrom(int start)
		{
			int depth = 0;
			bool opened = false;
			int end = start;

			for (; end < Bare.Length; end++)
			{
				string bare = Bare[end];
				depth += bare.Count(character => character == '{') - bare.Count(character => character == '}');
				opened |= bare.IndexOf('{') >= 0;

				if (opened && depth <= 0)
					break;
			}

			int count = Math.Min(end, Bare.Length - 1) - start + 1;
			return new SourceLines(
				Code.Skip(start).Take(count).ToArray(),
				Bare.Skip(start).Take(count).ToArray());
		}

		// - whitespace-collapsed: a reindent must not move a pinned hash
		public string CodeFingerprint()
		{
			return Fingerprint(Code
				.Select(line => Regex.Replace(line.Trim(), @"\s+", " "))
				.Where(line => line.Length > 0));
		}

		internal static string Fingerprint(IEnumerable<string> lines)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines)));

				// - 16 hex characters: collision-safe enough while the pinned table stays readable
				return BitConverter.ToString(hash).Replace("-", string.Empty).Substring(0, 16);
			}
		}
	}
}
