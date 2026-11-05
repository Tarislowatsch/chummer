using System;
using System.Collections.Generic;
using System.Xml;

namespace Chummer.Tests
{
	// - each port replays the Create's Variable(...) handling, then the cost calculators
	// - Weapon and Lifestyle are absent: their Create converts <cost> and NumericFields pins it
	// - parent cost, Body, Speed and Accel are runtime state, replaced by a round stand-in
	public static class CostExpressions
	{
		public static readonly IReadOnlyList<ExpressionChecks.ExpressionRule> Rules = new[]
		{
			// clsEquipment.cs:852
			Rule("ArmorMod", ArmorMod),
			// - clsEquipment.cs:976-990, only the Variable parse: the plain value is NumericFields'
			Rule("Armor", Armor),
			// - Create at clsEquipment.cs:2448, then TotalCost :3744 and OwnCost :3921
			Rule("Cyberware", Cyberware),
			Rule("Bioware", Cyberware),
			// clsEquipment.cs:7808 and :7833
			Rule("WeaponAccessory", WeaponAccessory),
			// clsEquipment.cs:8745
			Rule("WeaponMod", WeaponMod),
			// - Create at clsEquipment.cs:9706, then TotalCost :11515 and OwnCost :11611
			Rule("Gear", (item, rating) => Gear(item, rating, true)),
			// - the commlink Creates (:11957, :12824) take <cost> as is, without the Variable step
			Rule("Commlink", (item, rating) => Gear(item, rating, false)),
			Rule("OperatingSystem", (item, rating) => Gear(item, rating, false)),
			// - Create at clsEquipment.cs:13404, then TotalCost :14202 and OwnCost :14249
			Rule("VehicleMod", VehicleMod),
			// clsEquipment.cs:15617 and :15661
			Rule("Vehicle", Vehicle),
		};

		// - every member a port above replays, for the drift pin
		public static readonly IReadOnlyList<string> Calculators = new[]
		{
			"ArmorMod.TotalCost",
			"Cyberware.TotalCost",
			"Cyberware.OwnCost",
			"WeaponAccessory.TotalCost",
			"WeaponAccessory.OwnCost",
			"WeaponMod.TotalCost",
			"Gear.TotalCost",
			"Gear.OwnCost",
			"VehicleMod.TotalCost",
			"VehicleMod.OwnCost",
			"Vehicle.TotalCost",
			"Vehicle.OwnCost",
		};

		private static readonly string StandIn = ExpressionChecks.RuntimeStandIn.ToString();

		private static ExpressionChecks.ExpressionRule Rule(string entity, ExpressionChecks.Replay replay)
		{
			return new ExpressionChecks.ExpressionRule(entity, "cost", replay);
		}

		private static void ArmorMod(XmlNode item, int rating)
		{
			string cost = ExpressionChecks.FieldOf(item, "cost");

			if (cost.Contains("Armor Cost"))
				CeilingOf(ExpressionChecks.EvaluateToString(cost.Replace("Armor Cost", StandIn)));
			else if (cost.Contains("Rating"))
				CeilingOf(ExpressionChecks.EvaluateToString(cost.Replace("Rating", rating.ToString())));
			else
				Convert.ToInt32(cost);
		}

		private static void Armor(XmlNode item, int rating)
		{
			ResolveVariable(ExpressionChecks.FieldOf(item, "cost"));
		}

		// - the plain branch sits in an empty catch: a bad value silently costs 0
		// - a "*1.5" plugin multiplier lands there by design, its parent reads it (:3744)
		private static void Cyberware(XmlNode item, int rating)
		{
			string cost = ResolveVariable(ExpressionChecks.FieldOf(item, "cost"));

			if (cost.Contains("Rating"))
			{
				Convert.ToInt32(ExpressionChecks.EvaluateToString(cost.Replace("Rating", rating.ToString())));
			}
			else if (cost.StartsWith("FixedValues"))
			{
				string[] values = cost.Replace("FixedValues(", string.Empty).Replace(")", string.Empty).Split(',');
				Convert.ToInt32(values[rating - 1], ExpressionChecks.ApplicationCulture);
			}
			else if (cost.StartsWith("*"))
			{
				Convert.ToDouble(cost.Replace("*", string.Empty), ExpressionChecks.ApplicationCulture);
			}
			else
			{
				Convert.ToInt32(cost);
			}
		}

		private static void WeaponAccessory(XmlNode item, int rating)
		{
			string cost = ExpressionChecks.FieldOf(item, "cost");
			if (cost != "Weapon Cost")
				Convert.ToInt32(cost);
		}

		private static void WeaponMod(XmlNode item, int rating)
		{
			string cost = ExpressionChecks.FieldOf(item, "cost");

			if (cost.StartsWith("Total Cost"))
			{
				Convert.ToInt32(cost.Replace("Total Cost * ", string.Empty));
				return;
			}

			Convert.ToInt32(ExpressionChecks.EvaluateToString(
				cost.Replace("Weapon Cost", StandIn).Replace("Rating", rating.ToString())));
		}

		// - TotalCost and OwnCost share this part, character for character
		private static void Gear(XmlNode item, int rating, bool resolvesVariable)
		{
			string cost = ExpressionChecks.FieldOf(item, "cost");
			if (resolvesVariable)
				cost = ResolveVariable(cost);

			string cost3 = ExpressionChecks.FieldOf(item, "cost3");
			string cost6 = ExpressionChecks.FieldOf(item, "cost6");
			string cost10 = ExpressionChecks.FieldOf(item, "cost10");

			if (cost.Contains("Gear Cost") || cost3.Contains("Gear Cost") || cost6.Contains("Gear Cost")
				|| cost10.Contains("Gear Cost"))
			{
				string expression = ByRating(cost, cost3, cost6, cost10, rating);
				Convert.ToInt32(ExpressionChecks.EvaluateToString(expression.Replace("Gear Cost", StandIn)));
			}
			else if (cost.Contains("Rating") || cost3.Contains("Rating") || cost6.Contains("Rating")
				|| cost10.Contains("Rating") || cost.Contains("*") || cost3.Contains("*")
				|| cost6.Contains("*") || cost10.Contains("*"))
			{
				string expression = ByRating(cost, cost3, cost6, cost10, rating);
				Convert.ToInt32(Math.Ceiling(Convert.ToDouble(
					ExpressionChecks.Evaluate(expression.Replace("Rating", rating.ToString())),
					ExpressionChecks.ApplicationCulture)));
			}
			else if (cost != string.Empty)
			{
				Convert.ToInt32(cost);
			}
		}

		// - <cost> wins whenever it is set, the banded fields apply only to an empty one
		private static string ByRating(string cost, string cost3, string cost6, string cost10, int rating)
		{
			if (cost != "")
				return cost;
			if (rating <= 3)
				return cost3;
			if (rating <= 6)
				return cost6;
			return cost10;
		}

		// - TotalCost rounds the result, OwnCost parses its text: a fraction only breaks OwnCost
		private static void VehicleMod(XmlNode item, int rating)
		{
			string expression = ResolveVariable(ExpressionChecks.FieldOf(item, "cost"))
				.Replace("Rating", rating.ToString())
				.Replace("Vehicle Cost", StandIn)
				.Replace("Body", StandIn)
				.Replace("Speed", StandIn)
				.Replace("Accel(Running)", StandIn)
				.Replace("Accel", StandIn);

			Convert.ToInt32(ExpressionChecks.Evaluate(expression), ExpressionChecks.ApplicationCulture);
			Convert.ToInt32(ExpressionChecks.EvaluateToString(expression));
		}

		private static void Vehicle(XmlNode item, int rating)
		{
			Convert.ToInt32(ExpressionChecks.FieldOf(item, "cost"));
		}

		// - Convert.ToDouble(Evaluate(...).ToString(), en-US), rounded up, as ArmorMod writes it
		private static void CeilingOf(string evaluated)
		{
			Convert.ToInt32(Math.Ceiling(Convert.ToDouble(evaluated, ExpressionChecks.ApplicationCulture)));
		}

		// - the Variable(min-max) / Variable(min+) parse four Creates repeat verbatim
		// - the dialog then replaces the text with a number in range, unless both bounds are 0
		public static string ResolveVariable(string cost)
		{
			if (!cost.StartsWith("Variable"))
				return cost;

			string range = cost.Replace("Variable(", string.Empty).Replace(")", string.Empty);
			int min = 0;
			int max = 0;

			if (range.Contains("-"))
			{
				string[] bounds = range.Split('-');
				min = Convert.ToInt32(bounds[0]);
				max = Convert.ToInt32(bounds[1]);
			}
			else
			{
				min = Convert.ToInt32(range.Replace("+", string.Empty));
			}

			return min != 0 || max != 0 ? min.ToString() : cost;
		}
	}
}
