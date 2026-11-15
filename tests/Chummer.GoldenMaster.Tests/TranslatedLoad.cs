using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Microsoft.Win32;

namespace Chummer.GoldenMaster.Tests
{
	// - one AppDomain per language: LanguageManager loads a language once per domain
	// - XmlManager caches the merged documents in a static list as well
	public sealed class TranslatedLoad : MarshalByRefObject
	{
		// What the translation pass of XmlManager.Load adds to an item.
		private static readonly string[] AddedElements = { "translate", "altpage", "altcode", "altadvantage", "altdisadvantage" };

		private const string ChummerKey = @"Software\Chummer";

		// - the language goes in the registry, where the app reads it
		// - GlobalOptions' static constructor reads it, then loads and caches books, cyberware and bioware
		// - the registry sandbox is per process: the key must be gone before the next test
		public static string[] Project(string language, IEnumerable<string> fileNames)
		{
			AppHost.CreateVolatileCurrentUserKey(ChummerKey);
			AppDomain domain = null;
			try
			{
				using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ChummerKey, true))
					key.SetValue("language", language);
				domain = AppDomain.CreateDomain("TranslatedLoad " + language, null, AppDomain.CurrentDomain.SetupInformation);
				TranslatedLoad worker = (TranslatedLoad)domain.CreateInstanceAndUnwrap(typeof(TranslatedLoad).Assembly.FullName, typeof(TranslatedLoad).FullName);
				return worker.ProjectInThisDomain(language, fileNames.ToArray());
			}
			finally
			{
				if (domain != null)
					AppDomain.Unload(domain);
				Registry.CurrentUser.DeleteSubKeyTree("Software", false);
			}
		}

		// The StartupPath field is per domain: redirect it before GlobalOptions reads it.
		public string[] ProjectInThisDomain(string language, string[] fileNames)
		{
			AppHost.RedirectStartupPath();
			if (GlobalOptions.Instance.Language != language)
				throw new InvalidOperationException("GlobalOptions read " + GlobalOptions.Instance.Language + " instead of " + language + ".");
			return fileNames.SelectMany(fileName => Lines(fileName, XmlManager.Instance.Load(fileName))).ToArray();
		}

		private static IEnumerable<string> Lines(string fileName, XmlDocument document)
		{
			foreach (XmlElement element in document.SelectNodes("//*").OfType<XmlElement>())
			{
				if (element.HasAttribute("translate"))
					yield return fileName + " " + PathOf(element) + " @translate = " + Escaped(element.GetAttribute("translate"));
				foreach (XmlElement added in element.ChildNodes.OfType<XmlElement>().Where(child => AddedElements.Contains(child.Name)))
					yield return fileName + " " + PathOf(added.ParentNode) + " " + added.Name + " = " + Escaped(added.InnerText);
			}
		}

		// Keyed by <name>, or by the text of a leaf such as a category or spec.
		private static string PathOf(XmlNode node)
		{
			List<string> steps = new List<string>();
			for (XmlNode step = node; step != null && step.ParentNode is XmlElement; step = step.ParentNode)
			{
				string key = step["name"] != null ? step["name"].InnerText
					: step.ChildNodes.OfType<XmlElement>().Any() ? null : step.InnerText;
				steps.Insert(0, key == null ? step.Name : step.Name + "[" + Escaped(key) + "]");
			}

			return "/" + string.Join("/", steps);
		}

		// One line per added value: a translation with a line break must not split it.
		private static string Escaped(string value)
		{
			return value.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
		}
	}
}
