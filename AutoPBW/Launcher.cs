using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPBW
{
	/// <summary>
	/// Wrapper for Process.Start that can handle URLs.
	/// </summary>
	public static class Launcher
	{
		/// <summary>
		/// Launches an app or URL.
		/// </summary>
		/// <param name="runnable">The path to the app or the URL to launch.</param>
		/// <param name="args">Command line arguments.</param>
		/// <returns>The process started.</returns>
		public static Process? Launch(string runnable, string args = null)
		{
			return Process.Start(new ProcessStartInfo(runnable, args) { UseShellExecute = true });
		}
	}
}
