using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Chummer.Tests.Kernel
{
	// - edges from using lines and qualified names: a sibling context is reachable no other way
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

		private static readonly Regex Reference = new Regex(@"\bChummer\.Tests\.([\w.]+)");

		// - public so the detection tests can drive it with hand-built source
		// - the longest known context wins: Consumption.Fields, not Consumption
		public static IEnumerable<Edge> EdgesIn(string file, SourceLines source, IReadOnlyCollection<string> contexts)
		{
			string from = source.Code.Select(line => Namespace.Match(line)).Where(match => match.Success)
				.Select(match => ContextOf(match.Groups[1].Value, contexts)).FirstOrDefault();
			if (from == null)
				return Enumerable.Empty<Edge>();

			return source.Bare
				.SelectMany(line => Reference.Matches(line).Cast<Match>())
				.Select(match => ContextOf(Prefix + match.Groups[1].Value, contexts))
				.Where(to => to != null && to != from)
				.Distinct(StringComparer.Ordinal)
				.Select(to => new Edge(file, from, to))
				.ToArray();
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
			string generated = Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar;
			string[] files = Directory
				.EnumerateFiles(Path.Combine(DataPaths.RepoRoot, "tests", "Chummer.Tests"), "*.cs", SearchOption.AllDirectories)
				.Where(path => path.IndexOf(generated, StringComparison.Ordinal) < 0)
				.OrderBy(path => path, StringComparer.Ordinal)
				.ToArray();
			string[] contexts = typeof(ContextDependencies).Assembly.GetTypes()
				.Select(type => type.Namespace)
				.Where(name => name != null && name.StartsWith(Prefix, StringComparison.Ordinal))
				.Select(name => name.Substring(Prefix.Length))
				.Distinct(StringComparer.Ordinal)
				.ToArray();

			return files
				.SelectMany(path => EdgesIn(Path.GetFileName(path), SourceLines.Of(File.ReadAllLines(path)), contexts))
				.ToArray();
		});
	}
}
