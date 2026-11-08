using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Xunit;

namespace Chummer.Tests.Consumption
{
	// - every real value converts today: the data cases cannot show a blind check
	// - hand-built XML and source pin each decision to something that fails when it changes
	public class NumericFieldDetectionTests
	{
		private static readonly NumericFields.ConvertedField Rating =
			new NumericFields.ConvertedField("rating", null, NumericFields.Conversion.Int32);

		[Fact]
		public void NonIntegerValueIsReported()
		{
			Assert.Equal(new[] { "R" }, UnconvertibleIn("Gear", "<gear><rating>R</rating></gear>", Rating));
		}

		[Fact]
		public void IntegerValueIsAccepted()
		{
			Assert.Empty(UnconvertibleIn("Gear", "<gear><rating>-3</rating></gear>", Rating));
		}

		// - Convert.ToInt32 trims: a padded value is no crash
		[Fact]
		public void SurroundingWhitespaceIsAccepted()
		{
			Assert.Empty(UnconvertibleIn("Gear", "<gear><rating> 6 </rating></gear>", Rating));
		}

		[Fact]
		public void AbsentFieldIsNotReported()
		{
			Assert.Empty(UnconvertibleIn("Gear", "<gear><name>Commlink</name></gear>", Rating));
		}

		// - Convert.ToInt32("") throws, unlike Convert.ToInt32(null) for an absent field
		[Fact]
		public void EmptyValueIsReported()
		{
			Assert.Equal(new[] { "" }, UnconvertibleIn("Gear", "<gear><rating></rating></gear>", Rating));
		}

		// - 47 programs and options carry an empty <maxrating> the Create never converts
		[Fact]
		public void EmptyMaxRatingIsDivertedForPrograms()
		{
			NumericFields.ConvertedField maxRating =
				new NumericFields.ConvertedField("maxrating", null, NumericFields.Conversion.Int32);

			Assert.Empty(UnconvertibleIn("TechProgram", "<program><maxrating></maxrating></program>", maxRating));
			Assert.Equal(new[] { "x" },
				UnconvertibleIn("TechProgram", "<program><maxrating>x</maxrating></program>", maxRating));
		}

		// - a diversion belongs to one class: another Create would still convert the same field
		[Fact]
		public void DiversionDoesNotLeakIntoOtherClasses()
		{
			NumericFields.ConvertedField cost =
				new NumericFields.ConvertedField("cost", null, NumericFields.Conversion.Int32);

			Assert.Empty(UnconvertibleIn("Armor", "<armor><cost>Variable(20-100)</cost></armor>", cost));
			Assert.Equal(new[] { "Variable(20-100)" },
				UnconvertibleIn("Weapon", "<weapon><cost>Variable(20-100)</cost></weapon>", cost));
		}

		// - Boolean.Parse knows only True and False, in any case
		[Fact]
		public void BooleanAcceptsOnlyTrueAndFalse()
		{
			NumericFields.ConvertedField requireAmmo =
				new NumericFields.ConvertedField("requireammo", null, NumericFields.Conversion.Boolean);

			Assert.Empty(UnconvertibleIn("Weapon", "<weapon><requireammo>false</requireammo></weapon>", requireAmmo));
			Assert.Equal(new[] { "no" },
				UnconvertibleIn("Weapon", "<weapon><requireammo>no</requireammo></weapon>", requireAmmo));
		}

		// - the attribute is read off the element, not the element's own text
		// - en-US, as the Create passes it: a decimal comma would be a thousands separator
		[Fact]
		public void AttributeValueIsParsedInTheApplicationCulture()
		{
			NumericFields.ConvertedField multiply =
				new NumericFields.ConvertedField("range", "multiply", NumericFields.Conversion.Double);

			Assert.Empty(UnconvertibleIn("Weapon", "<weapon><range multiply=\"1.5\">x</range></weapon>", multiply));
			Assert.Equal(new[] { "half" },
				UnconvertibleIn("Weapon", "<weapon><range multiply=\"half\">2</range></weapon>", multiply));
		}

		[Fact]
		public void ConversionsOfTheCreateParameterAreRead()
		{
			Assert.Equal(new[] { "range/@multiply:Double", "rating:Int32", "requireammo:Boolean" },
				FieldsOf("Gear", ConvertedFieldsIn(
					"public class Gear",
					"{",
					"	public void Create(XmlNode objXmlGear)",
					"	{",
					"		_intMaxRating = Convert.ToInt32(objXmlGear[\"rating\"].InnerText);",
					"		_blnAmmo = Convert.ToBoolean(objXmlGear[\"requireammo\"].InnerText);",
					"		_dbl = Convert.ToDouble(objXmlGear[\"range\"].Attributes[\"multiply\"].InnerText, c);",
					"	}",
					"}")));
		}

		// - a commented-out read would add a field the code no longer converts
		// - strValues[0] indexes a string array, not a node: no field and no throw
		[Fact]
		public void CommentsAndStringArraysAreIgnored()
		{
			Assert.Equal(new[] { "rating:Int32" },
				FieldsOf("Gear", ConvertedFieldsIn(
					"public class Gear",
					"{",
					"	public void Create(XmlNode objXmlGear)",
					"	{",
					"		_intMaxRating = Convert.ToInt32(objXmlGear[\"rating\"].InnerText);",
					"		// _intSignal = Convert.ToInt32(objXmlGear[\"signal\"].InnerText);",
					"		intMin = Convert.ToInt32(strValues[0]);",
					"	}",
					"}")));
		}

		// - armor, weapon and vehicle Creates convert attributes on the nested nodes they loop over
		// - both loop shapes occur: SelectNodes inline and through an XmlNodeList variable
		[Fact]
		public void ConversionsOnLoopedNodesAreReadWithTheirPath()
		{
			Assert.Equal(new[] { "gears/gear/@qty:Int32", "mods/name/@rating:Int32" },
				FieldsOf("Vehicle", ConvertedFieldsIn(
					"public class Vehicle",
					"{",
					"	public void Create(XmlNode objXmlVehicle)",
					"	{",
					"		foreach (XmlNode objXmlMod in objXmlVehicle.SelectNodes(\"mods/name\"))",
					"		{",
					"			if (objXmlMod.Attributes[\"rating\"] != null)",
					"				intRating = Convert.ToInt32(objXmlMod.Attributes[\"rating\"].InnerText);",
					"		}",
					"		XmlNodeList objXmlGearList = objXmlVehicle.SelectNodes(\"gears/gear\");",
					"		foreach (XmlNode objXmlGear in objXmlGearList)",
					"			intQty = Convert.ToInt32(objXmlGear.Attributes[\"qty\"].InnerText);",
					"	}",
					"}")));
		}

		// - a node the scan cannot place would drop its conversions out of the check
		[Fact]
		public void ConversionOnAnUnresolvedNodeThrows()
		{
			Assert.Throws<InvalidOperationException>(() => ConvertedFieldsIn(
				"public class Gear",
				"{",
				"	public void Create(XmlNode objXmlGear)",
				"	{",
				"		XmlNode objChild = objXmlGear.SelectSingleNode(\"gears/gear\");",
				"		_intQty = Convert.ToInt32(objChild[\"qty\"].InnerText);",
				"	}",
				"}"));
		}

		[Fact]
		public void BadValueOnALoopedNodeIsReported()
		{
			NumericFields.ConvertedField rating = new NumericFields.ConvertedField(
				"mods/name", null, "rating", NumericFields.Conversion.Int32);

			Assert.Equal(new[] { "x" }, UnconvertibleIn("Vehicle",
				"<vehicle><mods><name rating=\"2\">A</name><name rating=\"x\">B</name></mods></vehicle>", rating));
		}

		// - each attribute conversion in a Create is guarded by != null (clsEquipment.cs:4288)
		// - reporting the absence would describe a crash the application does not have
		[Fact]
		public void AbsentAttributeIsNotReported()
		{
			NumericFields.ConvertedField multiply =
				new NumericFields.ConvertedField("range", "multiply", NumericFields.Conversion.Double);

			Assert.Empty(UnconvertibleIn("Weapon", "<weapon><range>x</range></weapon>", multiply));
		}

		// - reads after the method's closing brace belong to some other member
		[Fact]
		public void ScanStopsAtTheEndOfTheCreate()
		{
			Assert.Equal(new[] { "rating:Int32" },
				FieldsOf("Gear", ConvertedFieldsIn(
					"public class Gear",
					"{",
					"	public void Create(XmlNode objXmlGear)",
					"	{",
					"		_intMaxRating = Convert.ToInt32(objXmlGear[\"rating\"].InnerText);",
					"	}",
					"	public void Load(XmlNode objXmlGear)",
					"	{",
					"		_intSignal = Convert.ToInt32(objXmlGear[\"signal\"].InnerText);",
					"	}",
					"}")));
		}

		// - an unknown shape must stop the run, not shrink the field list
		[Fact]
		public void UnrecognisedConversionShapeThrows()
		{
			Assert.Throws<InvalidOperationException>(() => ConvertedFieldsIn(
				"public class Gear",
				"{",
				"	public void Create(XmlNode objXmlGear)",
				"	{",
				"		int.TryParse(objXmlGear[\"rating\"].InnerText, out _intMaxRating);",
				"	}",
				"}"));
		}

		private static IReadOnlyList<string> UnconvertibleIn(string className, string itemXml,
			NumericFields.ConvertedField field)
		{
			XmlDocument document = new XmlDocument();
			document.LoadXml(itemXml);

			return NumericFields
				.UnconvertibleValuesIn(new XmlNode[] { document.DocumentElement }, className, new[] { field })
				.Select(value => value.Value)
				.ToArray();
		}

		private static IReadOnlyDictionary<string, IReadOnlyList<NumericFields.ConvertedField>> ConvertedFieldsIn(
			params string[] lines)
		{
			return NumericFields.ConvertedFieldsIn(lines);
		}

		private static IEnumerable<string> FieldsOf(string className,
			IReadOnlyDictionary<string, IReadOnlyList<NumericFields.ConvertedField>> fields)
		{
			return fields[className].Select(field => field.Path + ":" + field.Conversion);
		}
	}
}
