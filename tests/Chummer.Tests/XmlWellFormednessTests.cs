using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// - nothing in the application catches a malformed file today: clsXmlManager.cs:145 throws XmlException unfiltered, and Program.cs installs neither Application.ThreadException nor AppDomain.UnhandledException, so one stray ampersand in gear.xml is a crash for a user, not a diagnostic here
	// - each file is its own test case rather than one pass/fail verdict for the whole directory, so a single bad file does not hide the rest
	public class XmlWellFormednessTests
	{
		[Theory]
		[MemberData(nameof(DataPaths.RuleXmlFiles), MemberType = typeof(DataPaths))]
		public void RuleXmlIsWellFormed(string path)
		{
			AssertWellFormed(path);
		}

		[Theory]
		[MemberData(nameof(DataPaths.LangXmlFiles), MemberType = typeof(DataPaths))]
		public void LangXmlIsWellFormed(string path)
		{
			AssertWellFormed(path);
		}

		[Theory]
		[MemberData(nameof(DataPaths.SheetXslFiles), MemberType = typeof(DataPaths))]
		public void SheetXslIsWellFormed(string path)
		{
			AssertWellFormed(path);
		}

		private static void AssertWellFormed(string path)
		{
			using (XmlReader reader = XmlReader.Create(path))
			{
				while (reader.Read())
				{
				}
			}
		}
	}
}
