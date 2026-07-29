using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.Tests
{
	// - the contract is which element a Create reads with no try/catch around it
	// - Gear.Create (clsEquipment.cs:9552) reads <avail> unguarded, so a gear entry without one throws a NullReferenceException the moment a player selects it
	// - nothing catches it: clsXmlManager.cs:145 throws unfiltered
	// - Program.cs installs neither Application.ThreadException nor AppDomain.UnhandledException
	// - the failure is therefore the application closing, not a message box
	//
	// - one rule per entity type in DataPaths.RequiredFieldRules, each citing the Create line it was read off
	// - no absent field anywhere in the data today, so there is no allowlist
	// - a check that finds nothing proves nothing about whether it still can, which is what the detection tests next door are for
	//
	// - the .xsd files already reject 150 of the 159 required fields, measured by deleting each one from a real entry in turn
	// - the 9 they miss are the two Metamagic files' <source>/<page>, gear.xml's four commlink fields and a vehicle weapon reference's <name>
	// - not redundant even so: the .xsd files were brought into line with the *data* when they were last corrected, never with what a Create dereferences
	// - a schema relaxation would take a crash contract out of CI with nothing to notice
	public class RequiredFieldContractTests
	{
		[Theory]
		[MemberData(nameof(DataPaths.EntitiesWithRequiredFields), MemberType = typeof(DataPaths))]
		public void EveryEntryCarriesTheFieldsItsCreateReadsUnguarded(string entity)
		{
			DataPaths.RequiredFieldContract contract = DataPaths.RequiredFieldContractFor(entity);

			// - grouped by field rather than listed per entry, as in the book-code check
			// - a field that goes missing usually goes missing across a whole batch of entries at once
			// - one line each would hide how many distinct fields are actually broken
			var absent = contract.MissingFields
				.GroupBy(missing => missing.Field, StringComparer.Ordinal)
				.OrderBy(group => group.Key, StringComparer.Ordinal)
				.ToArray();

			if (absent.Length > 0)
			{
				Assert.Fail(FailureReport.Build(
					Path.GetFileName(contract.FilePath) + " has " + entity + " entries missing "
						+ absent.Length + " field(s) that " + entity + ".Create reads unguarded",
					absent,
					group => "<" + group.Key + "> absent from " + group.Count()
						+ " entries, e.g. '" + group.First().ItemName + "'"));
			}
		}
	}
}
