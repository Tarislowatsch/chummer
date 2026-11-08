using Xunit;

namespace Chummer.GoldenMaster.Tests
{
	// - one collection: the host state is process-wide
	// - AppHostTests guards that every test class joins it
	[CollectionDefinition(AppHost.Collection)]
	public sealed class AppHostCollection : ICollectionFixture<AppHost>
	{
	}
}
