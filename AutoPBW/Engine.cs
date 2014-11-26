using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPBW
{
	/// <summary>
	/// A game engine.
	/// </summary>
	public class Engine
	{
		public Engine(string code)
		{
			Code = code;
			IsUnknown = true;
		}

		/// <summary>
		/// A unique code used to refer to this engine.
		/// </summary>
		public string Code {get; set; }

		public override string ToString()
		{
			return Code;
		}

		/// <summary>
		/// The full path to the host instance of the game engine executable.
		/// </summary>
		public string HostExecutable { get; set; }

		/// <summary>
		/// The full path to the player instance of the game engine executable.
		/// </summary>
		public string PlayerExecutable { get; set; }

		/// <summary>
		/// Comma separated list of filename filters for the host turn upload.
		/// Can use replacement strings from HostArguments.
		/// </summary>
		public string HostTurnUploadFilter { get; set; }

		/// <summary>
		/// Comma separated list of filename filters for the player turn upload.
		/// Can use replacement strings from PlayerArguments.
		/// </summary>
		public string PlayerTurnUploadFilter { get; set; }

		/// <summary>
		/// Is this an unknown engine?
		/// </summary>
		public bool IsUnknown { get; set; }

		/// <summary>
		/// Command line arguments for the host.
		/// Can use the following replacement strings:
		/// {EnginePath}: the path to the game engine
		/// {ModPath}: the path to the mod (relative to the engine path)
		/// {SavePath}: the save path of the mod (relative to the engine path)
		/// {Password}: the host password for the game
		/// {GameCode}: the unique game code for each game
		/// {TurnNumber}: the turn number for the game
		/// </summary>
		public string HostArguments { get; set; }

		/// <summary>
		/// Command line arguments for the player.
		/// Can use the following replacement strings:
		/// {EnginePath}: the path to the game engine
		/// {ModPath}: the path to the mod (relative to the engine path)
		/// {SavePath}: the save path of the mod (relative to the engine path)
		/// {Password}: the player password for the game
		/// {GameCode}: the unique game code for each game
		/// {TurnNumber}: the turn number for the game
		/// {PlayerNumber}: the player number for our empire
		/// </summary>
		public string PlayerArguments { get; set; }

		public static Engine Find(string code)
		{
			if (code == null)
				return null;
			var old = Config.Instance.Engines.SingleOrDefault(x => x.Code == code);
			Engine nu;
			if (old == null || old.IsUnknown)
			{
				// load from default if present
				var d = Config.Default.Engines.SingleOrDefault(x => x.Code == code);
				if (d != null)
					nu = d;
				else
					nu = new Engine(code); // let the user know what the code is so he can find the engine
			}
			else
				nu = old;
			if (nu != old)
			{
				if (old != null)
					Config.Instance.Engines.Remove(old);
				Config.Instance.Engines.Add(nu);
			}
			return nu;
		}
	}
}
