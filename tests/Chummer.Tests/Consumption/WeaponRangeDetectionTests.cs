using System;
using System.Xml.XPath;
using Xunit;

namespace Chummer.Tests
{
	// - hand-built formulas: the real data shows today's values, never that a bad one is caught
	public class WeaponRangeDetectionTests
	{
		[Theory]
		[InlineData("50", 4, 50)]
		[InlineData("STR*3", 4, 12)]
		[InlineData("BOD*2", 4, 8)]
		[InlineData("STR", 4, 4)]
		public void EvaluatesTheFormulasTheDataUses(string formula, int attribute, int expected)
		{
			Assert.Equal(expected, WeaponRanges.Evaluate(formula, attribute, attribute));
		}

		[Fact]
		public void ThrowsOnAFormulaXPathCannotCompile()
		{
			Assert.ThrowsAny<XPathException>(() => WeaponRanges.Evaluate("STR*", 4, 4));
		}

		[Fact]
		public void ThrowsOnANonNumericResult()
		{
			Assert.ThrowsAny<FormatException>(() => WeaponRanges.Evaluate("'far'", 4, 4));
		}

		[Fact]
		public void FindsASetRangeLiteral()
		{
			Assert.Equal(new[] { "Aerodynamic Grenades" },
				WeaponRanges.SetRangeLiteralsIn(new[] { "objWeapon.SetRange(\"Aerodynamic Grenades\");" }));
		}

		[Fact]
		public void IgnoresACommentedOutSetRange()
		{
			Assert.Empty(WeaponRanges.SetRangeLiteralsIn(new[] { "// objWeapon.SetRange(\"Old\");" }));
		}
	}
}
