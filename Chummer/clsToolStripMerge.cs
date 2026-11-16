using System;
using System.Windows.Forms;

namespace Chummer
{
	/// <summary>
	/// Portable wrapper around ToolStripManager.RevertMerge.
	/// </summary>
	public static class ToolStripMerge
	{
		/// <summary>
		/// Undo a previous merge into the named ToolStrip, if there is one.
		/// </summary>
		/// <param name="strTargetName">Name of the ToolStrip that was merged into.</param>
		// - .NET returns false when nothing is merged, Mono throws ArgumentNullException
		public static void RevertMerge(string strTargetName)
		{
			try
			{
				ToolStripManager.RevertMerge(strTargetName);
			}
			catch (ArgumentNullException) when (Platform.IsMono)
			{
			}
		}
	}
}
