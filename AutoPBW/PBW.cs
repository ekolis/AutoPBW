using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Web;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AutoPBW
{
	/// <summary>
	/// Adapted from Jon Sorensen's PBW Autoclient
	/// https://github.com/se5a/PBWAutoClient
	/// </summary>
	public static class PBW
	{
		/// <summary>
		/// For parsing UNIX timestamps
		/// </summary>
		private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		private static DateTime? UnixTimeToDateTime(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return null;
			double seconds = double.Parse(text, CultureInfo.InvariantCulture);
			if (seconds == 0)
				return null; // HACK - PBW returns zero for nulls
			return Epoch.AddSeconds(seconds).ToLocalTime();
		}

		private static string ToQueryString(this IDictionary<string, string> q)
		{
			var array = (from key in q.Keys
						 select string.Format("{0}={1}", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(q[key])))
				.ToArray();
			return string.Join("&", array);
		}

		/// <summary>
		/// Shortcut for string.Format
		/// </summary>
		/// <param name="f"></param>
		/// <param name="data"></param>
		/// <returns></returns>
		public static string F(this string f, params object[] data)
		{
			return string.Format(f, data);
		}

		private static CookieContainer cookies;

		/// <summary>
		/// Logs into PBW using HTTPS.
		/// </summary>
		/// <param name="login_address">The full login path.</param>
		/// <param name="username">The username.</param>
		/// <param name="password">The password.</param>
		/// <remarks>
		/// Todo: handle errors. better.
		/// </remarks>
		public static void Login(string username, string password)
		{
			var login_address = "https://pbw.spaceempires.net/login/process";
			Log.Write("Attempting to connect to PBW at {0} using username={1} and password={2}.".F(login_address, username, new string('*', password.Length)));
			var fields = new Dictionary<string, string>
			{
				{"username", username},
				{"password", password},
			};
			SubmitForm(login_address, fields, "logging in");
		}

		public static void SubmitForm(string url, IDictionary<string, string> fields, string action = "submitting form")
		{
			HttpWebRequest request;
			HttpWebResponse response;
			
			try
			{
				request = (HttpWebRequest)WebRequest.Create(url);
				request.Method = "POST";
				request.ContentType = "application/x-www-form-urlencoded";
				using (StreamWriter writer = new StreamWriter(request.GetRequestStream(), Encoding.ASCII))
				{
					writer.Write(fields.ToQueryString());
				}
				request.CookieContainer = new CookieContainer();
				response = (HttpWebResponse)request.GetResponse();
				

				ConnectionStatus = response.StatusCode;

				Log.Write("Connection status to {0} is {1} {2}".F(response.Server, response.StatusCode, response.StatusDescription));
				if (response.Cookies != null)
					Log.Write("Cookies exist.");
				else
					Log.Write("Cookies do not exist.");

				cookies = request.CookieContainer;

				StreamReader responseReader = new StreamReader(response.GetResponseStream());
				string fullResponse = responseReader.ReadToEnd();
				response.Close();
			}
			catch (WebException ex)
			{
				Log.Write("Error while {0}:".F(action));
				Log.Write(ex.ToString());
				throw;
			}
		}

		/// <summary>
		/// Gets XML data from PBW.
		/// </summary>
		/// <param name="cookies">Cookies from a previous authentication.</param>
		/// <param name="url">URL of page.</param>
		/// <returns>
		/// XML games list, or throws an exception if something went wrong
		/// </returns>
		/// <remarks>
		/// Todo: 
		/// handle errors,
		/// login if not, etc.
		/// </remarks>
		private static XDocument GetXMLData(string url)
		{
			string returnString = null;

			StringBuilder sb = new StringBuilder();
			byte[] buf = new byte[8192];

			HttpWebRequest request;
			HttpWebResponse response;
			Stream resStream = null;
			string tempString = null;

			try
			{
				request = (HttpWebRequest)WebRequest.Create(url);
				request.CookieContainer = cookies;

				request.UserAgent = "AutoPBW/" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
				response = (HttpWebResponse)request.GetResponse();
				resStream = response.GetResponseStream();

				int count = 0;
				do
				{
					count = resStream.Read(buf, 0, buf.Length);

					if (count != 0)
					{
						tempString = Encoding.ASCII.GetString(buf, 0, count);
						sb.Append(tempString);
					}
				}
				while (count > 0);

				returnString = sb.ToString();
			}
			catch (WebException ex)
			{
				Log.Write("Error while getting XML data:");
				Log.Write(ex.ToString());
				throw;
			}
			finally
			{
				if (resStream != null)
				{ resStream.Close(); }
			}
			return XDocument.Parse(returnString);
		}

		/// <summary>
		/// Gets games that the player is hosting that are in need of processing.
		/// </summary>
		/// <returns></returns>
		public static IEnumerable<HostGame> GetHostGames()
		{
			var xml = GetXMLData("http://pbw.spaceempires.net/node/host");
			var hx = xml.Element("host");
			MaxFileSize = int.Parse(hx.Attribute("max_file_size").Value);
			NextUpdateTime = DateTime.Now.AddSeconds(double.Parse(hx.Attribute("update_interval").Value));
			foreach (var gx in hx.Element("empires_ready").Element("games").Elements("game"))
			{
				var g = LoadHostGame(gx);
				g.Status = HostStatus.EmpiresReady;
				yield return g;
			}
			foreach (var gx in hx.Element("host_ready").Element("games").Elements("game"))
			{
				var g = LoadHostGame(gx);
				g.Status = HostStatus.HostReady;
				yield return g;
			}
			foreach (var gx in hx.Element("players_ready").Element("games").Elements("game"))
			{
				var g = LoadHostGame(gx);
				g.Status = HostStatus.PlayersReady;
				yield return g;
			}
		}

		/// <summary>
		/// Gets games that the player is playing.
		/// </summary>
		/// <returns></returns>
		public static IEnumerable<PlayerGame> GetPlayerGames()
		{
			var xml = GetXMLData("http://pbw.spaceempires.net/node/player");
			var gamesxml = xml.Element("games");
			MaxFileSize = int.Parse(gamesxml.Attribute("max_file_size").Value);
			NextUpdateTime = DateTime.Now.AddSeconds(double.Parse(gamesxml.Attribute("update_interval").Value));
			foreach (var gx in gamesxml.Elements("game"))
			{
				var g = LoadPlayerGame(gx);
				yield return g;
			}
		}

		#region Game loading helpers
		private static HostGame LoadHostGame(XElement gx)
		{
			var g = new HostGame();
			g.Code = gx.Element("game_code").Value;
			g.Password = gx.Element("game_password").Value;
			g.Mod = Mod.Find(gx.Element("mod_code").Value, gx.Element("game_type").Value);
			g.TurnMode = Game.ParseTurnMode(gx.Element("turn_mode").Value);
			g.TurnNumber = int.Parse(gx.Element("turn").Value);
			if (gx.Element("next_turn_date") != null)
				g.TurnDueDate = UnixTimeToDateTime(gx.Element("next_turn_date").Value);
			return g;
		}

		private static PlayerGame LoadPlayerGame(XElement gx)
		{
			var g = new PlayerGame();
			g.Code = gx.Element("game_code").Value;
			g.Password = gx.Element("empire_password").Value;
			g.Mod = Mod.Find(gx.Element("mod_code").Value, gx.Element("game_type").Value);
			g.TurnMode = Game.ParseTurnMode(gx.Element("turn_mode").Value);
			g.TurnNumber = int.Parse(gx.Element("turn").Value);
			g.TurnDueDate = UnixTimeToDateTime(gx.Element("next_turn_date").Value);
			g.Status = PlayerGame.ParseStatus(gx.Element("plr_status").Value);
			g.PlayerNumber = int.Parse(gx.Element("number").Value);
			g.ShipsetCode = gx.Element("shipset_code").Value;
			return g;
		}
		#endregion

		/// <summary>
		/// Downloads a file from PBW.
		/// </summary>
		/// <param name="fullurl"></param>
		/// <param name="downloadfilename"></param>
		public static void Download(string fullurl, string downloadfilename)
		{
			Log.Write("Attempting to download from {0} and save as {1}.".F(fullurl, downloadfilename));
			FileStream fileStream = File.Create(downloadfilename);

			try
			{
				HttpWebRequest request = (HttpWebRequest)WebRequest.Create(fullurl);
				request.CookieContainer = cookies;
				request.Method = WebRequestMethods.Http.Get;

				HttpWebResponse response = (HttpWebResponse)request.GetResponse();
				Stream responseStream = response.GetResponseStream();

				Log.Write("response StatusCode is " + response.StatusCode + "\r\n");
				Log.Write("response StatusDescription is " + response.StatusDescription + "\r\n");

				int buffersize = 1024;
				byte[] buffer = new byte[buffersize];
				int bytesRead = 0;
				WebHeaderCollection headders = response.Headers;

				while ((bytesRead = responseStream.Read(buffer, 0, buffersize)) != 0)
				{
					fileStream.Write(buffer, 0, bytesRead);
				}
			}

			catch (WebException ex)
			{
				Log.Write("Error while downloading file:");
				Log.Write(ex.ToString());
				throw;
			}
			finally
			{
				fileStream.Close();
			}
		}


		public static bool Upload(string file, string uploadurl, string uploadFormParam, HttpStatusCode expectedStatus = HttpStatusCode.OK)
		{
			//adapted from and many thanks to: http://www.briangrinstead.com/blog/multipart-form-post-in-c
			Log.Write("Attempting to upload {0} to {1} as form field {2}.".F(file, uploadurl, uploadFormParam));
			bool success = false;

			string filename = Path.GetFileName(file);
			string fileformat = uploadFormParam;
			var maxfilesize = MaxFileSize == 0 ? 64 * 1024 * 1024 : MaxFileSize; // default 64 MB if not already known
			string content_type = "application/octet-stream";

			try
			{
				// Read file data
				FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read);
				byte[] data = new byte[fs.Length];
				fs.Read(data, 0, data.Length);
				fs.Close();

				// Generate post objects
				Dictionary<string, object> postParameters = new Dictionary<string, object>();
				postParameters.Add("MAX_FILE_SIZE", maxfilesize);
				postParameters.Add("fileformat", fileformat);
				postParameters.Add(uploadFormParam, new FormUpload.FileParameter(data, filename, content_type));

				// Create request and receive response
				string postURL = uploadurl;
				string userAgent = "AutoPBW/" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
				HttpWebResponse webResponse = FormUpload.MultipartFormDataPost(cookies, postURL, userAgent, postParameters);

				// Process response
				StreamReader responseReader = new StreamReader(webResponse.GetResponseStream());
				if (webResponse.StatusCode == expectedStatus)
					success = true;
				webResponse.Close();
			}

			catch (Exception ex)
			{
				Log.Write("Error while uploading file:");
				Log.Write(ex.ToString());
				throw;
			}

			return success;
		}

		/// <summary>
		/// PBW log
		/// </summary>
		public static class Log
		{
			private static List<string> log = new List<string>();
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

		public static HttpStatusCode ConnectionStatus { get; private set; }

		/// <summary>
		/// Maximum upload size allowed by PBW.
		/// </summary>
		public static int MaxFileSize { get; private set; }

		/// <summary>
		/// When we can check PBW again.
		/// </summary>
		public static DateTime NextUpdateTime { get; private set; }

		// Implements multipart/form-data POST in C# http://www.ietf.org/rfc/rfc2388.txt
		// http://www.briangrinstead.com/blog/multipart-form-post-in-c
		private static class FormUpload
		{
			private static readonly Encoding encoding = Encoding.UTF8;
			public static HttpWebResponse MultipartFormDataPost(CookieContainer cookies, string postUrl, string userAgent, Dictionary<string, object> postParameters)
			{
				string formDataBoundary = String.Format("----------{0:N}", Guid.NewGuid());
				string contentType = "multipart/form-data; boundary=" + formDataBoundary;

				byte[] formData = GetMultipartFormData(postParameters, formDataBoundary);

				return PostForm(cookies, postUrl, userAgent, contentType, formData);
			}
			private static HttpWebResponse PostForm(CookieContainer cookies, string postUrl, string userAgent, string contentType, byte[] formData)
			{
				HttpWebRequest request = WebRequest.Create(postUrl) as HttpWebRequest;

				if (request == null)
				{
					throw new NullReferenceException("request is not a http request");
				}

				// Set up the request properties.
				request.Method = "POST";
				request.ContentType = contentType;
				request.UserAgent = userAgent;
				request.CookieContainer = cookies;
				request.ContentLength = formData.Length;

				// You could add authentication here as well if needed:
				// request.PreAuthenticate = true;
				// request.AuthenticationLevel = System.Net.Security.AuthenticationLevel.MutualAuthRequested;
				// request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.Default.GetBytes("username" + ":" + "password")));

				// Send the form data to the request.
				using (Stream requestStream = request.GetRequestStream())
				{
					requestStream.Write(formData, 0, formData.Length);
					requestStream.Close();
				}

				return request.GetResponse() as HttpWebResponse;
			}

			private static byte[] GetMultipartFormData(Dictionary<string, object> postParameters, string boundary)
			{
				Stream formDataStream = new System.IO.MemoryStream();
				bool needsCLRF = false;

				foreach (var param in postParameters)
				{
					// Thanks to feedback from commenters, add a CRLF to allow multiple parameters to be added.
					// Skip it on the first parameter, add it to subsequent parameters.
					if (needsCLRF)
						formDataStream.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

					needsCLRF = true;

					if (param.Value is FileParameter)
					{
						FileParameter fileToUpload = (FileParameter)param.Value;

						// Add just the first part of this param, since we will write the file data directly to the Stream
						string header = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\";\r\nContent-Type: {3}\r\n\r\n",
							boundary,
							param.Key,
							fileToUpload.FileName ?? param.Key,
							fileToUpload.ContentType ?? "application/octet-stream");

						formDataStream.Write(encoding.GetBytes(header), 0, encoding.GetByteCount(header));

						// Write the file data directly to the Stream, rather than serializing it to a string.
						formDataStream.Write(fileToUpload.File, 0, fileToUpload.File.Length);
					}
					else
					{
						string postData = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}",
							boundary,
							param.Key,
							param.Value);
						formDataStream.Write(encoding.GetBytes(postData), 0, encoding.GetByteCount(postData));
					}
				}

				// Add the end of the request.  Start with a newline
				string footer = "\r\n--" + boundary + "--\r\n";
				formDataStream.Write(encoding.GetBytes(footer), 0, encoding.GetByteCount(footer));

				// Dump the Stream into a byte[]
				formDataStream.Position = 0;
				byte[] formData = new byte[formDataStream.Length];
				formDataStream.Read(formData, 0, formData.Length);
				formDataStream.Close();

				return formData;
			}

			public class FileParameter
			{
				public byte[] File { get; set; }
				public string FileName { get; set; }
				public string ContentType { get; set; }
				public FileParameter(byte[] file) : this(file, null) { }
				public FileParameter(byte[] file, string filename) : this(file, filename, null) { }
				public FileParameter(byte[] file, string filename, string contenttype)
				{
					File = file;
					FileName = filename;
					ContentType = contenttype;
				}
			}
		}
	}
}
