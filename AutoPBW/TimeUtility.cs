using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPBW
{
	public static class TimeUtility
	{
		private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		public static DateTime? UnixTimeToDateTime(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return null;
			double seconds = double.Parse(text, CultureInfo.InvariantCulture);
			if (seconds == 0)
				return null; // HACK - PBW returns zero for nulls
			return Epoch.AddSeconds(seconds).ToLocalTime();
		}
	}
}
