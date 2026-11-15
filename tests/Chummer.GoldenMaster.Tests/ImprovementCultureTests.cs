using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml;
using Xunit;

namespace Chummer.GoldenMaster.Tests
{
	// The print never runs CreateImprovements: Load reads the saved values.
	[Collection(AppHost.Collection)]
	public class ImprovementCultureTests
	{
		// - the bonus values BonusValueTests found culture-dependent, both "Rating div 2"
		private static readonly string[] Cyberware = { "Boosted Reflexes", "Boosted Reflexes (2050)" };

		// - "name | rating" -> initiative passes under en-US and de-DE, read off the running code
		// - exact: every rating the selection dialog offers, 1 to <rating>
		private static readonly IReadOnlyDictionary<string, string> PinnedPasses =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "Boosted Reflexes | 1", "en-US 0, de-DE 5" },
				{ "Boosted Reflexes | 2", "en-US 1, de-DE 1" },
				{ "Boosted Reflexes | 3", "en-US 1, de-DE 15" },
				{ "Boosted Reflexes (2050) | 1", "en-US 0, de-DE 5" },
				{ "Boosted Reflexes (2050) | 2", "en-US 1, de-DE 1" },
				{ "Boosted Reflexes (2050) | 3", "en-US 1, de-DE 15" },
				{ "Boosted Reflexes (2050) | 4", "en-US 2, de-DE 2" },
				{ "Boosted Reflexes (2050) | 5", "en-US 2, de-DE 25" },
				{ "Boosted Reflexes (2050) | 6", "en-US 3, de-DE 3" },
			};

		[Fact]
		public void InitiativePassesArePinnedPerCulture()
		{
			Dictionary<string, string> actual = new Dictionary<string, string>(StringComparer.Ordinal);
			XmlDocument data = XmlManager.Instance.Load("cyberware.xml");
			foreach (string name in Cyberware)
			{
				XmlNode cyberware = data.SelectSingleNode("/chummer/cyberwares/cyberware[name = \"" + name + "\"]");
				int maximum = Convert.ToInt32(cyberware["rating"].InnerText, CultureInfo.InvariantCulture);
				for (int rating = 1; rating <= maximum; rating++)
					actual.Add(name + " | " + rating, "en-US " + Passes(cyberware["bonus"], rating, Fixtures.Baseline)
						+ ", de-DE " + Passes(cyberware["bonus"], rating, Fixtures.German));
			}

			string[] differing = actual.Keys.Union(PinnedPasses.Keys, StringComparer.Ordinal)
				.Where(key => !actual.ContainsKey(key) || !PinnedPasses.ContainsKey(key) || actual[key] != PinnedPasses[key])
				.OrderBy(key => key, StringComparer.Ordinal)
				.Select(key => key + ": " + (actual.ContainsKey(key) ? actual[key] : "no case")
					+ ", pinned " + (PinnedPasses.ContainsKey(key) ? PinnedPasses[key] : "nothing"))
				.ToArray();
			Assert.True(differing.Length == 0, "Initiative passes differ from the pin:\n" + string.Join("\n", differing));
		}

		private static int Passes(XmlNode bonus, int rating, CultureInfo culture)
		{
			Thread thread = Thread.CurrentThread;
			CultureInfo ambient = thread.CurrentCulture;
			thread.CurrentCulture = culture;
			try
			{
				Character character = new Character();
				Assert.True(new ImprovementManager(character).CreateImprovements(Improvement.ImprovementSource.Cyberware, "pin", bonus, false, rating));
				return character.Improvements.Single(improvement => improvement.ImproveType == Improvement.ImprovementType.InitiativePass).Value;
			}
			finally
			{
				thread.CurrentCulture = ambient;
			}
		}
	}
}
