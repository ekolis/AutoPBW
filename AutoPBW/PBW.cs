using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Composition;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace AutoPBW
{
	/// <summary>
	/// Adapted from Jon Sorensen's PBW Autoclient
	/// https://github.com/se5a/PBWAutoClient
	/// </summary>
	[Export(typeof(IMultiplayerService))]
	public class PBW
		: IMultiplayerService
	{
		public string ServiceTypeName { get; } = "PBW";

		public string Name { get; set; } = "PBW";

		private static CookieContainer? cookies;

		private static string cachedIP = "64.22.124.205";

		private static readonly string userAgent = "AutoPBW/" + Assembly.GetExecutingAssembly().GetName().Version.ToString();

		private static HttpWebRequest RetryRequestWithCachedIP(HttpWebRequest originalRequest)
		{
			UriBuilder uriBuilder = new UriBuilder(originalRequest.RequestUri);
			uriBuilder.Host = cachedIP;
			HttpWebRequest request2 = (HttpWebRequest)WebRequest.Create(uriBuilder.ToString());
			request2.Host = originalRequest.Host;

			request2.CookieContainer = originalRequest.CookieContainer;
			request2.UserAgent = originalRequest.UserAgent;
			request2.Method = originalRequest.Method;
			request2.ContentType = originalRequest.ContentType;
			if (originalRequest.ContentLength >= 0) // HttpWebRequest throws an exception if we set ContentLength to -1 ourselves
				request2.ContentLength = originalRequest.ContentLength;
			request2.Timeout = originalRequest.Timeout;
			// add as needed
			return request2;
		}
		public bool IsLoggedIn { get; private set; } = false;

		public bool Login(string username, string password)
		{
			var url = $"http://pbw.spaceempires.net/login/process";

			Log.Write($"Attempting to connect to PBW at {url} using username={username} and password={password.Redact()}.");

			var fields = new Dictionary<string, string>
			{
				{"username", username},
				{"password", password},
			};

			try
			{
				var status = HttpUtility.SubmitForm(url, fields, cookies, "logging in");

				IsLoggedIn = (int)status < 400; // not an error? we should be logged in

				// if we login successfully, cache the IP address of the pbw web service in case we have connection issues later
				cachedIP = Dns.GetHostEntry(new Uri(url).Host).AddressList.First().ToString();
			}
			catch
			{
				IsLoggedIn = false;
			}

			return IsLoggedIn;
		}

		/// <summary>
		/// Enables logging in even if PBW's SSL certificate is invalid or expired.
		/// http://stackoverflow.com/questions/2675133/c-sharp-ignore-certificate-errors
		/// </summary>
		public static void OverrideBadCertificates()
		{
			ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
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
			string? returnString = null;

			StringBuilder sb = new StringBuilder();
			byte[] buf = new byte[8192];

			HttpWebRequest request;
			HttpWebResponse response;
			Stream resStream = null;

			try
			{
				request = (HttpWebRequest)WebRequest.Create(url);
				request.CookieContainer = cookies;

				request.UserAgent = userAgent;

				try
				{
					response = (HttpWebResponse)request.GetResponse();
				}
				catch (WebException)
				{
					request = RetryRequestWithCachedIP(request);
					response = (HttpWebResponse)request.GetResponse();
				}
				resStream = response.GetResponseStream();

				var sr = new StreamReader(resStream);
				returnString = sr.ReadToEnd();
			}
			catch (WebException ex)
			{
				Log.Write("Error while getting XML data:");
				Log.Write(ex.ToString());
				throw;
			}
			finally
			{
				if (resStream is not null)
					resStream.Close();
			}
			return XDocument.Parse(returnString);
		}

		/// <summary>
		/// Gets games that the player is hosting that are in need of processing.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<HostGame> GetHostGames()
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
		public IEnumerable<PlayerGame> GetPlayerGames()
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

		private static HostGame LoadHostGame(XElement gx)
		{
			var g = new HostGame();
			g.Code = gx.Element("game_code").Value;
			g.Password = gx.Element("game_password").Value;
			g.Mod = Mod.Find(gx.Element("mod_code").Value, gx.Element("game_type").Value);
			g.TurnMode = ParseTurnMode(gx.Element("turn_mode").Value);
			g.TurnNumber = int.Parse(gx.Element("turn").Value);
			if (gx.Element("next_turn_date") != null)
				g.TurnDueDate = TimeUtility.UnixTimeToDateTime(gx.Element("next_turn_date").Value);
			return g;
		}

		private static PlayerGame LoadPlayerGame(XElement gx)
		{
			var g = new PlayerGame();
			g.Code = gx.Element("game_code").Value;
			g.Password = gx.Element("empire_password").Value;
			g.Mod = Mod.Find(gx.Element("mod_code").Value, gx.Element("game_type").Value);
			g.TurnMode = ParseTurnMode(gx.Element("turn_mode").Value);
			g.TurnNumber = int.Parse(gx.Element("turn").Value);
			g.TurnStartDate = TimeUtility.UnixTimeToDateTime(gx.Element("turn_start_date").Value);
			g.TurnDueDate = TimeUtility.UnixTimeToDateTime(gx.Element("next_turn_date").Value);
			g.Status = PlayerGame.ParseStatus(gx.Element("plr_status").Value);
			g.PlayerNumber = int.Parse(gx.Element("number").Value);
			g.ShipsetCode = gx.Element("shipset_code").Value;
			return g;
		}

		public bool Download(string fullurl, string downloadfilename)
		{
			Log.Write($"Attempting to download from {fullurl} and save as {downloadfilename}.");
			FileStream fileStream = File.Create(downloadfilename);

			bool result = false;

			try
			{
				HttpWebRequest request = (HttpWebRequest)WebRequest.Create(fullurl);
				request.CookieContainer = cookies;
				request.Method = WebRequestMethods.Http.Get;

				HttpWebResponse response;
				try
				{
					response = (HttpWebResponse)request.GetResponse();
				}
				catch (WebException)
				{
					request = RetryRequestWithCachedIP(request);
					response = (HttpWebResponse)request.GetResponse();
				}

				Stream responseStream = response.GetResponseStream();

				Log.Write($"Connection status to {response.ResponseUri} is {(int)response.StatusCode} {response.StatusCode}: {response.StatusDescription}");

				int buffersize = 1024;
				byte[] buffer = new byte[buffersize];
				int bytesRead = 0;
				WebHeaderCollection headders = response.Headers;

				while ((bytesRead = responseStream.Read(buffer, 0, buffersize)) != 0)
				{
					fileStream.Write(buffer, 0, bytesRead);
				}
				result = true;
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

			return result;
		}


		public bool Upload(string url, string uploadFormParam, string filepath, HttpStatusCode expectedStatus = HttpStatusCode.OK)
		{
			// adapted from and many thanks to: http://www.briangrinstead.com/blog/multipart-form-post-in-c
			Log.Write($"Attempting to upload {filepath} to {url} as form field {uploadFormParam}.");

			string filename = Path.GetFileName(filepath);
			string fileformat = uploadFormParam;
			var maxfilesize = MaxFileSize == 0 ? 64 * 1024 * 1024 : MaxFileSize; // default 64 MB if not already known
			string content_type = "application/octet-stream";

			bool result = false;

			try
			{
				// Read file data
				FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read);
				byte[] data = new byte[fs.Length];
				fs.Read(data, 0, data.Length);
				fs.Close();

				// Generate post objects
				Dictionary<string, object> postParameters = new Dictionary<string, object>();
				postParameters.Add("MAX_FILE_SIZE", maxfilesize);
				postParameters.Add("fileformat", fileformat);
				postParameters.Add(uploadFormParam, new FileParameter(data, filename, content_type));

				// Create request and receive response
				string postURL = url;
				HttpWebResponse response = FormUpload.MultipartFormDataPost(cookies, postURL, userAgent, postParameters);

				Log.Write($"Connection status to {response.ResponseUri} is {(int)response.StatusCode} {response.StatusCode}: {response.StatusDescription}");

				// Process response
				StreamReader responseReader = new StreamReader(response.GetResponseStream());
				if (response.StatusCode != expectedStatus)
				{
					if (response.StatusCode == HttpStatusCode.OK)
					{
						Log.Write($"Warning while uploading {filename}: Expected http response {(int)expectedStatus} {new HttpResponseMessage(expectedStatus).ReasonPhrase} but received 200 {response.StatusDescription}\n");
					}
					else
					{
						throw new WebException($"Could not upload {filename} to PBW, response {(int)response.StatusCode} {response.StatusDescription}. Try uploading it manually to see if there is an error.");
					}
				}
				response.Close();
				result = true;
			}
			catch (Exception ex)
			{
				Log.Write($"Error while uploading {filename}:\n");
				Log.Write(ex.ToString());
				throw;
			}

			return result;
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
				request.Timeout = 10 * 60 * 1000; // give it 10 minutes since the file might be big; TODO - user configurable or dynamic timeouts based on connection speed
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
				Stream requestStream;
				try
				{
					requestStream = request.GetRequestStream();
				}
				catch (WebException)
				{
					request = RetryRequestWithCachedIP(request);
					requestStream = request.GetRequestStream();
				}

				using (requestStream)
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
		}

		public static TurnMode ParseTurnMode(string s)
		{
			if (s == "manual")
				return TurnMode.Manual;
			else if (s == "alpu")
				return TurnMode.AfterLastPlayerUpload;
			else if (s == "timed")
				return TurnMode.Timed;
			else if (s == "auto")
				return TurnMode.FullyAutomatic;
			else
				throw new ArgumentException("Invalid turn mode: " + s, nameof(s));
		}

		public bool PlaceHold(HostGame g, string reason)
		{
			try
			{
				var url = $"http://pbw.spaceempires.net/games/{g.Code}/hold-turn";
				Log.Write($"Attempting to place hold on auto processing for {g}.");
				var fields = new Dictionary<string, string>
				{
					{"hold_message", reason},
				};
				HttpUtility.SubmitForm(url, fields, cookies, "placing hold on " + g);
				Log.Write($"Placing hold succeeded for {g}.");
				return true;
			}
			catch (Exception ex)
			{
				Log.Write($"Placing hold failed for {g}.");
				return false;
			}
		}

		public bool ClearHold(HostGame g, string reason)
		{
			// reason parameter is ignored by PBW
			try
			{
				var url = $"http://pbw.spaceempires.net/games/{g.Code}/clear-hold";
				Log.Write($"Attempting to clear hold on auto processing for {g}.");
				var fields = new Dictionary<string, string>();
				HttpUtility.SubmitForm(url, fields, cookies, "clearing hold on " + g);
				Log.Write($"Placing hold succeeded for {g}.");
				return true;
			}
			catch (Exception ex)
			{
				Log.Write($"Placing hold failed for {g}.");
				return false;
			}
		}
	}
}
