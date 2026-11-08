using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Chummer.Tests.Kernel
{
	// - a child namespace sees its parent's types without a using
	// - a type in a parent therefore hides edges the using lines are meant to list
	// - the global namespace and Chummer are parents of every context too
	public class NamespaceLayoutTests
	{
		private const string ContextPrefix = "Chummer.Tests.";

		[Fact]
		public void EveryTypeLivesInALeafContextNamespace()
		{
			Type[] types = typeof(NamespaceLayoutTests).Assembly.GetTypes()
				.Where(type => !type.IsNested && !type.IsDefined(typeof(CompilerGeneratedAttribute), false))
				.ToArray();
			string[] namespaces = types
				.Select(type => type.Namespace)
				.Where(name => name != null && name.StartsWith(ContextPrefix, StringComparison.Ordinal))
				.Distinct(StringComparer.Ordinal)
				.ToArray();
			Type[] misplaced = types
				.Where(type => type.Namespace == null
					|| !type.Namespace.StartsWith(ContextPrefix, StringComparison.Ordinal)
					|| namespaces.Any(other => other.StartsWith(type.Namespace + ".", StringComparison.Ordinal)))
				.OrderBy(type => type.FullName, StringComparer.Ordinal)
				.ToArray();

			FailureReport.AssertNone(misplaced.Length + " type(s) outside a leaf context namespace",
				misplaced, type => type.FullName);

			// - exact, one per folder with files: an empty or partial scan cannot pass
			// - bumping it is only correct for a deliberate new folder
			Assert.Equal(7, namespaces.Length);
		}

		// - the contexts must stay a DAG: a cycle means one cannot be read without the others
		[Fact]
		public void NoContextsDependOnEachOtherInACycle()
		{
			FailureReport.AssertNoneInFull("These contexts depend on each other in a cycle",
				ContextDependencies.Cycles(ContextEdges()).ToArray());
		}

		// - exact: a new edge is a design decision, and an empty scan cannot pass
		// - edges only, no files: a new test file on a known edge needs no bump
		private static readonly string[] PinnedContextEdges =
		{
			"Consumption.Expressions -> Consumption.Fields",
			"Consumption.Fields -> Consumption.Bonus",
			"Consumption.Fields -> Rulebook",
			"Rulebook -> Consumption.Bonus",
		};

		[Fact]
		public void TheContextEdgesAreThePinnedOnes()
		{
			string[] actual = ContextEdges().Select(edge => edge.From + " -> " + edge.To)
				.Distinct(StringComparer.Ordinal).ToArray();

			FailureReport.AssertNoneInFull("Context edges differ from the pinned list",
				actual.Except(PinnedContextEdges, StringComparer.Ordinal).Select(edge => "new: " + edge)
					.Concat(PinnedContextEdges.Except(actual, StringComparer.Ordinal).Select(edge => "gone: " + edge))
					.ToArray());
		}

		// - the kernel still imports contexts: RuleCorpus until registration, DataPathsTests until the pins move
		// - pinned per file so the list can only shrink
		private static readonly IReadOnlyDictionary<string, string> PinnedKernelImports =
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				{ "DataPathsTests.cs", "Consumption.Bonus, Consumption.Expressions, Consumption.Fields, Localization, Rulebook" },
				{ "RuleCorpus.cs", "Consumption.Bonus, Consumption.Expressions, Consumption.Fields, Rulebook" },
			};

		[Fact]
		public void TheKernelImportsOnlyThePinnedContexts()
		{
			PinReport.AssertEqual("Kernel imports", PinnedKernelImports, ContextDependencies.TestProjectEdges
				.Where(edge => edge.From == ContextDependencies.Kernel)
				.GroupBy(edge => edge.File, StringComparer.Ordinal)
				.ToDictionary(group => group.Key,
					group => string.Join(", ", group.Select(edge => edge.To).OrderBy(to => to, StringComparer.Ordinal)),
					StringComparer.Ordinal));
		}

		// - kernel edges left out: the kernel is shared, its own imports are pinned above
		private static ContextDependencies.Edge[] ContextEdges()
		{
			return ContextDependencies.TestProjectEdges
				.Where(edge => edge.From != ContextDependencies.Kernel && edge.To != ContextDependencies.Kernel)
				.ToArray();
		}
	}
}
