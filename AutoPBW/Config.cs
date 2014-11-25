using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
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
			Instance = new Config();

			JsonSettings = new JsonSerializerSettings();
			JsonSettings.Formatting = Formatting.Indented;
			JsonSettings.PreserveReferencesHandling = PreserveReferencesHandling.All;

			if (IntPtr.Size == 8) // 64-bit
			{
				SevenZipCompressor.SetLibraryPath("7z64.dll");
				SevenZipExtractor.SetLibraryPath("7z64.dll");

			}
			else
			{
				SevenZipCompressor.SetLibraryPath("7z.dll");
				SevenZipExtractor.SetLibraryPath("7z.dll");
			}
		}

		private Config()
		{
			Engines = new ObservableCollection<Engine>();
			Mods = new ObservableCollection<Mod>();

			// set up some defaults
			var se4 = new Engine("SE4");
			se4.HostPath = @"C:\Autohost\SE4";
			se4.PlayerPath = @"C:\Games\SE4";
		}

		public static Config Instance { get; private set; }

		private const string filename = "AutoPBW.config.json";

		private static JsonSerializerSettings JsonSettings;

		public static void Load()
		{
			try
			{
				Instance = JsonConvert.DeserializeObject<Config>(File.ReadAllText(filename), JsonSettings);
			}
			catch (Exception ex)
			{
				PBW.Log.Write("Could not load config from " + filename + "; reverting to default settings.");
				PBW.Log.Write("Error that occurred: " + ex.Message);
				Instance = new Config();
			}
		}

		public static void Save()
		{
			File.WriteAllText(filename, JsonConvert.SerializeObject(Instance, JsonSettings));
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
		/// Known game engines.
		/// </summary>
		public ObservableCollection<Engine> Engines { get; private set; }

		/// <summary>
		/// Known mods.
		/// </summary>
		public ObservableCollection<Mod> Mods { get; private set; }
	}
}
