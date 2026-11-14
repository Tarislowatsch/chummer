using Xunit;

namespace Chummer.Tests.Localization
{
	// - hand-built keys: the real list shows today's gaps, never that a new one is caught
	public class UiKeyReadDetectionTests
	{
		private static readonly UiStringKeys.ComposedForm Exempt = new UiStringKeys.ComposedForm(
			"\"String_Attribute\" + x + \"Short\"", "String_Attribute", "Short", "test", null);

		[Fact]
		public void ReportsOnlyKeysNoReaderReaches()
		{
			Assert.Equal(new[] { "Label_B", "Label_C" }, UiKeyReads.UnreadKeys(
				new[] { "Label_C", "Label_A", "Label_B", "Label_C" }, new[] { "Label_A" }, new UiStringKeys.ComposedForm[0]));
		}

		[Fact]
		public void AnExemptFormCoversEveryKeyOfItsShape()
		{
			Assert.Empty(UiKeyReads.UnreadKeys(new[] { "String_AttributeBODShort" }, new string[0], new[] { Exempt }));
		}

		[Theory]
		[InlineData("String_AttributeShort")]
		[InlineData("String_AttributeBODLong")]
		[InlineData("Label_AttributeBODShort")]
		public void AnExemptFormNeedsPrefixValueAndSuffix(string key)
		{
			Assert.Equal(new[] { key }, UiKeyReads.UnreadKeys(new[] { key }, new string[0], new[] { Exempt }));
		}
	}
}
