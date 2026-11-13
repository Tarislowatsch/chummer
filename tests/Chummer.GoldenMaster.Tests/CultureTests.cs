using System.Globalization;
using System.Linq;
using System.Threading;
using Xunit;

namespace Chummer.GoldenMaster.Tests
{
	[Collection(AppHost.Collection)]
	public class CultureTests
	{
		[Fact]
		public void EveryGermanCaseIsAFixture()
		{
			Assert.DoesNotContain(Fixtures.GermanSubset, path => !Fixtures.RelativePaths().Contains(path));
		}

		// A German case that prints like the baseline no longer watches any formatting.
		[Theory]
		[MemberData(nameof(Fixtures.GermanCharacters), MemberType = typeof(Fixtures))]
		public void GermanPrintDiffersFromTheBaseline(string relativePath)
		{
			Assert.NotEqual(Fixtures.Print(relativePath), Fixtures.Print(relativePath, culture: Fixtures.German));
		}

		[Fact]
		public void PrintIgnoresTheAmbientCulture()
		{
			string path = Fixtures.GermanSubset[0];
			Assert.Equal(PrintUnderAmbient(path, Fixtures.Baseline), PrintUnderAmbient(path, Fixtures.German));
		}

		// Also checks that Print hands the thread back with its culture unchanged.
		private static string PrintUnderAmbient(string relativePath, CultureInfo ambient)
		{
			Thread thread = Thread.CurrentThread;
			CultureInfo before = thread.CurrentCulture;
			thread.CurrentCulture = ambient;
			try
			{
				string print = Fixtures.Print(relativePath);
				Assert.Same(ambient, thread.CurrentCulture);
				return print;
			}
			finally
			{
				thread.CurrentCulture = before;
			}
		}
	}
}
