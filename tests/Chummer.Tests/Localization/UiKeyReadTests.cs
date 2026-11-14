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
		// - deleting them is a fix of its own, after the translations are checked
		private static readonly IReadOnlyList<string> KnownUnreadKeys = new[]
		{
			"Checkbox_RestrictedTarget",
			"Checkbox_VeryRestrictedTarget",
			"Label_BallisticArmorLong",
			"Label_GearRating",
			"Label_ImpactArmorLong",
			"Label_Network",
			"MessageTitle_Omae_DeleteSheet",
			"Message_DeleteStackedFocus",
			"Message_IncorrectGameVersion_SR4",
			"String_AttributeINILong",
			"String_Hour",
			"String_Roommates",
			"String_SelectPACKSKit_Sprites",
			"String_SpellSpecial",
			"String_Unarmed",
			"String_UnarmedAttack",
			"String_Weeks",
			"Tip_Improvement_EditNotes",
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
