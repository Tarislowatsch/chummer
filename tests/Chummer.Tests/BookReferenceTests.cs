using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// Every catalogue entry names the book it came out of in <source>, and
	// books.xml is the list of book codes that exist. The link matters because
	// the application filters on it: Options.BookXPath() builds a predicate over
	// the enabled books and every picker ANDs it into its query - see
	// frmSelectWeapon.cs:82, frmSelectArmorMod.cs:58, frmSelectProgramOption.cs:37.
	// An entry citing a code no book declares can therefore never match an enabled
	// book, so it is unbuyable no matter which books the character has switched on.
	//
	// The data is clean today: 6293 references, 42 declared codes, nothing
	// dangling. So this has no allowlist and nothing to defer - it is here to keep
	// it that way, which is the cheapest moment to add such a check.
	public class BookReferenceTests
	{
		[Theory]
		[MemberData(nameof(DataPaths.TopLevelRuleXmlFilesCitingBooks), MemberType = typeof(DataPaths))]
		public void EverySourceCitesADeclaredBook(string xmlPath)
		{
			// Grouped by code rather than listed per reference: a dropped book code
			// takes every entry that cited it down at once, and 200 lines all
			// saying the same thing hides how many distinct problems there are.
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
