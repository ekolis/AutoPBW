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
		/// <summary>
		/// For deserialization.
		/// </summary>
		public Mod()
		{
			IsUnknown = true;
		}

		public Mod(string code, string engineCode)
			: this()
		{
			Code = code;
			Engine = Engine.Find(engineCode);
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
			if (code == null)
				return null;
			var old = Config.Instance.Mods.SingleOrDefault(x => x.Code == code);
			Mod nu;
			if (old == null || old.IsUnknown)
			{
				// load from default if present
				var d = Config.Default.Mods.SingleOrDefault(x => x.Code == code);
				if (d != null)
					nu = d;
				else
					nu = new Mod(code, defaultEngineCode); // let the user know what the code is so he can find the mod
			}
			else
				nu = old;
			if (nu != old)
			{
				if (old != null)
					Config.Instance.Mods.Remove(old);
				Config.Instance.Mods.Add(nu);
			}
			return nu;
		}
	}
}
