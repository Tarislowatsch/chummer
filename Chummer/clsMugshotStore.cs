using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Chummer
{
	/// <summary>
	/// Takes a character's decoded portrait during printing and returns the URL the sheets show.
	/// </summary>
	public interface IMugshotStore
	{
		string Store(Image imgMugshot);
	}

	/// <summary>
	/// Writes each portrait to a new GUID-named file below StartupPath\mugshots.
	/// </summary>
	public class FileMugshotStore : IMugshotStore
	{
		public string Store(Image imgMugshot)
		{
			// Since IE is retarded and can't handle base64 images before IE9, we need to dump the image to a temporary directory and re-write the information.
			// If you give it an extension of jpg, gif, or png, it expects the file to be in that format and won't render the image unless it was originally that type.
			// But if you give it the extension img, it will render whatever you give it (which doesn't make any damn sense, but that's IE for you).
			if (!Directory.Exists(Application.StartupPath + Path.DirectorySeparatorChar + "mugshots"))
				Directory.CreateDirectory(Application.StartupPath + Path.DirectorySeparatorChar + "mugshots");
			Guid guiImage = Guid.NewGuid();
			imgMugshot.Save(Application.StartupPath + Path.DirectorySeparatorChar + "mugshots" + Path.DirectorySeparatorChar + guiImage.ToString() + ".img");
			return "file://" + (Application.StartupPath + Path.DirectorySeparatorChar + "mugshots" + Path.DirectorySeparatorChar + guiImage.ToString() + ".img").Replace(Path.DirectorySeparatorChar, '/');
		}
	}
}
