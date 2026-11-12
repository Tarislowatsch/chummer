using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Chummer.Tests.Kernel
{
	// - edges from using lines and dotted names, resolved against the enclosing namespace
	// - an unused using counts too: it would hide a real edge added later
	public static class ContextDependencies
	{
		public const string Prefix = "Chummer.Tests.";

		public const string Kernel = "Kernel";

		public sealed class Edge
		{
			public Edge(string file, string from, string to)
			{
				File = file;
				From = from;
				To = to;
			}

			public string File { get; }

			public string From { get; }

			public string To { get; }
		}

		private static readonly Regex Namespace = new Regex(@"^\s*namespace\s+([\w.]+)");

		private static readonly Regex DottedName = new Regex(@"(?<![\w.])([A-Za-z_]\w*(?:\.\w+)+)");

		// - a using inside the namespace block may name a sibling with one bare segment
		private static readonly Regex UsingDirective = new Regex(@"^\s*using\s+(?:static\s+)?(?:\w+\s*=\s*)?([\w.]+)\s*;");

		// - public so the detection tests can drive it with hand-built source
		// - the longest known context wins: Consumption.Fields, not Consumption
		public static IEnumerable<Edge> EdgesIn(string file, SourceLines source, IReadOnlyCollection<string> contexts)
		{
			string enclosing = source.Code.Select(line => Namespace.Match(line)).Where(match => match.Success)
				.Select(match => match.Groups[1].Value).FirstOrDefault();
			string from = enclosing == null ? null : ContextOf(enclosing, contexts);
			if (from == null)
				return Enumerable.Empty<Edge>();

			HashSet<string> namespaces = new HashSet<string>(contexts
				.SelectMany(context => NamespacesAlong(Prefix + context)), StringComparer.Ordinal);

			return source.Bare
				.SelectMany(line => DottedName.Matches(line).Cast<Match>().Concat(UsingDirective.Matches(line).Cast<Match>()))
				.Select(match => Resolve(match.Groups[1].Value, enclosing, namespaces))
				.Where(name => name != null)
				.Select(name => ContextOf(name, contexts))
				.Where(to => to != null && to != from)
				.Distinct(StringComparer.Ordinal)
				.Select(to => new Edge(file, from, to))
				.ToArray();
		}

		// - "A.B.C" -> "A", "A.B", "A.B.C"
		private static IEnumerable<string> NamespacesAlong(string name)
		{
			string[] segments = name.Split('.');
			return Enumerable.Range(1, segments.Length).Select(count => string.Join(".", segments.Take(count)));
		}

		// - the first segment is looked up from the innermost namespace outwards, members ignored
		// - a same-named member can only add an edge
		// - "Expressions.X" inside Consumption.Bonus names Consumption.Expressions
		private static string Resolve(string name, string enclosing, ISet<string> namespaces)
		{
			string first = name.Split('.')[0];
			for (string scope = enclosing; ; scope = scope.Substring(0, Math.Max(0, scope.LastIndexOf('.'))))
			{
				string prefix = scope.Length == 0 ? "" : scope + ".";
				if (namespaces.Contains(prefix + first))
					return prefix + name;
				if (scope.Length == 0)
					return null;
			}
		}

		private static string ContextOf(string name, IReadOnlyCollection<string> contexts)
		{
			return contexts
				.Where(context => name == Prefix + context || name.StartsWith(Prefix + context + ".", StringComparison.Ordinal))
				.OrderByDescending(context => context.Length)
				.FirstOrDefault();
		}

		// - one line per strongly connected component, with the edges inside it
		public static IEnumerable<string> Cycles(IReadOnlyCollection<Edge> edges)
		{
			ILookup<string, string> next = edges.ToLookup(edge => edge.From, edge => edge.To, StringComparer.Ordinal);
			string[] nodes = edges.SelectMany(edge => new[] { edge.From, edge.To })
				.Distinct(StringComparer.Ordinal).OrderBy(node => node, StringComparer.Ordinal).ToArray();

			HashSet<string> placed = new HashSet<string>(StringComparer.Ordinal);
			foreach (string node in nodes.Where(node => !placed.Contains(node)))
			{
				string[] component = nodes.Where(other => Reaches(next, node, other) && Reaches(next, other, node)).ToArray();
				placed.UnionWith(component);
				if (component.Length > 1)
					yield return string.Join(" / ", component) + ": " + string.Join(", ", edges
						.Where(edge => component.Contains(edge.From) && component.Contains(edge.To))
						.Select(edge => edge.From + " -> " + edge.To + " (" + edge.File + ")")
						.OrderBy(line => line, StringComparer.Ordinal));
			}
		}

		private static bool Reaches(ILookup<string, string> next, string from, string to)
		{
			if (from == to)
				return true;

			HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal) { from };
			Queue<string> queue = new Queue<string>(new[] { from });
			while (queue.Count > 0)
			{
				foreach (string target in next[queue.Dequeue()].Where(seen.Add))
				{
					if (target == to)
						return true;
					queue.Enqueue(target);
				}
			}

			return false;
		}

		public static IReadOnlyList<Edge> TestProjectEdges => CachedEdges.Value;

		private static readonly Lazy<IReadOnlyList<Edge>> CachedEdges = new Lazy<IReadOnlyList<Edge>>(() =>
		{
			string[] contexts = typeof(ContextDependencies).Assembly.GetTypes()
				.Select(type => type.Namespace)
				.Where(name => name != null && name.StartsWith(Prefix, StringComparison.Ordinal))
				.Select(name => name.Substring(Prefix.Length))
				.Distinct(StringComparer.Ordinal)
				.ToArray();

			return SourceCorpus.TestProjectFiles
				.SelectMany(file => EdgesIn(file.Name, file.Source, contexts))
				.ToArray();
		});
	}
}
