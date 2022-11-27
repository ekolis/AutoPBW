using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SevenZip;

namespace AutoPBW
{
	/// <summary>
	/// Configuration for AutoPBW
	/// </summary>
	public class Config
	{
		static Config()
		{
			Instance = new();

			JsonOptions = new()
			{
				WriteIndented = true,
				ReferenceHandler = ReferenceHandler.Preserve,
			};

			if (IntPtr.Size == 8) // 64-bit
			{
				SevenZipBase.SetLibraryPath("7z64.dll");
			}
			else
			{
				SevenZipBase.SetLibraryPath("7z.dll");
			}
		}

		public Config()
		{
			Engines = new ObservableCollection<Engine>();
			Mods = new ObservableCollection<Mod>();

			// set up some defaults
			var se4 = new Engine("SE4");
			se4.HostExecutable = @"C:\Autohost\SE4";
			se4.PlayerExecutable = @"C:\Games\SE4";
		}

		public static Config Instance { get; private set; }

		internal static Config Default { get; private set; }

		private const string filename = "AutoPBW.config.json";

		private const string defaultFilename = "AutoPBW.config.default.json";

		private static JsonSerializerOptions JsonOptions;

		public static void Load()
		{
			try
			{
				Default = JsonSerializer.Deserialize<Config>(File.ReadAllText(defaultFilename), JsonOptions)!;
			}
			catch (Exception ex)
			{
				PBW.Log.Write("Could not load default config from " + filename + ".");
				PBW.Log.Write("Error that occurred: " + ex.Message);
				Default = new Config();
			}

			try
			{
				Instance = JsonSerializer.Deserialize<Config>(File.ReadAllText(filename), JsonOptions)!;
			}
			catch (Exception ex)
			{
				PBW.Log.Write("Could not load config from " + filename + "; reverting to default settings.");
				PBW.Log.Write("Error that occurred: " + ex.Message);
				Instance = new Config();
				Instance.Username = Default.Username;
				Instance.Password = Default.Password;
				foreach (var e in Default.Engines)
					Instance.Engines.Add(e);
				foreach (var m in Default.Mods)
					Instance.Mods.Add(m);
			}
		}

		public static void Save()
		{
			File.WriteAllText(filename, JsonSerializer.Serialize(Instance, JsonOptions));
		}

		/// <summary>
		/// User's PBW username.
		/// </summary>
		public string Username { get; set; }

		/// <summary>
		/// User's PBW password.
		/// </summary>
		public string Password { get; set; }

		/// <summary>
		/// Should this instance of AutoPBW host games?
		/// </summary>
		public bool EnableHosting { get; set; }

		/// <summary>
		/// Should games in which we are player zero (host player) be hidden?
		/// </summary>
		public bool HidePlayerZero { get; set; }

		/// <summary>
		/// Should new player turns be automatically downloaded?
		/// </summary>
		public bool AutoDownload { get; set; }

		/// <summary>
		/// Should we watch for and automatically upload player turn files?
		/// </summary>
		public bool EnableAutoUpload { get; set; }

		/// <summary>
		/// Should we silently ignore bad SSL certificates on the PBW site, or prompt the user about them?
		/// </summary>
		public bool IgnoreBadCertificates { get; set; }

		/// <summary>
		/// The polling interval to check with PBW, in seconds.
		/// </summary>
		public int PollingInterval { get; set; } = 120;

		/// <summary>
		/// Known game engines.
		/// </summary>
		public ObservableCollection<Engine> Engines { get; set; }

		/// <summary>
		/// Known mods.
		/// </summary>
		public ObservableCollection<Mod> Mods { get; set; }
	}
}
