using System;
using System.Collections.Generic;
using Xunit;

namespace Chummer.Tests
{
	// - which collections are in scope was, until now, asserted only by a count: twenty of them, take it or leave it - that number moves for any reason at all and says nothing about which rule produced it
	// - one of the rules - the throw on a redirect pointing at a block that does not exist - could not be reached by a test at all, because the real override is correct
	// - the resolution used to read the cached corpus directly; it now takes its declarations as an argument, so each outcome can be driven here instead of being taken on trust from a mutation somebody ran once by hand
	public class CategoryScopeResolutionTests
	{
		[Fact]
		public void OrdinaryCollectionAnswersToTheFilesCategoriesBlock()
		{
			string block;
			IReadOnlyCollection<string> declared;

			bool inScope = DataPaths.TryResolveDeclarationBlock("weapons.xml", "weapons",
				Declarations("categories", "Bike", "Car"), out block, out declared);

			Assert.True(inScope);
			Assert.Equal("categories", block);
			Assert.Equal(new[] { "Bike", "Car" }, declared);
		}

		[Fact]
		public void RedirectedCollectionAnswersToItsOwnBlock()
		{
			string block;
			IReadOnlyCollection<string> declared;

			bool inScope = DataPaths.TryResolveDeclarationBlock("vehicles.xml", "mods",
				Declarations("modcategories", "Standard", "Special"), out block, out declared);

			Assert.True(inScope);
			Assert.Equal("modcategories", block);
		}

		// - the redirect must not quietly fall back to <categories> when its own block is absent - vehicles.xml has both, so falling back would check mod categories against the vehicle vocabulary, exactly the bug clsEquipment.cs:13453 already has
		[Fact]
		public void RedirectDoesNotFallBackToTheDefaultBlock()
		{
			string block;
			IReadOnlyCollection<string> declared;

			Assert.Throws<InvalidOperationException>(() =>
				DataPaths.TryResolveDeclarationBlock("vehicles.xml", "mods",
					Declarations("categories", "Bike", "Car"), out block, out declared));
		}

		[Fact]
		public void RedirectToAMissingBlockNamesTheCollectionAndTheBlock()
		{
			string block;
			IReadOnlyCollection<string> declared;

			InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
				DataPaths.TryResolveDeclarationBlock("vehicles.xml", "mods",
					NoDeclarations, out block, out declared));

			Assert.Contains("vehicles.xml/mods", error.Message);
			Assert.Contains("modcategories", error.Message);
		}

		// - the legitimate half of the same condition: the throw cannot simply cover every missing block, because lifestyles.xml and ranges.xml carry categories that no block anywhere declares
		[Fact]
		public void FileWithNoBlockAtAllIsOutOfScopeWithoutThrowing()
		{
			string block;
			IReadOnlyCollection<string> declared;

			Assert.False(DataPaths.TryResolveDeclarationBlock("lifestyles.xml", "qualities",
				NoDeclarations, out block, out declared));
		}

		// - an exemption wins even when the file does have a <categories> block, because the whole point is that the overlap is coincidental
		[Fact]
		public void ExemptCollectionIsOutOfScopeEvenWhenTheBlockExists()
		{
			string block;
			IReadOnlyCollection<string> declared;

			Assert.False(DataPaths.TryResolveDeclarationBlock("weapons.xml", "mods",
				Declarations("categories", "Weapon Mod"), out block, out declared));
		}

		private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> NoDeclarations =
			new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);

		private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> Declarations(
			string block, params string[] values)
		{
			return new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
			{
				{ block, values },
			};
		}
	}
}
