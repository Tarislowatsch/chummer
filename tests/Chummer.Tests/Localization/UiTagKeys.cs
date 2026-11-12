using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Chummer.Tests.Kernel;

namespace Chummer.Tests.Localization
{
	// - a control's Tag is a GetString key wherever a lookup reads that control's type
	// - UpdateControls catches a miss, but rethrows under _blnDebug
	// - every ToolStrip lookup outside the language manager has no try at all
	public static class UiTagKeys
	{
		// One string literal written to a Tag, in a designer file or at run time.
		public sealed class Assignment
		{
			public Assignment(string site, string receiver, string declaredType, string expression, string value)
			{
				Site = site;
				Receiver = receiver;
				DeclaredType = declaredType;
				Expression = expression;
				Value = value;
			}

			// - the right-hand side as written
			public string Expression { get; }

			// - a computed value that starts with a literal is a key built at run time
			public bool IsComposed => Value == null && Expression.StartsWith("\"", StringComparison.Ordinal);

			public string Site { get; }

			public string Receiver { get; }

			// - null when no declaration names the receiver unambiguously
			public string DeclaredType { get; }

			// - null for anything but one string literal
			public string Value { get; }
		}

		// One GetString call that takes its key from a Tag.
		public sealed class Lookup
		{
			public Lookup(string file, string argument, int calls, Type reads, string filter, Func<string, bool> accepts)
			{
				File = file;
				Argument = argument;
				Calls = calls;
				Reads = reads;
				Filter = filter;
				Accepts = accepts;
			}

			public string File { get; }

			public string Argument { get; }

			// - several calls share one argument text: the table pins how many
			public int Calls { get; }

			// - the widest type the calls enumerate: the check over-approximates
			public Type Reads { get; }

			public string Filter { get; }

			public Func<string, bool> Accepts { get; }
		}

		private static readonly Func<string, bool> Always = tag => true;

		// - hand-written: the read type sits in the enclosing loop, not in the call
		public static readonly IReadOnlyList<Lookup> Lookups = new[]
		{
			new Lookup("clsLanguageManager.cs", "lblLabel.Tag.ToString()", 1, typeof(Label), null, Always),
			new Lookup("clsLanguageManager.cs", "cmdButton.Tag.ToString()", 1, typeof(Button), null, Always),
			// clsLanguageManager.cs:265
			new Lookup("clsLanguageManager.cs", "chkCheckbox.Tag.ToString()", 1, typeof(CheckBox),
				"contains '_'", tag => tag.Contains("_")),
			new Lookup("clsLanguageManager.cs", "objHeader.Tag.ToString()", 1, typeof(ColumnHeader), null, Always),
			new Lookup("clsLanguageManager.cs", "tabPage.Tag.ToString()", 1, typeof(TabPage), null, Always),
			// - clsLanguageManager.cs:357, level-0 nodes only: the check takes every level
			new Lookup("clsLanguageManager.cs", "objNode.Tag.ToString()", 1, typeof(TreeNode),
				"starts with 'Node_'", tag => tag.StartsWith("Node_", StringComparison.Ordinal)),
			new Lookup("clsLanguageManager.cs", "objForm.Tag.ToString()", 1, typeof(Form), null, Always),
			new Lookup("clsLanguageManager.cs", "tssLabel.Tag.ToString()", 1, typeof(ToolStripStatusLabel), null, Always),
			new Lookup("ContactControl.cs", "objItem.Tag.ToString()", 1, typeof(ToolStripItem), null, Always),
			new Lookup("PetControl.cs", "objItem.Tag.ToString()", 1, typeof(ToolStripItem), null, Always),
			new Lookup("SpiritControl.cs", "objItem.Tag.ToString()", 1, typeof(ToolStripItem), null, Always),
			// - two loops over menu items, two over every context-menu item
			new Lookup("frmCareer.cs", "objItem.Tag.ToString()", 4, typeof(ToolStripItem), null, Always),
			new Lookup("frmCreate.cs", "objItem.Tag.ToString()", 4, typeof(ToolStripItem), null, Always),
			new Lookup("frmMain.cs", "objItem.Tag.ToString()", 3, typeof(ToolStripMenuItem), null, Always),
			new Lookup("frmMain.cs", "objButton.Tag.ToString()", 3, typeof(ToolStripButton), null, Always),
		};

		private static readonly Regex TagWrite = new Regex(@"(?<![\w.])(?:this\.)?(\w+)\.Tag\s*=(?!=)\s*([^;]*);");

		private static readonly Regex Literal = new Regex(@"^""([^""\\]*)""$");

		// - type, then name, then the end of a declarator or a foreach
		private static readonly Regex Declaration = new Regex(@"(?<![\w.])([A-Za-z_][\w.]*)\s+(\w+)\s*(?:=(?!=)|;|\bin\b)");

		private static readonly HashSet<string> NotATypeName = new HashSet<string>(StringComparer.Ordinal)
		{
			"return", "new", "else", "case", "in", "out", "ref", "throw", "await", "goto",
		};

		private static readonly Regex PartialClass = new Regex(@"\bpartial\s+class\s+(\w+)");

		// - public so the detection tests can drive it with hand-built source
		// - companion is the other half of a partial class: designer fields serve the code file
		public static IEnumerable<Assignment> AssignmentsIn(string fileName, SourceLines source, SourceLines companion)
		{
			string[] code = source.Code;
			string[] other = companion.Bare;
			string className = source.Bare.Concat(other).Select(line => PartialClass.Match(line))
				.FirstOrDefault(match => match.Success)?.Groups[1].Value;

			Declared[] here = DeclarationsIn(source.Bare);
			Declared[] there = DeclarationsIn(other);

			for (int i = 0; i < code.Length; i++)
			{
				foreach (Match match in TagWrite.Matches(code[i]))
				{
					string receiver = match.Groups[1].Value;
					string declared = receiver == "this"
						? className
						: DeclaredType(receiver, i, here, there);
					string expression = match.Groups[2].Value.Trim();
					Match literal = Literal.Match(expression);

					yield return new Assignment(fileName + ":" + (i + 1), receiver, declared, expression,
						literal.Success ? literal.Groups[1].Value : null);
				}
			}
		}

		// - nearest declaration above the write first: local names repeat with other types
		// - then a unique declaration anywhere: designer fields come after InitializeComponent
		// - literals stripped: text in a string must not declare anything
		private static string DeclaredType(string name, int line, Declared[] here, Declared[] there)
		{
			Declared above = here.LastOrDefault(entry => entry.Name == name && entry.Line <= line);
			if (above != null)
				return above.Type;

			string[] candidates = here.Concat(there)
				.Where(entry => entry.Name == name)
				.Select(entry => entry.Type)
				.Distinct(StringComparer.Ordinal)
				.ToArray();
			return candidates.Length == 1 ? candidates[0] : null;
		}

		private sealed class Declared
		{
			public int Line;
			public string Type;
			public string Name;
		}

		private static Declared[] DeclarationsIn(string[] lines)
		{
			return lines
				.SelectMany((line, index) => Declaration.Matches(line).Cast<Match>()
					.Where(match => !NotATypeName.Contains(match.Groups[1].Value))
					.Select(match => new Declared
					{
						Line = index,
						Type = match.Groups[1].Value,
						Name = match.Groups[2].Value,
					}))
				.ToArray();
		}

		private static readonly Regex ClassBase = new Regex(@"\bclass\s+(\w+)\s*:\s*([\w.]+)");

		// - public so the detection tests can drive it with hand-built source
		public static IReadOnlyDictionary<string, string> ClassBasesIn(IEnumerable<string> sources)
		{
			Dictionary<string, string> bases = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (Match match in sources.SelectMany(source => ClassBase.Matches(source).Cast<Match>()))
				bases[match.Groups[1].Value] = match.Groups[2].Value;
			return bases;
		}

		// - a WinForms type by name, else the application class's base, followed up
		// - null when the chain ends outside WinForms
		public static Type Resolve(string typeName, IReadOnlyDictionary<string, string> classBases)
		{
			for (int depth = 0; typeName != null && depth < 16; depth++)
			{
				string simple = typeName.Substring(typeName.LastIndexOf('.') + 1);
				Type type = typeof(Control).Assembly.GetType("System.Windows.Forms." + simple);
				if (type != null)
					return type;

				typeName = classBases.TryGetValue(simple, out string next) ? next : null;
			}

			return null;
		}

		// - null when no lookup reads this tag
		public static Lookup ReadBy(Type type, string tag)
		{
			return Lookups.FirstOrDefault(lookup => lookup.Reads.IsAssignableFrom(type) && lookup.Accepts(tag));
		}

		// - one implementation for the real source and the hand-built cases
		// - unresolved and computed writes are left to their own guards
		public static IEnumerable<Assignment> MissingKeys(IEnumerable<Assignment> assignments,
			IReadOnlyDictionary<string, string> classBases, IReadOnlyCollection<string> keys)
		{
			HashSet<string> known = new HashSet<string>(keys, StringComparer.Ordinal);
			return Read(assignments, classBases, true).Where(assignment => !known.Contains(assignment.Value));
		}

		public static IEnumerable<Assignment> Unread(IEnumerable<Assignment> assignments,
			IReadOnlyDictionary<string, string> classBases)
		{
			return Read(assignments, classBases, false);
		}

		private static IEnumerable<Assignment> Read(IEnumerable<Assignment> assignments,
			IReadOnlyDictionary<string, string> classBases, bool read)
		{
			return assignments
				.Where(assignment => assignment.Value != null)
				.Select(assignment => new { assignment, type = Resolve(assignment.DeclaredType, classBases) })
				.Where(item => item.type != null && (ReadBy(item.type, item.assignment.Value) != null) == read)
				.Select(item => item.assignment);
		}

		public static bool IsDesignerFile(string site)
		{
			return site.Contains(".Designer.cs:");
		}

		public static IReadOnlyList<Assignment> AllAssignments => CachedAssignments.Value;

		private static readonly Lazy<IReadOnlyList<Assignment>> CachedAssignments =
			new Lazy<IReadOnlyList<Assignment>>(() => SourceCorpus.Files
				.SelectMany(file => AssignmentsIn(file.Name, file.Source,
					CompanionOf(file.Path)))
				.ToArray());

		private static SourceLines CompanionOf(string path)
		{
			string other = path.EndsWith(".Designer.cs", StringComparison.Ordinal)
				? path.Substring(0, path.Length - ".Designer.cs".Length) + ".cs"
				: path.Substring(0, path.Length - ".cs".Length) + ".Designer.cs";
			return SourceCorpus.At(other)?.Source ?? SourceLines.Of(new string[0]);
		}

		public static IReadOnlyDictionary<string, string> AllClassBases => CachedClassBases.Value;

		private static readonly Lazy<IReadOnlyDictionary<string, string>> CachedClassBases =
			new Lazy<IReadOnlyDictionary<string, string>>(() => ClassBasesIn(
				SourceCorpus.Files.Select(file => file.Source.JoinedCode)));
	}
}
