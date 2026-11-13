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
		private const string MugshotFixture = @"SR4 Contacts\Fixer.chum";

		private static string MugshotDirectory => Path.Combine(AppHost.AppDirectory, "mugshots");

		// Exact count: each of these prints is where the store replaces a file write.
		[Fact]
		public void MugshotFixtureCountIsPinned()
		{
			Assert.Equal(36, Fixtures.RelativePaths().Count(Fixtures.HasMugshot));
		}

		[Fact]
		public void PrintWritesNoFileBelowStartupPath()
		{
			Assert.True(Fixtures.HasMugshot(MugshotFixture), MugshotFixture + " lost its mugshot: pick another fixture.");
			string[] before = FilesIn(MugshotDirectory);
			Fixtures.Print(MugshotFixture);
			Assert.Equal(before, FilesIn(MugshotDirectory));
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

		private static string[] FilesIn(string directory)
		{
			return Directory.Exists(directory) ? Directory.GetFiles(directory).OrderBy(path => path, StringComparer.Ordinal).ToArray() : new string[0];
		}
	}
}
