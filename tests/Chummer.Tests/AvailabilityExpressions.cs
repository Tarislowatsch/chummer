using System;
using System.Collections.Generic;
using System.Xml;

namespace Chummer.Tests
{
	// - each port replays one TotalAvail plus the GetAvailInt ValidateCharacter runs on it
	// - ports, not a common grammar: Armor knows no Rating, Cyberware alone knows FixedValues
	// - English run: the translated suffix GetAvailInt strips is then R or F
	public static class AvailabilityExpressions
	{
		// - the selection dialogs parse the raw XML on their own and are not replayed here
		public static readonly IReadOnlyList<ExpressionChecks.ExpressionRule> Rules = new[]
		{
			// clsEquipment.cs:697
			Rule("ArmorMod", ArmorMod),
			// clsEquipment.cs:1812
			Rule("Armor", Armor),
			// - clsEquipment.cs:3380, Bioware through the same class
			Rule("Cyberware", Cyberware),
			Rule("Bioware", Cyberware),
			// - clsEquipment.cs:11156, inherited by both commlink classes
			Rule("Gear", Gear),
			Rule("Commlink", Gear),
			Rule("OperatingSystem", Gear),
			// clsEquipment.cs:14137
			Rule("VehicleMod", VehicleMod),
			// - clsEquipment.cs:7173, :7732, :8697, :15272 only translate and return the raw text
			Rule("Weapon", RawText),
			Rule("WeaponAccessory", RawText),
			Rule("WeaponMod", RawText),
			Rule("Vehicle", RawText),
		};

		// - every member a port above replays, for the drift pin
		public static readonly IReadOnlyList<string> Calculators = new[]
		{
			"ArmorMod.TotalAvail",
			"Armor.TotalAvail",
			"Cyberware.TotalAvail",
			"Gear.TotalAvail",
			"VehicleMod.TotalAvail",
			"Weapon.TotalAvail",
			"WeaponAccessory.TotalAvail",
			"WeaponMod.TotalAvail",
			"Vehicle.CalculatedAvail",
			"frmCreate.GetAvailInt",
		};

		private static ExpressionChecks.ExpressionRule Rule(string entity,
			Func<XmlNode, int, string> calculator)
		{
			return new ExpressionChecks.ExpressionRule(entity, "avail",
				(item, rating) => GetAvailInt(calculator(item, rating)));
		}

		private static string ArmorMod(XmlNode item, int rating)
		{
			string avail = ExpressionChecks.FieldOf(item, "avail");
			if (avail.Contains("+"))
				return avail;

			return ParseCalculated(avail.Contains("Rating") ? RatingExpression(avail, rating) : Straight(avail));
		}

		private static string Armor(XmlNode item, int rating)
		{
			string avail = ExpressionChecks.FieldOf(item, "avail");
			if (avail.Contains("+"))
				return avail;

			return ParseCalculated(Straight(avail));
		}

		// - Grade.Avail is added on top: a runtime int, it cannot make a value fail
		private static string Cyberware(XmlNode item, int rating)
		{
			string avail = ExpressionChecks.FieldOf(item, "avail");

			if (avail.StartsWith("+"))
			{
				if (!avail.Contains("Rating"))
					return avail;

				string suffix;
				string expression = StripSuffix(avail.Substring(1, avail.Length - 1), out suffix);
				return "+" + ExpressionChecks.EvaluateToString(expression.Replace("Rating", rating.ToString()))
					+ suffix;
			}

			string calculated;
			if (avail.Contains("Rating"))
			{
				calculated = RatingExpression(avail, rating);
			}
			else if (avail.StartsWith("FixedValues"))
			{
				string[] values = avail.Replace("FixedValues(", string.Empty).Replace(")", string.Empty).Split(',');
				calculated = values[rating - 1];
			}
			else
			{
				calculated = Straight(avail);
			}

			return ParseCalculated(calculated);
		}

		// - TotalAvail(true), the call ValidateCharacter makes
		private static string Gear(XmlNode item, int rating)
		{
			string avail = ExpressionChecks.FieldOf(item, "avail");
			string avail3 = ExpressionChecks.FieldOf(item, "avail3");
			string avail6 = ExpressionChecks.FieldOf(item, "avail6");
			string avail10 = ExpressionChecks.FieldOf(item, "avail10");

			if (avail == "")
				avail = "0";

			bool includePlus = avail.StartsWith("+");
			string calculated;

			if (avail.Contains("Rating") || avail3.Contains("Rating") || avail6.Contains("Rating")
				|| avail10.Contains("Rating"))
			{
				string expression = includePlus ? avail.Substring(1, avail.Length - 1) : avail;

				if (rating <= 3 && avail3 != "")
					expression = avail3;
				else if (rating <= 6 && avail6 != "")
					expression = avail6;
				else if (rating >= 7 && avail10 != "")
					expression = avail10;

				calculated = RatingExpression(expression, rating);
			}
			else
			{
				calculated = Straight(avail);
			}

			string result = ParseCalculated(calculated);
			return includePlus ? "+" + result : result;
		}

		private static string VehicleMod(XmlNode item, int rating)
		{
			string avail = ExpressionChecks.FieldOf(item, "avail");
			if (avail.StartsWith("+"))
				return avail;

			return ParseCalculated(avail.Contains("Rating") ? RatingExpression(avail, rating) : Straight(avail));
		}

		private static string RawText(XmlNode item, int rating)
		{
			return ExpressionChecks.FieldOf(item, "avail");
		}

		// - the Rating branch shared by four calculators: suffix off, substitute, evaluate
		private static string RatingExpression(string expression, int rating)
		{
			string suffix;
			string stripped = StripSuffix(expression, out suffix);

			return Convert.ToInt32(ExpressionChecks.Evaluate(stripped.Replace("Rating", rating.ToString())))
				.ToString() + suffix;
		}

		// - Substring(Length - 1) as the code does: an empty expression throws here too
		private static string StripSuffix(string expression, out string suffix)
		{
			string last = expression.Substring(expression.Length - 1, 1);
			if (last == "F" || last == "R")
			{
				suffix = last;
				return expression.Substring(0, expression.Length - 1);
			}

			suffix = "";
			return expression;
		}

		// - the no-Rating branch: exactly one trailing character is cut, "24FF" leaves "24F"
		private static string Straight(string avail)
		{
			if (avail.Contains("F") || avail.Contains("R"))
			{
				return Convert.ToInt32(avail.Substring(0, avail.Length - 1))
					+ avail.Substring(avail.Length - 1, 1);
			}

			return Convert.ToInt32(avail).ToString();
		}

		// - the second parse every calculating TotalAvail runs on its own intermediate result
		private static string ParseCalculated(string calculated)
		{
			if (calculated.Contains("F") || calculated.Contains("R"))
			{
				string text = calculated.Substring(calculated.Length - 1);
				return Convert.ToInt32(calculated.Replace(text, string.Empty)) + text;
			}

			return Convert.ToInt32(calculated).ToString();
		}

		// - frmCreate.cs:21767, every TotalAvail result passes it in ValidateCharacter
		// - Replace drops every R or F, not just the last: "24FF" parses here
		public static int GetAvailInt(string avail)
		{
			string result = avail;
			if (avail.StartsWith("+"))
				result = result.Replace("+", string.Empty);
			if (avail.EndsWith("R"))
				result = result.Replace("R", string.Empty);
			if (avail.EndsWith("F"))
				result = result.Replace("F", string.Empty);

			return Convert.ToInt32(result);
		}
	}
}
