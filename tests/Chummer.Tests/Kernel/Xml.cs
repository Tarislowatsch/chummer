using System.Xml;

namespace Chummer.Tests.Kernel
{
	// - no resolver, like the XmlReader tests' default: two loaders must not disagree on a DTD
	public static class Xml
	{
		public static XmlDocument Load(string path)
		{
			XmlDocument document = new XmlDocument { XmlResolver = null };
			document.Load(path);
			return document;
		}

		public static XmlDocument Parse(string text)
		{
			XmlDocument document = new XmlDocument { XmlResolver = null };
			document.LoadXml(text);
			return document;
		}
	}
}
