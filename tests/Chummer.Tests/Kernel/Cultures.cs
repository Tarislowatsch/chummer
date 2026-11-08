using System.Globalization;

namespace Chummer.Tests.Kernel
{
	public static class Cultures
	{
		// - GlobalOptions.CultureInfo (clsOptions.cs:80), the one culture the code passes explicitly
		// - new, not GetCultureInfo: the application picks up user overrides the same way
		public static readonly CultureInfo Application = new CultureInfo("en-US");
	}
}
