using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AutoPBW
{
	public static class HttpUtility
	{
		public static HttpStatusCode SubmitForm(string url, IDictionary<string, string> fields, CookieContainer? cookies = null, string action = "submitting form")
		{
			HttpWebRequest request;
			HttpWebResponse response;
			try
			{
				request = (HttpWebRequest)WebRequest.Create(url);
				request.CookieContainer = cookies ?? new CookieContainer();
				request.Method = "POST";
				request.ContentType = "application/x-www-form-urlencoded";

				using (StreamWriter writer = new StreamWriter(request.GetRequestStream(), Encoding.ASCII))
				{
					writer.Write(fields.ToQueryString());
				}

				response = (HttpWebResponse)request.GetResponse();

				var status = response.StatusCode;

				Log.Write($"Connection status to {response.ResponseUri} is {(int)response.StatusCode} {response.StatusCode}: {response.StatusDescription}");
				if (response.Cookies != null)
					Log.Write("Cookies exist.");
				else
					Log.Write("Cookies do not exist.");

				cookies = request.CookieContainer;

				StreamReader responseReader = new StreamReader(response.GetResponseStream());
				string fullResponse = responseReader.ReadToEnd();
				response.Close();
				return status;
			}
			catch (WebException ex)
			{
				Log.Write($"Error while {action}:");
				Log.Write(ex.ToString());
				throw;
			}
		}
	}
}
