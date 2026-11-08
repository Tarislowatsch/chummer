using System;
using System.Collections.Generic;
using System.Linq;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Localization
{
	// - deliberately no allowlist: every key resolves today
	public class UiStringKeyTests
	{
		// - RefreshStrings adds every key to a Dictionary (clsLanguageManager.cs:140)
		// - a repeat throws there: the catch exits the application at startup
		[Fact]
		public void EnglishKeysAreUnique()
		{
			string[] repeated = UiStringKeys.EnglishKeys
				.GroupBy(key => key, StringComparer.Ordinal)
				.Where(group => group.Count() > 1)
				.Select(group => group.Key)
				.ToArray();

			Assert.True(repeated.Length == 0,
				"en-us.xml declares these keys more than once:\n  " + string.Join("\n  ", repeated));
		}

		// - the same catch exits the application when an entry lacks <key> or <text>
		[Fact]
		public void EveryEnglishEntryHasKeyAndText()
		{
			string[] incomplete = UiStringKeys.EntriesMissingKeyOrText(UiStringKeys.LoadEnglish()).ToArray();

			Assert.True(incomplete.Length == 0,
				"These en-us.xml entries lack <key> or <text>:\n  " + string.Join("\n  ", incomplete));
		}

		[Fact]
		public void EveryLiteralKeyExists()
		{
			HashSet<string> keys = new HashSet<string>(UiStringKeys.EnglishKeys, StringComparer.Ordinal);

			var missing = UiStringKeys.AllCalls
				.Select(call => new { call.Site, Key = UiStringKeys.LiteralKey(call.Argument) })
				.Where(call => call.Key != null && !keys.Contains(call.Key))
				.ToArray();

			if (missing.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					missing.Length + " GetString call(s) name a key en-us.xml does not declare",
					missing, call => call.Site + " '" + call.Key + "'"));
			}
		}

		public static IEnumerable<object[]> CheckedComposedForms()
		{
			return UiStringKeys.ComposedForms
				.Where(form => form.Values != null)
				.Select(form => new object[] { form.Argument });
		}

		[Theory]
		[MemberData(nameof(CheckedComposedForms))]
		public void EveryComposedKeyExists(string argument)
		{
			UiStringKeys.ComposedForm form = UiStringKeys.ComposedForms.Single(entry => entry.Argument == argument);
			string[] values = form.Values().ToArray();

			// - an empty source would make the check vacuous: a moved XPath must fail, not pass
			Assert.True(values.Length > 0, form.Source + " yields no values for " + argument);

			string[] missing = UiStringKeys.MissingKeys(form, values, UiStringKeys.EnglishKeys).ToArray();

			Assert.True(missing.Length == 0,
				"Keys built by " + argument + " from " + form.Source + " are missing from en-us.xml:\n  "
				+ string.Join("\n  ", missing));
		}

		// - the table is hand-written: a new composed call would otherwise go unchecked
		[Fact]
		public void EveryCallIsLiteralComposedOrTag()
		{
			HashSet<string> known = new HashSet<string>(
				UiStringKeys.ComposedForms.Select(form => form.Argument), StringComparer.Ordinal);

			string[] unclassified = UiStringKeys.AllCalls
				.Where(call => UiStringKeys.LiteralKey(call.Argument) == null
					&& !known.Contains(call.Argument)
					&& !UiStringKeys.IsTagLookup(call.Argument))
				.Select(call => call.Site + " " + call.Argument)
				.ToArray();

			Assert.True(unclassified.Length == 0,
				"These GetString calls build their key in a way " + nameof(UiStringKeys.ComposedForms)
				+ " does not know. Trace where the value comes from and add a form:\n  "
				+ string.Join("\n  ", unclassified));
		}

		[Fact]
		public void EveryComposedFormIsStillCalled()
		{
			HashSet<string> called = new HashSet<string>(
				UiStringKeys.AllCalls.Select(call => call.Argument), StringComparer.Ordinal);

			string[] stale = UiStringKeys.ComposedForms
				.Where(form => !called.Contains(form.Argument))
				.Select(form => form.Argument)
				.ToArray();

			Assert.True(stale.Length == 0,
				"No call uses these forms any more, remove them from " + nameof(UiStringKeys.ComposedForms)
				+ ":\n  " + string.Join("\n  ", stale));
		}
	}
}