using System;
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

			if (misplaced.Length > 0)
			{
				Assert.Fail(FailureReport.Build(misplaced.Length + " type(s) outside a leaf context namespace",
					misplaced, type => type.FullName));
			}

			// - exact, one per folder with files: an empty or partial scan cannot pass
			// - bumping it is only correct for a deliberate new folder
			Assert.Equal(7, namespaces.Length);
		}
	}
}
