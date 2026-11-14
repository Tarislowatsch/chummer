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
		// - a listed key waits for a fix: a reader to wire up or a deletion
		private static readonly IReadOnlyList<string> KnownUnreadKeys = Array.Empty<string>();

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
