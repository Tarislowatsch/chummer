using System.Drawing;

namespace Chummer.GoldenMaster.Tests
{
	// - writes no file: each real print would leave a new GUID-named image behind
	// - a fixed URL keeps a skipped Store call visible in the print
	public sealed class RecordingMugshotStore : IMugshotStore
	{
		public const string Url = "file:///mugshots/fixture.img";

		public int Calls { get; private set; }

		public string Store(Image imgMugshot)
		{
			Calls++;
			return Url;
		}
	}
}
