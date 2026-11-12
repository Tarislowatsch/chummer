using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Chummer.Tests.Kernel
{
	// - members by "Class.Member", over the lexed corpus or one lexed file
	public static class SourceMembers
	{
		private static readonly Regex AnyClass =
			new Regex(@"^\s*public\s+(?:(?:sealed|partial|abstract)\s+)*class\s+(\w+)", RegexOptions.Compiled);

		private static readonly Regex MemberDeclaration = new Regex(
			@"^\s*(?:public|private|protected|internal)\s+(?:(?:static|virtual|override|new)\s+)*[\w<>\[\]]+\s+(\w+)\s*(?:\(|$)",
			RegexOptions.Compiled);

		// - "Class.Member" -> hash of the member's comment-free body, for every key asked for
		// - a port is only as good as its last reading: a changed hash means read it again
		// - a key that matches nothing maps to "(missing)" and fails the pin
		public static IReadOnlyDictionary<string, string> Fingerprints(IEnumerable<string> keys)
		{
			Dictionary<string, string> fingerprints = keys.Distinct(StringComparer.Ordinal)
				.ToDictionary(key => key, key => "(missing)", StringComparer.Ordinal);

			foreach (SourceCorpus.SourceFile file in SourceCorpus.Files)
			{
				SourceLines source = file.Source;
				string className = null;

				for (int i = 0; i < source.Code.Length; i++)
				{
					Match declaration = AnyClass.Match(source.Code[i]);
					if (declaration.Success)
					{
						className = declaration.Groups[1].Value;
						continue;
					}

					Match member = MemberDeclaration.Match(source.Code[i]);
					if (className == null || !member.Success)
						continue;

					string key = className + "." + member.Groups[1].Value;
					if (!fingerprints.ContainsKey(key))
						continue;

					if (fingerprints[key] != "(missing)")
						throw new InvalidOperationException(key + " is declared twice, so its port is ambiguous.");

					fingerprints[key] = source.BlockFrom(i).CodeFingerprint();
				}
			}

			return fingerprints;
		}

		// - "Class.Member" of the last declaration above each line, null before the first
		// - a constructor matches no declaration: its lines count to the member above
		public static string[] EnclosingMembers(SourceLines source)
		{
			string[] members = new string[source.Code.Length];
			string className = null;
			string member = null;

			for (int i = 0; i < source.Code.Length; i++)
			{
				Match declaration = AnyClass.Match(source.Code[i]);
				if (declaration.Success)
				{
					className = declaration.Groups[1].Value;
					member = null;
				}
				else if (MemberDeclaration.Match(source.Code[i]) is Match match && match.Success && className != null)
				{
					member = className + "." + match.Groups[1].Value;
				}

				members[i] = member;
			}

			return members;
		}
	}
}
