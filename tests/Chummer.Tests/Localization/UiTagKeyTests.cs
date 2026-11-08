using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - deliberately no allowlist: every tag a lookup reads has a key today
	public class UiTagKeyTests
	{
		private static Type TypeOf(UiTagKeys.Assignment assignment)
		{
			return UiTagKeys.Resolve(assignment.DeclaredType, UiTagKeys.AllClassBases);
		}

		[Fact]
		public void EveryReadTagIsAKey()
		{
			UiTagKeys.Assignment[] missing = UiTagKeys.MissingKeys(UiTagKeys.AllAssignments,
				UiTagKeys.AllClassBases, UiStringKeys.EnglishKeys).ToArray();

			if (missing.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					missing.Length + " tag(s) are looked up but en-us.xml does not declare them",
					missing, assignment => assignment.Site + " " + assignment.Receiver + " '" + assignment.Value + "'"));
			}
		}

		// - an unresolved type would drop out of the check above unseen
		[Fact]
		public void EveryLiteralTagHasAKnownType()
		{
			string[] unresolved = UiTagKeys.AllAssignments
				.Where(assignment => assignment.Value != null && TypeOf(assignment) == null)
				.Select(assignment => assignment.Site + " " + assignment.Receiver
					+ " (declared as " + (assignment.DeclaredType ?? "?") + ")")
				.ToArray();

			Assert.True(unresolved.Length == 0,
				"These tag writes have no WinForms type to match against the lookups:\n  "
				+ string.Join("\n  ", unresolved));
		}

		// - a computed tag skips the key check: only run-time data may be computed
		[Fact]
		public void EveryDesignerTagIsALiteral()
		{
			string[] computed = UiTagKeys.AllAssignments
				.Where(assignment => UiTagKeys.IsDesignerFile(assignment.Site) && assignment.Value == null)
				.Select(assignment => assignment.Site + " " + assignment.Receiver)
				.ToArray();

			Assert.True(computed.Length == 0,
				"These designer tags are not string literals:\n  " + string.Join("\n  ", computed));
		}

		// - run-time ids and objects stay allowed, a key built from a literal does not
		[Fact]
		public void NoTagIsComposedFromALiteral()
		{
			string[] composed = UiTagKeys.AllAssignments
				.Where(assignment => assignment.IsComposed)
				.Select(assignment => assignment.Site + " " + assignment.Receiver + " = " + assignment.Expression)
				.ToArray();

			Assert.True(composed.Length == 0,
				"These tags build a key the check cannot read. Write it as one literal:\n  "
				+ string.Join("\n  ", composed));
		}

		// - exact counts: a tag that newly escapes every lookup must be a deliberate choice
		// - CheckBox: 82 condition-monitor numbers, 36 spell-designer modifiers set at run time
		// - ContextMenuStrip: cmsImprovementLocation's empty tag, no lookup reads a strip
		[Fact]
		public void UnreadTagsArePinned()
		{
			string[] unread = UiTagKeys.Unread(UiTagKeys.AllAssignments, UiTagKeys.AllClassBases)
				.GroupBy(assignment => TypeOf(assignment).Name, StringComparer.Ordinal)
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.Select(group => group.Key + " x" + group.Count())
				.ToArray();

			Assert.Equal(new[] { "CheckBox x118", "ContextMenuStrip x1" }, unread);
		}

		// - the lookup table is hand-written: a new tag lookup would otherwise go unchecked
		[Fact]
		public void LookupTableMatchesEveryTagLookup()
		{
			string[] actual = UiStringKeys.AllCalls
				.Where(call => UiStringKeys.IsTagLookup(call.Argument))
				.GroupBy(call => call.Site.Substring(0, call.Site.LastIndexOf(':')) + " " + call.Argument)
				.Select(group => group.Key + " x" + group.Count())
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			string[] expected = UiTagKeys.Lookups
				.Select(lookup => lookup.File + " " + lookup.Argument + " x" + lookup.Calls)
				.OrderBy(entry => entry, StringComparer.Ordinal)
				.ToArray();

			Assert.Equal(expected, actual);
		}

		// - an empty extraction would make every check above vacuous
		// - a lower bound, not exact: it counts code, which later phases change
		[Fact]
		public void FindsTheDesignerTags()
		{
			int found = UiTagKeys.AllAssignments.Count(assignment => UiTagKeys.IsDesignerFile(assignment.Site));

			Assert.True(found >= 1500, "Only " + found + " designer tags found, expected at least 1500.");
		}
	}
}
