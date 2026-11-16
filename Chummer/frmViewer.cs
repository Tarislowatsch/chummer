using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.Xsl;

namespace Chummer
{
	public partial class frmViewer : Form
	{
		private List<Character> _lstCharacters = new List<Character>();
		private XmlDocument _objCharacterXML = new XmlDocument();
		private string _strSelectedSheet = "Shadowrun 4";
		private bool _blnLoading = false;
		// Mono has no WebBrowser engine: the sheet goes to the system browser instead.
		private readonly string _strExternalFile = Path.Combine(Path.GetTempPath(), "chummer-" + Guid.NewGuid().ToString() + ".htm");
		private readonly object _objExternalFileLock = new object();
		private bool _blnExternalShown = false;
		private string _strOutput = string.Empty;
		private static readonly List<string> _lstExternalFiles = new List<string>();
		private static string MugshotDirectory
		{
			get { return Application.StartupPath + Path.DirectorySeparatorChar + "mugshots"; }
		}

		#region Control Events
		public frmViewer()
		{
			_strSelectedSheet = GlobalOptions.Instance.DefaultCharacterSheet;

			InitializeComponent();
			LanguageManager.Instance.Load(GlobalOptions.Instance.Language, this);
			MoveControls();
			if (Platform.IsMono)
			{
				Controls.Remove(webBrowser1);
				lock (_lstExternalFiles)
				{
					if (_lstExternalFiles.Count == 0)
						Application.ApplicationExit += DeleteExternalFiles;
					_lstExternalFiles.Add(_strExternalFile);
				}
			}
		}

		private void frmViewer_Load(object sender, EventArgs e)
		{
			_blnLoading = true;
			List<ListItem> lstFiles = new List<ListItem>();
			// Populate the XSLT list with all of the XSL files found in the sheets directory.
			foreach (string strFile in Directory.GetFiles(Application.StartupPath + Path.DirectorySeparatorChar + "sheets"))
			{
				// Only show files that end in .xsl. Do not include files that end in .xslt since they are used as "hidden" reference sheets (hidden because they are partial templates that cannot be used on their own).
				if (!strFile.EndsWith(".xslt") && strFile.EndsWith(".xsl"))
				{
					string strFileName = strFile.Replace(Application.StartupPath + Path.DirectorySeparatorChar + "sheets" + Path.DirectorySeparatorChar, string.Empty).Replace(".xsl", string.Empty);
					ListItem objItem = new ListItem();
					objItem.Value = strFileName;
					objItem.Name = strFileName;
					lstFiles.Add(objItem);

					//cboXSLT.Items.Add(strFileName);
				}
			}

			try
			{
				// Populate the XSL list with all of the XSL files found in the sheets\[language] directory.
				if (GlobalOptions.Instance.Language != "en-us")
				{
					XmlDocument objLanguageDocument = LanguageManager.Instance.XmlDoc;
					string strLanguage = objLanguageDocument.SelectSingleNode("/chummer/name").InnerText;

					foreach (string strFile in Directory.GetFiles(Application.StartupPath + Path.DirectorySeparatorChar + "sheets" + Path.DirectorySeparatorChar + GlobalOptions.Instance.Language))
					{
						// Only show files that end in .xsl. Do not include files that end in .xslt since they are used as "hidden" reference sheets (hidden because they are partial templates that cannot be used on their own).
						if (!strFile.EndsWith(".xslt") && strFile.EndsWith(".xsl"))
						{
							string strFileName = strFile.Replace(Application.StartupPath + Path.DirectorySeparatorChar + "sheets" + Path.DirectorySeparatorChar + GlobalOptions.Instance.Language + Path.DirectorySeparatorChar, string.Empty).Replace(".xsl", string.Empty);
							ListItem objItem = new ListItem();
							objItem.Value = GlobalOptions.Instance.Language + Path.DirectorySeparatorChar + strFileName;
							objItem.Name = strLanguage + ": " + strFileName;
							lstFiles.Add(objItem);
						}
					}
				}
			}
			catch
			{
			}

			try
			{
				// Populate the XSLT list with all of the XSL files found in the sheets\omae directory.
				foreach (string strFile in Directory.GetFiles(Application.StartupPath + Path.DirectorySeparatorChar + "sheets" + Path.DirectorySeparatorChar + "omae"))
				{
					// Only show files that end in .xsl. Do not include files that end in .xslt since they are used as "hidden" reference sheets (hidden because they are partial templates that cannot be used on their own).
					if (!strFile.EndsWith(".xslt") && strFile.EndsWith(".xsl"))
					{
						string strFileName = strFile.Replace(Application.StartupPath + Path.DirectorySeparatorChar + "sheets" + Path.DirectorySeparatorChar + "omae" + Path.DirectorySeparatorChar, string.Empty).Replace(".xsl", string.Empty);
						ListItem objItem = new ListItem();
						objItem.Value = "omae" + Path.DirectorySeparatorChar + strFileName;
						objItem.Name = LanguageManager.Instance.GetString("Menu_Main_Omae") + ": " + strFileName;
						lstFiles.Add(objItem);
					}
				}
			}
			catch
			{
			}

			cboXSLT.ValueMember = "Value";
			cboXSLT.DisplayMember = "Name";
			cboXSLT.DataSource = lstFiles;

			cboXSLT.SelectedValue = _strSelectedSheet;
			// If the desired sheet was not found, fall back to the Shadowrun 4 sheet.
			if (cboXSLT.Text == "")
				cboXSLT.SelectedValue = "Shadowrun 4";
			GenerateOutput();
			_blnLoading = false;
		}

		private void cboXSLT_SelectedIndexChanged(object sender, EventArgs e)
		{
			// Re-generate the output when a new sheet is selected.
			if (!_blnLoading)
			{
				_strSelectedSheet = cboXSLT.SelectedValue.ToString();
				GenerateOutput();
				// Unlike a background refresh, a new sheet is worth a new browser tab.
				if (Platform.IsMono)
					Platform.OpenWithDefaultApplication(_strExternalFile);
			}
		}

		private void cmdPrint_Click(object sender, EventArgs e)
		{
			if (Platform.IsMono)
			{
				// Printing happens from the browser.
				Platform.OpenWithDefaultApplication(_strExternalFile);
				return;
			}
			webBrowser1.ShowPrintDialog();
		}

		private void cmdSaveHTML_Click(object sender, EventArgs e)
		{
			// Save the generated output as HTML.
			if (Platform.IsMono && _strOutput.Length == 0)
				return;
			string strSaveFile = "";
			SaveFileDialog1.Filter = "HTML Page|*.htm";
			SaveFileDialog1.Title = LanguageManager.Instance.GetString("Button_Viewer_SaveAsHtml");
			SaveFileDialog1.ShowDialog();
			strSaveFile = SaveFileDialog1.FileName;

			if (strSaveFile == "")
				return;

			TextWriter objWriter = new StreamWriter(strSaveFile, false, Encoding.UTF8);
			objWriter.Write(Platform.IsMono ? _strOutput : webBrowser1.DocumentText);
			objWriter.Close();

		}

		private void cmdSaveXML_Click(object sender, EventArgs e)
		{
			// Save the printout XML generated by the character.
			string strSaveFile = "";
			SaveFileDialog1.Filter = "XML File|*.xml";
			SaveFileDialog1.Title = LanguageManager.Instance.GetString("Button_Viewer_SaveAsXml");
			SaveFileDialog1.ShowDialog();
			strSaveFile = SaveFileDialog1.FileName;

			if (strSaveFile == "")
				return;

			_objCharacterXML.Save(strSaveFile);
		}

		private void frmViewer_FormClosing(object sender, FormClosingEventArgs e)
		{
			// Mono keeps the mugshots until exit, see DeleteExternalFiles.
			if (!Platform.IsMono)
			{
				try
				{
					Directory.Delete(MugshotDirectory, true);
				}
				catch
				{
				}
			}

			// Clear the reference to the character's Print window.
			if (_lstCharacters.Count > 0)
			{
				foreach (Character objCharacter in _lstCharacters)
					objCharacter.PrintWindow = null;
			}
		}
		#endregion

		#region Methods
		/// <summary>
		/// Run the generated XML file through the XSL transformation engine to create the file output.
		/// </summary>
		private void GenerateOutput()
		{
			XslCompiledTransform objXSLTransform = new XslCompiledTransform();
			objXSLTransform.Load(Application.StartupPath + Path.DirectorySeparatorChar + "sheets" + Path.DirectorySeparatorChar + _strSelectedSheet + ".xsl");

			MemoryStream objStream = new MemoryStream();
			XmlTextWriter objWriter = new XmlTextWriter(objStream, Encoding.UTF8);

			objXSLTransform.Transform(_objCharacterXML, null, objWriter);
			objStream.Position = 0;

			// This reads from a static file, outputs to an HTML file, then has the browser read from that file. For debugging purposes.
			//objXSLTransform.Transform("D:\\temp\\print.xml", "D:\\temp\\output.htm");
			//webBrowser1.Navigate("D:\\temp\\output.htm");

			if (Platform.IsMono)
			{
				_strOutput = new StreamReader(objStream).ReadToEnd();
				// AsyncRefresh runs this on a worker thread, a sheet change on the UI thread.
				lock (_objExternalFileLock)
					File.WriteAllText(_strExternalFile, _strOutput);
				// Only once: later refreshes rewrite the file for a reload.
				if (!_blnExternalShown)
				{
					_blnExternalShown = true;
					Platform.OpenWithDefaultApplication(_strExternalFile);
				}
			}
			else if (!GlobalOptions.Instance.PrintToFileFirst)
			{
				// Populate the browser using the DocumentStream.
				webBrowser1.DocumentStream = objStream;
			}
			else
			{
				// The DocumentStream method fails when using Wine, so we'll instead dump everything out a temporary HTML file, have the WebBrowser load that, then delete the temporary file.
				// Read in the resulting code and pass it to the browser.
				string strName = Guid.NewGuid().ToString() + ".htm";
				StreamReader objReader = new StreamReader(objStream);
				string strOutput = objReader.ReadToEnd();
				File.WriteAllText(strName, strOutput);
				string curDir = Directory.GetCurrentDirectory();
				webBrowser1.Url = new Uri(String.Format("file:///{0}/" + strName, curDir));
				File.Delete(strName);
			}
		}

		/// <summary>
		/// Asynchronously update the contents of the Viewer window.
		/// </summary>
		public void RefreshView()
		{
			// Create a delegate to handle refreshing the window.
			MethodInvoker objRefreshDelegate = AsyncRefresh;
			objRefreshDelegate.BeginInvoke(null, null);
		}

		/// <summary>
		/// Update the contents of the Viewer window.
		/// </summary>
		private void AsyncRefresh()
		{
			// Write the Character information to a MemoryStream so we don't need to create any files.
			MemoryStream objStream = new MemoryStream();
			XmlTextWriter objWriter = new XmlTextWriter(objStream, Encoding.UTF8);

			// Being the document.
			objWriter.WriteStartDocument();

			// </characters>
			objWriter.WriteStartElement("characters");

			foreach (Character objCharacter in _lstCharacters)
				objCharacter.PrintToStream(objStream, objWriter);

			// </characters>
			objWriter.WriteEndElement();

			// Finish the document and flush the Writer and Stream.
			objWriter.WriteEndDocument();
			objWriter.Flush();
			objStream.Flush();

			// Read the stream.
			StreamReader objReader = new StreamReader(objStream);
			objStream.Position = 0;
			XmlDocument objCharacterXML = new XmlDocument();

			// Put the stream into an XmlDocument and send it off to the Viewer.
			string strXML = objReader.ReadToEnd();
			objCharacterXML.LoadXml(strXML);

			objWriter.Close();
			objStream.Close();

			_objCharacterXML = objCharacterXML;
			GenerateOutput();
		}

		// The system browser keeps reading the sheet and its mugshots after the viewer closes.
		private static void DeleteExternalFiles(object sender, EventArgs e)
		{
			lock (_lstExternalFiles)
			{
				foreach (string strFile in _lstExternalFiles)
				{
					try
					{
						File.Delete(strFile);
					}
					catch (IOException)
					{
					}
					catch (UnauthorizedAccessException)
					{
					}
				}
			}
			try
			{
				Directory.Delete(MugshotDirectory, true);
			}
			catch
			{
			}
		}

		private void MoveControls()
		{
			int intWidth = cmdPrint.Width;
			cmdPrint.AutoSize = false;
			cmdPrint.Width = intWidth + 20;
		}
		#endregion

		#region Properties
		/// <summary>
		/// Character's XmlDocument.
		/// </summary>
		public XmlDocument CharacterXML
		{
			set
			{
				_objCharacterXML = value;
			}
		}

		/// <summary>
		/// Set the XSL sheet that will be selected by default.
		/// </summary>
		public string SelectedSheet
		{
			set
			{
				_strSelectedSheet = value;
			}
		}

		/// <summary>
		/// List of Characters to print.
		/// </summary>
		public List<Character> Characters
		{
			set
			{
				_lstCharacters = value;
			}
		}
		#endregion
	}
}