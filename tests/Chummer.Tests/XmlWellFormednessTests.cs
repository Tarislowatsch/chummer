using System.Xml;
using Xunit;

namespace Chummer.Tests
{
	// - a malformed file crashes the app: clsXmlManager.cs:145 throws XmlException unfiltered
	// - per-file cases keep one bad file from hiding the rest
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
