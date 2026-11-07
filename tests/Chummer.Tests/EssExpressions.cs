using System;
using System.Collections.Generic;
using System.Xml;

namespace Chummer.Tests
{
	// - CalculatedESS (clsEquipment.cs:3622) and the selection dialog read ess without a try
	// - grade, suite and improvement multipliers are runtime state and not replayed
	public static class EssExpressions
	{
		// - Create copies <ess> verbatim (clsEquipment.cs:2370), bioware shares the class
		public static readonly IReadOnlyList<ExpressionChecks.ExpressionRule> Rules = new[]
		{
			new ExpressionChecks.ExpressionRule("Cyberware", "ess", Ess),
			new ExpressionChecks.ExpressionRule("Bioware", "ess", Ess),
		};

		public static readonly IReadOnlyList<string> Calculators = new[]
		{
			"Cyberware.CalculatedESS",
			"frmSelectCyberware.UpdateCyberwareInfo",
		};

		// - a decimal comma splits FixedValues into one entry too many, without a throw
		public sealed class MisalignedFixedValues : Exception
		{
			public MisalignedFixedValues(int values, string rating)
				: base(values + " values for rating " + rating)
			{
			}
		}

		// - a "[*]" entry may also be added as a subsystem, always at rating 0 (clsEquipment.cs:2539)
		// - every other entry passes the selection dialog first
		private static void Ess(XmlNode item, int rating)
		{
			string ess = ExpressionChecks.FieldOf(item, "ess");
			string maxRating = ExpressionChecks.FieldOf(item, "rating");

			if (ess.StartsWith("FixedValues") && FixedValues(ess).Length.ToString() != maxRating)
				throw new MisalignedFixedValues(FixedValues(ess).Length, maxRating);

			Select(ess, rating);
			Calculate(ess, rating);
			if (ExpressionChecks.FieldOf(item, "capacity") == "[*]")
				Calculate(ess, 0);
		}

		private static string[] FixedValues(string ess)
		{
			return ess.Replace("FixedValues(", string.Empty).Replace(")", string.Empty).Split(',');
		}

		// - frmSelectCyberware.cs:838, outside any try: every value but FixedValues runs as XPath
		// - a decimal comma throws here, CalculatedESS instead reads "0,2" as 2
		public static double Select(string ess, int rating)
		{
			if (ess.StartsWith("FixedValues"))
			{
				return Convert.ToDouble(Convert.ToDecimal(FixedValues(ess)[rating - 1], ExpressionChecks.ApplicationCulture),
					ExpressionChecks.ApplicationCulture);
			}

			return Convert.ToDouble(ExpressionChecks.Evaluate(ess.Replace("Rating", rating.ToString())),
				ExpressionChecks.ApplicationCulture);
		}

		// - public so the detection tests can drive it with hand-built values
		public static decimal Calculate(string ess, int rating)
		{
			if (ess.Contains("Rating"))
			{
				return Convert.ToDecimal(ExpressionChecks.Evaluate(ess.Replace("Rating", rating.ToString())),
					ExpressionChecks.ApplicationCulture);
			}

			if (ess.StartsWith("FixedValues"))
			{
				return Convert.ToDecimal(FixedValues(ess)[rating - 1], ExpressionChecks.ApplicationCulture);
			}

			return Convert.ToDecimal(ess, ExpressionChecks.ApplicationCulture);
		}
	}
}
