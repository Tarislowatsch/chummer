using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Xunit;

namespace Chummer.GoldenMaster.Tests
{
	[Collection(AppHost.Collection)]
	public class MugshotTests
	{
		internal const string MugshotFixture = @"SR4 Contacts\Fixer.chum";

		private static string MugshotDirectory => Path.Combine(AppHost.AppDirectory, "mugshots");

		// - exact count: each of these prints asserts one Store call
		// - a mugshot fixture that stops printing must lower the pin deliberately
		[Fact]
		public void PrintingMugshotFixtureCountIsPinned()
		{
			Assert.Equal(36, Fixtures.RelativePaths().Count(path => Fixtures.HasMugshot(path) && !PrintTests.KnownPrintFailures.ContainsKey(path)));
		}

		[Fact]
		public void PrintWritesNoFileBelowStartupPath()
		{
			Assert.True(Fixtures.HasMugshot(MugshotFixture), MugshotFixture + " lost its mugshot: pick another fixture.");
			string[] before = FilesBelow(AppHost.AppDirectory);
			Fixtures.Print(MugshotFixture);
			Assert.Equal(new string[0], FilesBelow(AppHost.AppDirectory).Except(before));
		}

		[Fact]
		public void MugshotStoreRejectsNull()
		{
			Assert.Throws<ArgumentNullException>(() => { new Character().MugshotStore = null; });
		}

		// The store the app uses must keep writing where frmViewer deletes on close.
		[Fact]
		public void FileStoreWritesAnImgFileIntoTheMugshotsDirectory()
		{
			string url;
			using (Bitmap image = new Bitmap(1, 1))
				url = new FileMugshotStore().Store(image);

			string path = new Uri(url).LocalPath;
			try
			{
				Assert.StartsWith("file://", url);
				Assert.Equal(MugshotDirectory, Path.GetDirectoryName(path));
				Assert.Equal(".img", Path.GetExtension(path));
				Assert.True(File.Exists(path), path);
			}
			finally
			{
				File.Delete(path);
			}
		}

		private static string[] FilesBelow(string directory)
		{
			return Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
				.Select(path => path.Substring(directory.Length + 1))
				.OrderBy(path => path, StringComparer.Ordinal)
				.ToArray();
		}
	}
}
