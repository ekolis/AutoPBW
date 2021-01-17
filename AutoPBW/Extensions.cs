using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPBW
{
	public static class Extensions
	{
		/// <summary>
		/// Determines the innermost exception of an exception.
		/// </summary>
		/// <param name="ex">The starting exception.</param>
		/// <returns>The innermost exception.</returns>
		public static Exception InnermostException(this Exception ex)
		{
			var list = new List<Exception>();
			var inner = ex;
			while (inner != null)
			{
				list.Add(inner);
				inner = inner.InnerException;
			}
			return list.Last();
		}
	}
}
