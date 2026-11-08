using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using Chummer.Tests.Kernel;

namespace Chummer.Tests.Consumption.Expressions
{
	// - the calculators return text, the CapacityRemaining getters parse it without a try
	// - the forms show both on selection, unguarded (frmCreate.cs:16956)
	public static class CapacityExpressions
	{
		public static readonly IReadOnlyList<ExpressionChecks.ExpressionRule> Rules = new[]
		{
			// - Create copies the field verbatim: clsEquipment.cs:84, :950, :2371, :9578, :9585
			new ExpressionChecks.ExpressionRule("ArmorMod", "armorcapacity", ArmorMod),
			new ExpressionChecks.ExpressionRule("Armor", "armorcapacity", Armor),
			new ExpressionChecks.ExpressionRule("Cyberware", "capacity", Cyberware),
			new ExpressionChecks.ExpressionRule("Bioware", "capacity", Cyberware),
			new ExpressionChecks.ExpressionRule("Gear", "capacity", Gear),
			new ExpressionChecks.ExpressionRule("Commlink", "capacity", Gear),
			new ExpressionChecks.ExpressionRule("OperatingSystem", "capacity", Gear),
			new ExpressionChecks.ExpressionRule("Gear", "armorcapacity", GearArmor),
			new ExpressionChecks.ExpressionRule("Commlink", "armorcapacity", GearArmor),
			new ExpressionChecks.ExpressionRule("OperatingSystem", "armorcapacity", GearArmor),
			// - clsUnique.cs:5586
			new ExpressionChecks.ExpressionRule("TechProgram", "capacity", TechProgram),
		};

		public static readonly IReadOnlyList<string> Calculators = new[]
		{
			"ArmorMod.CalculatedCapacity",
			"Armor.CalculatedCapacity",
			"Armor.CapacityRemaining",
			"Cyberware.CalculatedCapacity",
			"Cyberware.CapacityRemaining",
			"Gear.CalculatedCapacity",
			"Gear.CalculatedArmorCapacity",
			"Gear.PluginCapacity",
			"Gear.PluginArmorCapacity",
			"Gear.CapacityRemaining",
			"TechProgram.CalculatedCapacity",
			"frmSelectArmorMod.UpdateSelectedArmor",
			"frmSelectCyberware.UpdateCyberwareInfo",
			"frmSelectGear.UpdateGearInfo",
		};

		private static void ArmorMod(XmlNode item, int rating)
		{
			string capacity = ExpressionChecks.FieldOf(item, "armorcapacity");
			CheckFixedValues(capacity, ExpressionChecks.FieldOf(item, "maxrating"));

			SelectArmorMod(capacity, rating);
			ArmorChild(CalculatedArmorModCapacity(capacity, rating));
		}

		// - only with ArmorSuitCapacity on: "" and "0" switch to the Body/Impact rule instead
		private static void Armor(XmlNode item, int rating)
		{
			string capacity = ExpressionChecks.FieldOf(item, "armorcapacity");
			if (capacity != "" && capacity != "0")
				Convert.ToInt32(capacity);
		}

		private static void Cyberware(XmlNode item, int rating)
		{
			string capacity = ExpressionChecks.FieldOf(item, "capacity");
			CheckFixedValues(capacity, ExpressionChecks.FieldOf(item, "rating"));

			SelectCyberware(capacity, rating);
			string calculated = CalculatedCyberwareCapacity(capacity, rating);
			CyberwareParent(capacity, calculated);
			CyberwareChild(calculated);
		}

		private static void Gear(XmlNode item, int rating)
		{
			if (item["capacity"] == null)
				return;

			string capacity = item["capacity"].InnerText;
			SelectGear(capacity, rating);
			string calculated = CalculatedGearCapacity(capacity, rating);
			GearParent(capacity, calculated);
			GearChild(calculated);
		}

		private static void GearArmor(XmlNode item, int rating)
		{
			if (item["armorcapacity"] == null)
				return;

			string capacity = item["armorcapacity"].InnerText;
			SelectGear(capacity, rating);
			string calculated = CalculatedGearArmorCapacity(capacity, rating);
			ArmorChild(calculated);
			GearChild(calculated);
		}

		// - maxrating 0 means "up to RES" (frmCreate.cs:4465): every rating up to the limit
		private static void TechProgram(XmlNode item, int rating)
		{
			string capacity = ExpressionChecks.FieldOf(item, "capacity");
			IEnumerable<int> ratings = rating == 0
				? Enumerable.Range(1, ExpressionChecks.ExhaustiveRatingLimit)
				: new[] { rating };
			foreach (int each in ratings)
				CalculatedTechProgramCapacity(capacity, each);
		}

		// - a decimal comma splits FixedValues into one entry too many, without a throw
		private static void CheckFixedValues(string capacity, string maxRating)
		{
			if (capacity.StartsWith("FixedValues") && FixedValues(capacity).Length.ToString() != maxRating)
				throw new EssExpressions.MisalignedFixedValues(FixedValues(capacity).Length, maxRating);
		}

		private static string[] FixedValues(string capacity)
		{
			return capacity.Replace("FixedValues(", string.Empty).Replace(")", string.Empty).Split(',');
		}

		private static string StripBrackets(string capacity)
		{
			return capacity.Substring(1, capacity.Length - 2);
		}

		private static XPathExpression Compile(string expression, int rating)
		{
			return new XmlDocument().CreateNavigator().Compile(expression.Replace("Rating", rating.ToString()));
		}

		private static string EvaluateToString(XPathExpression expression)
		{
			return Convert.ToString(new XmlDocument().CreateNavigator().Evaluate(expression),
				Cultures.Application);
		}

		// - the "/[" branch shared by ArmorMod, Cyberware and both Gear calculators
		// - only its evaluation sits in a try: a failure there becomes "0"
		private static string FirstHalf(string capacity, int rating, out string secondHalf)
		{
			int position = capacity.IndexOf("/[");
			string firstHalf = capacity.Substring(0, position);
			secondHalf = capacity.Substring(position + 1, capacity.Length - position - 1);
			bool squareBrackets = false;
			string expression = "";

			try
			{
				squareBrackets = firstHalf.Contains('[');
				expression = firstHalf;
				if (squareBrackets)
					expression = StripBrackets(expression);
			}
			catch
			{
			}

			XPathExpression compiled = Compile(expression, rating);

			try
			{
				string result;
				if (capacity == "[*]")
					result = "*";
				else if (capacity.StartsWith("FixedValues"))
					result = FixedValues(capacity)[rating - 1];
				else
					result = EvaluateToString(compiled);

				// - the code wraps the unevaluated expression, not the result
				return squareBrackets ? "[" + expression + "]" : result;
			}
			catch (Exception exception)
			{
				throw new ExpressionChecks.SilentlyDiscarded(exception);
			}
		}

		private static string RatingBranch(string capacity, int rating)
		{
			bool squareBrackets = capacity.Contains('[');
			string expression = squareBrackets ? StripBrackets(capacity) : capacity;
			string result = EvaluateToString(Compile(expression, rating));
			return squareBrackets ? "[" + result + "]" : result;
		}

		// clsEquipment.cs:762
		public static string CalculatedArmorModCapacity(string capacity, int rating)
		{
			if (capacity.Contains("/["))
				return FirstHalf(capacity, rating, out string secondHalf) + "/" + secondHalf;
			if (capacity.Contains("Rating"))
				return RatingBranch(capacity, rating);
			if (capacity == "")
				return "0";
			if (capacity.StartsWith("FixedValues"))
				return FixedValues(capacity)[rating - 1];
			return capacity;
		}

		// clsEquipment.cs:3524
		public static string CalculatedCyberwareCapacity(string capacity, int rating)
		{
			if (capacity.Contains("/["))
			{
				string firstHalf = FirstHalf(capacity, rating, out string secondHalf);
				if (secondHalf.Contains("Rating"))
				{
					secondHalf = secondHalf.Replace("[", string.Empty).Replace("]", string.Empty);
					secondHalf = "[" + EvaluateToString(Compile(secondHalf, rating)) + "]";
				}

				return firstHalf + "/" + secondHalf;
			}

			if (capacity.Contains("Rating"))
				return RatingBranch(capacity, rating);
			if (capacity.StartsWith("FixedValues"))
				return FixedValues(capacity)[rating - 1];
			return capacity;
		}

		// - clsEquipment.cs:11262, the Rating branch floors to a whole number of at least 1
		public static string CalculatedGearCapacity(string capacity, int rating)
		{
			if (capacity.Contains("/["))
				return FirstHalf(capacity, rating, out string secondHalf) + "/" + secondHalf;
			if (capacity.Contains("Rating"))
			{
				bool squareBrackets = capacity.Contains('[');
				string expression = squareBrackets ? StripBrackets(capacity) : capacity;
				decimal number = Convert.ToDecimal(new XmlDocument().CreateNavigator().Evaluate(Compile(expression, rating)),
					Cultures.Application);
				string result = Math.Max(1, Convert.ToInt32(Math.Floor(number))).ToString();
				return squareBrackets ? "[" + result + "]" : result;
			}

			return capacity == "" ? "0" : capacity;
		}

		// clsEquipment.cs:11355
		public static string CalculatedGearArmorCapacity(string capacity, int rating)
		{
			if (capacity.Contains("/["))
				return FirstHalf(capacity, rating, out string secondHalf) + "/" + secondHalf;
			if (capacity.Contains("Rating"))
				return RatingBranch(capacity, rating);
			return capacity == "" ? "0" : capacity;
		}

		// clsUnique.cs:6109
		public static int CalculatedTechProgramCapacity(string capacity, int rating)
		{
			if (!capacity.Contains("Rating"))
				return Convert.ToInt32(capacity);

			string expression = capacity.Contains('[') ? StripBrackets(capacity) : capacity;
			string result = EvaluateToString(Compile(expression.Replace("/", " div "), rating));
			return Convert.ToInt32(Math.Ceiling(Convert.ToDouble(result, Cultures.Application)));
		}

		// - Armor.CapacityRemaining (clsEquipment.cs:1945, :1963) deducts mods and gear alike
		public static int ArmorChild(string calculated)
		{
			string capacity = calculated;
			if (capacity.Contains("/["))
				capacity = capacity.Substring(capacity.IndexOf("/[") + 1);
			if (capacity.Contains("["))
				capacity = StripBrackets(capacity);
			if (capacity == "*")
				capacity = "0";
			return Convert.ToInt32(capacity);
		}

		// - Cyberware.CapacityRemaining (clsEquipment.cs:4006), the item as the parent
		public static int CyberwareParent(string capacity, string calculated)
		{
			if (capacity.Contains("/["))
				return Convert.ToInt32(calculated.Substring(0, calculated.IndexOf('/')));
			if (!capacity.Contains("["))
				return Convert.ToInt32(calculated);
			return 0;
		}

		// - the same getter, the item as a child: the text between the first [ and ]
		public static int CyberwareChild(string calculated)
		{
			string capacity = calculated;
			if (capacity.Contains("/["))
			{
				capacity = capacity.Substring(capacity.IndexOf('[') + 1,
					capacity.IndexOf(']') - capacity.IndexOf('[') - 1);
			}
			else if (capacity.Contains("["))
			{
				capacity = StripBrackets(capacity);
			}

			if (capacity == "*")
				capacity = "0";
			return Convert.ToInt32(capacity);
		}

		// - Gear.CapacityRemaining (clsEquipment.cs:11748), the item as the parent
		public static int GearParent(string capacity, string calculated)
		{
			if (capacity.Contains("[") && !capacity.Contains("/["))
				return 0;
			if (capacity.Contains("/["))
				return Convert.ToInt32(calculated.Substring(0, calculated.IndexOf("/[")));
			return Convert.ToInt32(calculated);
		}

		// - PluginCapacity, PluginArmorCapacity and the child loop of CapacityRemaining read alike
		// - text without brackets costs nothing
		public static int GearChild(string calculated)
		{
			string capacity = calculated;
			if (capacity.Contains("/["))
				capacity = capacity.Substring(capacity.IndexOf("/[") + 1);
			return Convert.ToInt32(capacity.Contains("[") ? StripBrackets(capacity) : "0");
		}

		// - frmSelectArmorMod.cs:275, outside any try
		// - the Standard style evaluates, the others never read the expression
		public static void SelectArmorMod(string capacity, int rating)
		{
			string expression = capacity;
			if (expression.StartsWith("FixedValues"))
				expression = FixedValues(expression)[rating - 1];

			EvaluateToString(Compile(StripBrackets(expression), rating));
		}

		// - frmSelectCyberware.cs:854, outside any try except the first half of "/["
		// - the plain branch compiles before it checks for FixedValues or "[*]"
		public static void SelectCyberware(string capacity, int rating)
		{
			if (capacity.Contains("/["))
			{
				FirstHalf(capacity, rating, out string secondHalf);
				if (secondHalf.Contains("Rating"))
					EvaluateToString(Compile(secondHalf.Replace("[", string.Empty).Replace("]", string.Empty), rating));
				return;
			}

			string expression = capacity.Contains('[') ? StripBrackets(capacity) : capacity;
			XPathExpression compiled = Compile(expression, rating);
			if (capacity == "[*]")
				return;
			if (capacity.StartsWith("FixedValues"))
			{
				string unused = FixedValues(capacity)[rating - 1];
				return;
			}

			EvaluateToString(compiled);
		}

		// - frmSelectGear.cs:984, the whole Standard branch sits in a try that shows "0"
		// - the original prints in the thread culture: under de-DE "2,5" skips the '.' rounding
		public static void SelectGear(string capacity, int rating)
		{
			try
			{
				if (capacity.Contains("/["))
				{
					FirstHalf(capacity, rating, out string unused);
					return;
				}

				string expression = capacity.Contains('[') ? StripBrackets(capacity) : capacity;
				XPathExpression compiled = Compile(expression, rating);
				if (capacity == "[*]")
					return;
				if (capacity.StartsWith("FixedValues"))
				{
					string value = FixedValues(capacity)[rating - 1];
					return;
				}

				string result = EvaluateToString(compiled);
				if (result.Contains('.'))
					Convert.ToInt32(Math.Floor(Convert.ToDecimal(result, Cultures.Application)));
			}
			catch (ExpressionChecks.SilentlyDiscarded)
			{
				throw;
			}
			catch (Exception exception)
			{
				throw new ExpressionChecks.SilentlyDiscarded(exception);
			}
		}
	}
}
