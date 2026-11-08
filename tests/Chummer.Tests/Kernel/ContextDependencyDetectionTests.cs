using System.Linq;
using Xunit;

namespace Chummer.Tests.Kernel
{
	public class ContextDependencyDetectionTests
	{
		private static readonly string[] Contexts = { "Kernel", "Rulebook", "Consumption.Bonus", "Consumption.Fields" };

		private static string[] TargetsOf(params string[] lines)
		{
			return ContextDependencies.EdgesIn("x.cs", SourceLines.Of(lines), Contexts).Select(edge => edge.To).ToArray();
		}

		[Fact]
		public void AUsingLineIsAnEdge()
		{
			Assert.Equal(new[] { "Consumption.Fields" },
				TargetsOf("using Chummer.Tests.Consumption.Fields;", "namespace Chummer.Tests.Consumption.Bonus"));
		}

		[Fact]
		public void AQualifiedNameIsAnEdge()
		{
			Assert.Equal(new[] { "Rulebook" },
				TargetsOf("namespace Chummer.Tests.Kernel", "{ var x = Chummer.Tests.Rulebook.NameUniqueness.Rules; }"));
		}

		[Fact]
		public void CommentsLiteralsAndTheOwnContextAreNoEdges()
		{
			Assert.Empty(TargetsOf(
				"// using Chummer.Tests.Rulebook;",
				"namespace Chummer.Tests.Consumption.Bonus",
				"{ string s = \"Chummer.Tests.Rulebook\"; Chummer.Tests.Consumption.Bonus.X y; }"));
		}

		[Fact]
		public void AnAliasCountsForTheContextItNames()
		{
			Assert.Equal(new[] { "Rulebook" },
				TargetsOf("using Names = Chummer.Tests.Rulebook.NameUniqueness;", "namespace Chummer.Tests.Kernel"));
		}

		[Fact]
		public void ACycleOverThreeContextsIsReported()
		{
			ContextDependencies.Edge[] edges =
			{
				new ContextDependencies.Edge("a.cs", "A", "B"),
				new ContextDependencies.Edge("b.cs", "B", "C"),
				new ContextDependencies.Edge("c.cs", "C", "A"),
				new ContextDependencies.Edge("d.cs", "C", "D"),
			};

			Assert.Equal("A / B / C: A -> B (a.cs), B -> C (b.cs), C -> A (c.cs)",
				Assert.Single(ContextDependencies.Cycles(edges)));
		}

		[Fact]
		public void AChainIsNoCycle()
		{
			ContextDependencies.Edge[] edges =
			{
				new ContextDependencies.Edge("a.cs", "A", "B"),
				new ContextDependencies.Edge("b.cs", "B", "C"),
				new ContextDependencies.Edge("c.cs", "A", "C"),
			};

			Assert.Empty(ContextDependencies.Cycles(edges));
		}
	}
}
