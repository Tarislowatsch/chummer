using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Chummer.Tests.Kernel;
using Chummer.Tests.Rulebook;

namespace Chummer.Tests.Consumption.Fields
{
	// - Create takes the kit rating past the Rating setter's cap
	// - a rating above the item's maximum shows up in the tree, the save and the print
	public static class KitRatings
	{
		public enum Absent
		{
			// - a missing or empty maximum means the item has no rating
			Zero,

			// - an empty maxrating leaves TechProgram at 0: frmCreate reads 0 as no maximum
			NoMaximum,
		}

		// One kit list whose rating goes to an item with a maximum.
		public sealed class Limit
		{
			public Limit(string ruleId, string conversionPath, string maximumField, Absent absent, bool quantityMode = false)
			{
				RuleId = ruleId;
				ConversionPath = conversionPath;
				MaximumField = maximumField;
				Absent = absent;
				QuantityMode = quantityMode;
			}

			// - the CrossFileReferences rule whose lookup finds the item
			public string RuleId { get; }

			// - the path LoaderConversions reports for that rating
			public string ConversionPath { get; }

			public string MaximumField { get; }

			public Absent Absent { get; }

			// - VehicleMod reads a maximum of "qty" as a count: the rating is a quantity then
			public bool QuantityMode { get; }

			public CrossFileReferences.ReferenceRule Rule =>
				CrossFileReferences.Rules.Single(rule => string.Equals(rule.Id, RuleId, StringComparison.Ordinal));
		}

		// - read off AddPACKSKit and the item classes' Create by hand
		public static readonly IReadOnlyList<Limit> Limits = new[]
		{
			new Limit("pack gear", ".//gears/gear/rating", "rating", Absent.Zero),
			new Limit("pack vehicle gear", "vehicles/vehicle/gears/gear/rating", "rating", Absent.Zero),
			new Limit("pack cyberware", "cyberwares/cyberware/rating", "rating", Absent.Zero),
			new Limit("pack cyberware children", "cyberwares/cyberware/cyberwares/cyberware/rating", "rating", Absent.Zero),
			new Limit("pack bioware", "biowares/bioware/rating", "rating", Absent.Zero),
			new Limit("pack armor mods", "armors/armor/mods/mod/rating", "maxrating", Absent.Zero),
			new Limit("pack vehicle mods", "vehicles/vehicle/mods/mod/rating", "rating", Absent.Zero, quantityMode: true),
			new Limit("pack programs", "programs/program/rating", "maxrating", Absent.NoMaximum),
		};

		public enum Outcome
		{
			Fits,
			Exceeds,
			NoMaximum,
			Quantity,

			// - reported by CrossFileReferences, not here
			NoTarget,

			// - Create would throw or fall back: NumericFields' business, reported here all the same
			NotANumber,
		}

		public sealed class Finding
		{
			public Finding(string ruleId, string kit, string item, string rating, string maximum, Outcome outcome)
			{
				RuleId = ruleId;
				Kit = kit;
				Item = item;
				Rating = rating;
				Maximum = maximum;
				Outcome = outcome;
			}

			public string RuleId { get; }

			public string Kit { get; }

			public string Item { get; }

			public string Rating { get; }

			public string Maximum { get; }

			public Outcome Outcome { get; }

			// - the allowlist key and the failure text
			public string Key => Kit + " / " + Item;

			public string Description => Key + ": rating " + Rating + ", maximum " + (Maximum ?? "(none)") + " (" + RuleId + ")";
		}

		// - one finding per kit entry that sets a rating
		public static IEnumerable<Finding> FindingsIn(XmlDocument kits, Limit limit, Func<string, XmlDocument> load)
		{
			CrossFileReferences.ReferenceRule rule = limit.Rule;
			foreach (XmlNode entry in kits.SelectNodes(rule.SourceXPath))
			{
				string rating = entry["rating"]?.InnerText;
				if (rating == null)
					continue;

				string kit = entry.SelectSingleNode("ancestor::pack/name")?.InnerText ?? "(unnamed kit)";
				string name = entry["name"]?.InnerText ?? entry.InnerText;
				XmlNode target = CrossFileReferences.TargetOf(rule, entry, load);
				string maximum = target?[limit.MaximumField]?.InnerText;
				yield return new Finding(rule.Id, kit, name, rating, maximum, OutcomeOf(limit, target, rating, maximum));
			}
		}

		public static Outcome OutcomeOf(Limit limit, XmlNode target, string rating, string maximum)
		{
			if (target == null)
				return Outcome.NoTarget;
			if (limit.QuantityMode && maximum == "qty")
				return Outcome.Quantity;
			if (string.IsNullOrEmpty(maximum) && limit.Absent == Absent.NoMaximum)
				return Outcome.NoMaximum;

			int value;
			int cap = 0;
			if (!int.TryParse(rating, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
				|| (!string.IsNullOrEmpty(maximum) && !int.TryParse(maximum, NumberStyles.Integer, CultureInfo.InvariantCulture, out cap)))
				return Outcome.NotANumber;

			return value > cap ? Outcome.Exceeds : Outcome.Fits;
		}

		public static IReadOnlyList<Finding> Findings => CachedFindings.Value;

		private static readonly Lazy<IReadOnlyList<Finding>> CachedFindings = new Lazy<IReadOnlyList<Finding>>(() =>
		{
			Dictionary<string, XmlDocument> documents = new Dictionary<string, XmlDocument>(StringComparer.OrdinalIgnoreCase);
			Func<string, XmlDocument> load = fileName =>
			{
				XmlDocument document;
				if (!documents.TryGetValue(fileName, out document))
				{
					document = CustomContent.Load(Path.Combine(DataPaths.ChummerDataDir, fileName));
					documents.Add(fileName, document);
				}

				return document;
			};

			XmlDocument kits = load("packs.xml");
			return Limits.SelectMany(limit => FindingsIn(kits, limit, load)).ToArray();
		});
	}
}
