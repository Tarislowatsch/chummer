using System;
using System.Collections.Generic;
using Xunit;

namespace Chummer.Tests
{
	// - the scope count next door moves for any reason and names no rule
	// - the missing-block throw is unreachable through the real data
	// - each outcome is driven through injected declarations, not the cached corpus
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

		// - falling back to <categories> would check mods against the vehicle vocabulary
		// - clsEquipment.cs:13453 already has exactly that bug
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

		// The legitimate missing-block case: lifestyles.xml and ranges.xml declare categories nowhere.
		[Fact]
		public void FileWithNoBlockAtAllIsOutOfScopeWithoutThrowing()
		{
			string block;
			IReadOnlyCollection<string> declared;

			Assert.False(DataPaths.TryResolveDeclarationBlock("lifestyles.xml", "qualities",
				NoDeclarations, out block, out declared));
		}

		// An exemption must beat an existing <categories> block: the overlap is coincidental.
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
