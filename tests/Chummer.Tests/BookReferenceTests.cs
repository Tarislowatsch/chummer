using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - a code books.xml does not declare never matches an enabled book: the entry is unbuyable
	// - filter sites: frmSelectWeapon.cs:82, frmSelectArmorMod.cs:58, frmSelectProgramOption.cs:37
	// - deliberately no allowlist: the data is clean today
	public class BookReferenceTests
	{
		[Theory]
		[MemberData(nameof(DataPaths.TopLevelRuleXmlFilesCitingBooks), MemberType = typeof(DataPaths))]
		public void EverySourceCitesADeclaredBook(string xmlPath)
		{
			// Grouped by code: one dropped book is one problem, not hundreds of identical lines.
			var unknown = DataPaths
				.ReferencesToUndeclaredBooks(DataPaths.BookReferencesFor(xmlPath), DataPaths.BookCodes)
				.GroupBy(reference => reference.Code, StringComparer.Ordinal)
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.ToArray();

			if (unknown.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					Path.GetFileName(xmlPath) + " cites " + unknown.Length
						+ " book code(s) that books.xml does not declare",
					unknown,
					group => "'" + group.Key + "' used by " + group.Count()
						+ " entries, e.g. '" + group.First().ItemName + "'"));
			}
		}
	}
}
