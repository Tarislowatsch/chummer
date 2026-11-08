using System;
using System.Linq;
using Xunit;

namespace Chummer.Tests.Kernel
{
	// - a child namespace sees its parent's types without a using
	// - a type in a parent therefore hides edges the using lines are meant to list
	public class NamespaceLayoutTests
	{
		private const string Root = "Chummer.Tests";

		[Fact]
		public void ParentNamespacesDeclareNoTypes()
		{
			Type[] types = typeof(NamespaceLayoutTests).Assembly.GetTypes()
				.Where(type => !type.IsNested && type.Namespace != null
					&& (type.Namespace == Root || type.Namespace.StartsWith(Root + ".", StringComparison.Ordinal)))
				.ToArray();
			string[] namespaces = types.Select(type => type.Namespace).Distinct(StringComparer.Ordinal).ToArray();
			Type[] inParents = types
				.Where(type => namespaces.Any(other => other.StartsWith(type.Namespace + ".", StringComparison.Ordinal)))
				.OrderBy(type => type.FullName, StringComparer.Ordinal)
				.ToArray();

			if (inParents.Length > 0)
			{
				Assert.Fail(FailureReport.Build(inParents.Length + " type(s) in a namespace that has child namespaces",
					inParents, type => type.FullName));
			}

			// - exact, one per folder: an empty or partial scan cannot pass
			// - bumping it is only correct for a deliberate new folder
			Assert.Equal(7, namespaces.Length);
		}
	}
}
