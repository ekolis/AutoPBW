using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

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

		/// <summary>
		/// Converts a dictionary to a query string.
		/// </summary>
		/// <param name="q"></param>
		/// <returns></returns>
		public static string ToQueryString(this IDictionary<string, string> q)
		{
			var array = (from key in q.Keys
						 select string.Format("{0}={1}", System.Web.HttpUtility.UrlEncode(key), System.Web.HttpUtility.UrlEncode(q[key])))
				.ToArray();
			return string.Join("&", array);
		}

		/// <summary>
		/// Redacts a string by replacing all characters with asterisks.
		/// </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		public static string? Redact(this string? s)
		{
			if (s is null)
				return null;
			return new string('*', s.Length);
		}

		/// <summary>
		/// Shorthand for <see cref="string.IsNullOrWhiteSpace(string?)"/>.
		/// </summary>
		/// <param name="s"></param>
		/// <returns></returns>
		public static bool IsBlank(this string? s)
		{
			return string.IsNullOrWhiteSpace(s);
		}
	}
}
