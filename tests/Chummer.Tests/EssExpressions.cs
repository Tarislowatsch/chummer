using System;
using System.Collections.Generic;
using System.Xml;

namespace Chummer.Tests
{
	// - Cyberware.CalculatedESS (clsEquipment.cs:3622) has no try: a bad value throws into the UI
	// - grade, suite and improvement multipliers are runtime state and not replayed
	public static class EssExpressions
	{
		// - Create copies <ess> verbatim (clsEquipment.cs:2370), bioware shares the class
		public static readonly IReadOnlyList<ExpressionChecks.ExpressionRule> Rules = new[]
		{
			new ExpressionChecks.ExpressionRule("Cyberware", "ess", Ess),
			new ExpressionChecks.ExpressionRule("Bioware", "ess", Ess),
		};

		public static readonly IReadOnlyList<string> Calculators = new[] { "Cyberware.CalculatedESS" };

		// - a "[*]" entry may also be added as a subsystem, always at rating 0 (clsEquipment.cs:2539)
		private static void Ess(XmlNode item, int rating)
		{
			string ess = ExpressionChecks.FieldOf(item, "ess");

			Calculate(ess, rating);
			if (ExpressionChecks.FieldOf(item, "capacity") == "[*]")
				Calculate(ess, 0);
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
				string[] values = ess.Replace("FixedValues(", string.Empty).Replace(")", string.Empty).Split(',');
				return Convert.ToDecimal(values[rating - 1], ExpressionChecks.ApplicationCulture);
			}

			return Convert.ToDecimal(ess, ExpressionChecks.ApplicationCulture);
		}
	}
}
