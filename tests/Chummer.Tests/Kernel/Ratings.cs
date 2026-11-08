using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Chummer.Tests.Kernel
{
	// - the rating range a replay sweeps, shared by the expression and the bonus replays
	public static class Ratings
	{
		// - every rating up to here is replayed, no assumption about the expressions needed
		// - above it only the top end: credsticks reach 1000000, a full sweep would take minutes
		public const int ExhaustiveLimit = 100;

		// - the dialogs take minrating as is, 0 included (frmSelectGear.cs:1097), 1 without one
		// - a negative one would be no rating at all: it counts as 0
		public static int Minimum(string minText)
		{
			return int.TryParse(minText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int declared)
				? Math.Max(0, declared)
				: 1;
		}

		// - min..max, cut at the limit, plus max itself
		public static IReadOnlyList<int> Sweep(int min, int max)
		{
			return Enumerable.Range(min, Math.Max(0, Math.Min(max, ExhaustiveLimit) - min + 1))
				.Concat(new[] { max })
				.Distinct()
				.ToArray();
		}
	}
}
