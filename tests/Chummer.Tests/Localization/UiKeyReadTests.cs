using System;
using System.Collections.Generic;
using System.Linq;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Localization
{
	public class UiKeyReadTests
	{
		// - exact: deleting a key or adding a reader must shrink this list in the same change
		// - these keys stay: their controls exist but lack the Tag that would read them
		private static readonly IReadOnlyList<string> KnownUnreadKeys = new[]
		{
			"Checkbox_RestrictedTarget",
			"Checkbox_VeryRestrictedTarget",
			"Title_ExportCharacter",
		};

		[Fact]
		public void OnlyKnownKeysGoUnread()
		{
			string[] unexpected = UiKeyReads.AllUnread.Except(KnownUnreadKeys, StringComparer.Ordinal)
				.Select(key => "unread, not listed: " + key)
				.ToArray();
			string[] stale = KnownUnreadKeys.Except(UiKeyReads.AllUnread, StringComparer.Ordinal)
				.Select(key => "listed, but read or gone: " + key)
				.ToArray();

			FailureReport.AssertNoneInFull("en-us.xml keys differ from the pinned unread list",
				unexpected.Concat(stale).ToArray());
		}
	}
}
