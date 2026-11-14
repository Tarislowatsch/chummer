using System;
using System.Collections.Generic;
using System.Linq;

namespace Chummer.Tests.Localization
{
	// - the reverse of the key checks: en-us.xml keys no reader reaches
	// - GetString is the only reader of the dictionary (clsLanguageManager.cs:440)
	public static class UiKeyReads
	{
		// - one implementation for the real code and the hand-built cases
		// - an exempt form's value is unknown: every key of its shape counts as read
		public static IEnumerable<string> UnreadKeys(IEnumerable<string> keys, IEnumerable<string> readKeys,
			IEnumerable<UiStringKeys.ComposedForm> exemptForms)
		{
			HashSet<string> read = new HashSet<string>(readKeys, StringComparer.Ordinal);
			UiStringKeys.ComposedForm[] exempt = exemptForms.ToArray();

			return keys
				.Where(key => !read.Contains(key))
				.Where(key => !exempt.Any(form => Fits(key, form)))
				.Distinct(StringComparer.Ordinal)
				.OrderBy(key => key, StringComparer.Ordinal);
		}

		private static bool Fits(string key, UiStringKeys.ComposedForm form)
		{
			return key.Length > form.Prefix.Length + form.Suffix.Length
				&& key.StartsWith(form.Prefix, StringComparison.Ordinal)
				&& key.EndsWith(form.Suffix, StringComparison.Ordinal);
		}

		// - the readers: literal calls, keys from a checked form, tags a lookup reads
		public static IEnumerable<string> ReadKeys()
		{
			IEnumerable<string> literal = UiStringKeys.AllCalls
				.Select(call => UiStringKeys.LiteralKey(call.Argument))
				.Where(key => key != null);

			IEnumerable<string> composed = UiStringKeys.ComposedForms
				.Where(form => form.Values != null)
				.SelectMany(form => form.Values().Select(value => form.Prefix + value + form.Suffix));

			IEnumerable<string> tags = UiTagKeys.ReadTags(UiTagKeys.AllAssignments, UiTagKeys.AllClassBases)
				.Select(assignment => assignment.Value);

			return literal.Concat(composed).Concat(tags);
		}

		public static IReadOnlyList<string> AllUnread => CachedUnread.Value;

		private static readonly Lazy<IReadOnlyList<string>> CachedUnread =
			new Lazy<IReadOnlyList<string>>(() => UnreadKeys(UiStringKeys.EnglishKeys, ReadKeys(),
					UiStringKeys.ComposedForms.Where(form => form.Values == null))
				.ToArray());
	}
}
