using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// A [Theory] with an empty MemberData source runs zero cases and reports
	// green - so if path resolution ever broke (wrong directory, a rename), the
	// well-formedness theories below would silently stop checking anything
	// while CI stayed green. These guard against that by asserting each source
	// actually found a plausible number of files.
	public class DataPathsTests
	{
		[Fact]
		public void RuleXmlFilesFindsData()
		{
			Assert.True(DataPaths.RuleXmlFiles().Count() > 10);
		}

		[Fact]
		public void LangXmlFilesFindsTranslations()
		{
			Assert.True(DataPaths.LangXmlFiles().Count() > 1);
		}

		[Fact]
		public void SheetXslFilesFindsCharacterSheets()
		{
			Assert.True(DataPaths.SheetXslFiles().Count() > 1);
		}
	}
}
