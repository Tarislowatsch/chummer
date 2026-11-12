using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Chummer.Tests.Kernel;
using Xunit;

namespace Chummer.Tests.Localization
{
	// - hand-built source: the real code shows today's tags, never that a bad one is caught
	public class UiTagKeyDetectionTests
	{
		private static readonly SourceLines None = SourceLines.Of(new string[0]);

		private static readonly IReadOnlyDictionary<string, string> NoBases = new Dictionary<string, string>();

		[Fact]
		public void FindsADesignerTagWithItsFieldType()
		{
			UiTagKeys.Assignment assignment = UiTagKeys.AssignmentsIn("frmX.Designer.cs", SourceLines.Of(new[]
			{
				"this.lblName.Tag = \"Label_Name\";",
				"private System.Windows.Forms.Label lblName;",
			}), None).Single();

			Assert.Equal("frmX.Designer.cs:1", assignment.Site);
			Assert.Equal("System.Windows.Forms.Label", assignment.DeclaredType);
			Assert.Equal("Label_Name", assignment.Value);
		}

		[Fact]
		public void TakesTheFieldTypeFromTheCompanionFile()
		{
			UiTagKeys.Assignment assignment = UiTagKeys.AssignmentsIn("frmX.cs",
				SourceLines.Of(new[] { "chkA.Tag = \"+0\";" }),
				SourceLines.Of(new[] { "private System.Windows.Forms.CheckBox chkA;" })).Single();

			Assert.Equal("System.Windows.Forms.CheckBox", assignment.DeclaredType);
		}

		[Fact]
		public void PrefersTheNearestDeclarationAbove()
		{
			UiTagKeys.Assignment assignment = UiTagKeys.AssignmentsIn("frmX.cs", SourceLines.Of(new[]
			{
				"XmlNode objNode = null;",
				"TreeNode objNode = new TreeNode();",
				"objNode.Tag = \"Node_A\";",
			}), None).Single();

			Assert.Equal("TreeNode", assignment.DeclaredType);
		}

		[Fact]
		public void LeavesAnAmbiguousNameUnresolved()
		{
			UiTagKeys.Assignment assignment = UiTagKeys.AssignmentsIn("frmX.cs", SourceLines.Of(new[]
			{
				"objNode.Tag = \"Node_A\";",
				"XmlNode objNode;",
				"TreeNode objNode;",
			}), None).Single();

			Assert.Null(assignment.DeclaredType);
		}

		[Fact]
		public void ResolvesThisToThePartialClass()
		{
			UiTagKeys.Assignment assignment = UiTagKeys.AssignmentsIn("frmX.Designer.cs", SourceLines.Of(new[]
			{
				"partial class frmX",
				"this.Tag = \"Title_X\";",
			}), None).Single();

			Assert.Equal("frmX", assignment.DeclaredType);
		}

		[Fact]
		public void IgnoresACommentedOutTag()
		{
			Assert.Empty(UiTagKeys.AssignmentsIn("frmX.cs", SourceLines.Of(new[] { "// lblA.Tag = \"Label_A\";" }), None));
		}

		[Fact]
		public void KeepsAComputedTagWithoutValue()
		{
			UiTagKeys.Assignment assignment = UiTagKeys.AssignmentsIn("frmX.cs",
				SourceLines.Of(new[] { "objNode.Tag = objGear.InternalId;" }), None).Single();

			Assert.Null(assignment.Value);
			Assert.False(assignment.IsComposed);
		}

		[Fact]
		public void FlagsATagComposedFromALiteral()
		{
			UiTagKeys.Assignment assignment = UiTagKeys.AssignmentsIn("frmX.cs",
				SourceLines.Of(new[] { "lblA.Tag = \"Label_\" + strName;" }), None).Single();

			Assert.Null(assignment.Value);
			Assert.True(assignment.IsComposed);
		}

		[Fact]
		public void IgnoresAComparison()
		{
			Assert.Empty(UiTagKeys.AssignmentsIn("frmX.cs", SourceLines.Of(new[] { "if (objNode.Tag == \"Node_A\")" }), None));
		}

		[Fact]
		public void ResolvesAnApplicationClassThroughItsBase()
		{
			IReadOnlyDictionary<string, string> bases = UiTagKeys.ClassBasesIn(new[]
			{
				"public class SplitButton : Button {}",
				"public partial class frmX : Form {}",
			});

			Assert.Equal(typeof(Button), UiTagKeys.Resolve("SplitButton", bases));
			Assert.Equal(typeof(Form), UiTagKeys.Resolve("frmX", bases));
			Assert.Null(UiTagKeys.Resolve("XmlNode", bases));
		}

		[Fact]
		public void ReadsAMenuItemThroughTheWiderToolStripLookup()
		{
			Assert.NotNull(UiTagKeys.ReadBy(typeof(ToolStripMenuItem), "Menu_A"));
			Assert.NotNull(UiTagKeys.ReadBy(typeof(LinkLabel), "Label_A"));
		}

		[Fact]
		public void ReadsACheckBoxOnlyWithAnUnderscore()
		{
			Assert.NotNull(UiTagKeys.ReadBy(typeof(CheckBox), "Checkbox_A"));
			Assert.Null(UiTagKeys.ReadBy(typeof(CheckBox), "12"));
		}

		[Fact]
		public void ReadsATreeNodeOnlyWithTheNodePrefix()
		{
			Assert.NotNull(UiTagKeys.ReadBy(typeof(TreeNode), "Node_A"));
			Assert.Null(UiTagKeys.ReadBy(typeof(TreeNode), "Label_A"));
		}

		[Fact]
		public void ReadsNoTypeWithoutALookup()
		{
			Assert.Null(UiTagKeys.ReadBy(typeof(ContextMenuStrip), "Menu_A"));
			Assert.Null(UiTagKeys.ReadBy(typeof(RadioButton), "Label_A"));
			Assert.Null(UiTagKeys.ReadBy(typeof(UserControl), "Title_A"));
		}

		[Fact]
		public void MissingKeysReportsOnlyReadTagsWithoutKey()
		{
			UiTagKeys.Assignment[] assignments = UiTagKeys.AssignmentsIn("frmX.Designer.cs", SourceLines.Of(new[]
			{
				"this.lblA.Tag = \"Label_A\";",
				"this.lblB.Tag = \"Label_B\";",
				"this.chkC.Tag = \"7\";",
				"this.cmsD.Tag = \"Menu_D\";",
				"private System.Windows.Forms.Label lblA;",
				"private System.Windows.Forms.Label lblB;",
				"private System.Windows.Forms.CheckBox chkC;",
				"private System.Windows.Forms.ContextMenuStrip cmsD;",
			}), None).ToArray();

			Assert.Equal(new[] { "lblB" }, UiTagKeys.MissingKeys(assignments, NoBases, new[] { "Label_A" })
				.Select(assignment => assignment.Receiver));
			Assert.Equal(new[] { "chkC", "cmsD" }, UiTagKeys.Unread(assignments, NoBases)
				.Select(assignment => assignment.Receiver));
		}

		[Fact]
		public void ResolveIgnoresTheNamespace()
		{
			Assert.Equal(typeof(Label), UiTagKeys.Resolve("System.Windows.Forms.Label", NoBases));
		}
	}
}
