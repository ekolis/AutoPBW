using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace AutoPBW
{
	/// <summary>
	/// A game mod, or the stock game.
	/// </summary>
	public class Mod
	{
		public Mod(string code, string engineCode)
		{
			if (Config.Instance != null && Config.Instance.Engines.Any(e => e.Code == code))
				throw new ArgumentException("Mod " + code + " already exists.");
			Code = code;
			Engine = Engine.Find(engineCode);
			IsUnknown = true;
		}

		/// <summary>
		/// A unique code name for this mod. Used in PBW URLs.
		/// </summary>
		public string Code { get; set; }

		public override string ToString()
		{
			return Code;
		}

		/// <summary>
		/// The game engine that this mod uses.
		/// </summary>
		public Engine Engine { get; set; }

		/// <summary>
		/// The path to the mod's root directory, relative to the engine's root directory.
		/// </summary>
		public string Path { get; set; }

		/// <summary>
		/// The path to the mod's savegame directory, relative to the engine's root directory.
		/// </summary>
		public string SavePath { get; set; }

		/// <summary>
		/// The path to the mod's empire setup directory, relative to the engine's root directory.
		/// </summary>
		public string EmpirePath { get; set; }

		/// <summary>
		/// Is this an unknown mod?
		/// </summary>
		public bool IsUnknown { get; set; }

		public static Mod Find(string code, string defaultEngineCode = null)
		{
			if (!Config.Instance.Mods.Any(m => m.Code == code))
				Config.Instance.Mods.Add(new Mod(code, defaultEngineCode)); // let the user know what the mod code is so he can find the mod
			return Config.Instance.Mods.Single(m => m.Code == code);
		}
	}
}
