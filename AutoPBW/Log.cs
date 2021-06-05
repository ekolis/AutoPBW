using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPBW
{
	/// <summary>
	/// Log of all events captured by AutoPBW.
	/// </summary>
	public static class Log
	{
		private static readonly ObservableCollection<string> log = new ObservableCollection<string>();
		private static int readCount = 0;

		/// <summary>
		/// Writes to the log.
		/// </summary>
		/// <param name="text"></param>
		/// <param name="newline"></param>
		public static void Write(string text)
		{
			log.Add(text);
		}

		/// <summary>
		/// Reads all messages from the log, including ones that have already been read.
		/// </summary>
		/// <param name="markRead"></param>
		/// <returns></returns>
		public static IEnumerable<string> ReadAll(bool markRead = true)
		{
			if (markRead)
				readCount = log.Count;
			return log;
		}

		/// <summary>
		/// Reads any new messages from the log.
		/// </summary>
		/// <param name="markRead"></param>
		/// <returns></returns>
		public static IEnumerable<string> ReadNew(bool markRead = true)
		{
			var result = log.Skip(readCount).ToArray();
			if (markRead)
				readCount = log.Count;
			return result;
		}
	}
}
