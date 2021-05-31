using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPBW
{


	/// <summary>
	/// A game that is being played on PBW.
	/// </summary>
	public class PlayerGame : Game
	{
		/// <summary>
		/// The game's status for player purposes.
		/// </summary>
		public PlayerStatus Status { get; set; }

		/// <summary>
		/// Which player are we?
		/// </summary>
		public int PlayerNumber { get; set; }

		/// <summary>
		/// Which shipset are we using?
		/// </summary>
		public string ShipsetCode { get; set; }

		/// <summary>
		/// Have we downloaded this game turn yet?
		/// </summary>
		public bool HasDownloaded { get; set; }

		/// <summary>
		/// The string to display for the status in the UI
		/// </summary>
		public string DisplayStatus
		{
			get
			{
				// Display "Waiting [D]" for downloaded turns
				if (Status == PlayerStatus.Waiting && HasDownloaded)
					return Status.ToString() + " [D]";
				return Status.ToString();
			}
		}

		public static PlayerStatus ParseStatus(string s)
		{
			if (s == "waiting")
				return PlayerStatus.Waiting;
			else if (s == "uploaded")
				return PlayerStatus.Uploaded;
			else
				throw new ArgumentException("Invalid player status: " + s, "s");
		}

		public void CreateEmpire()
		{
			// just run the game so the player can create it in-game
			var cmd = GenerateArgumentsOrFilter(Engine.PlayerExecutable);
			Log.Write($"Launching {Engine} to create empire for {this}: {cmd}.");
			Process.Start(cmd);
		}

		public string GetSavePath()
		{
			return Path.Combine(Engine.GetPlayerExecutableDirectory(), Mod.SavePath);
		}

		/// <summary>
		/// Downloads the turn for this game.
		/// </summary>
		public void DownloadTurn()
		{
			var url = $"http://pbw.spaceempires.net/games/{Code}/player-turn/download";
			var path = GetSavePath();
			Log.Write($"Downloading turn for {this} to {path}.");
			DownloadExtractAndDelete(url, path);
			HasDownloaded = true;
		}

		/// <summary>
		/// Uploads empire file for this game.
		/// </summary>
		public void UploadEmpire(string empfile)
		{
			var url = $"http://pbw.spaceempires.net/games/{Code}/player-empire/upload";
			var path = Path.Combine(Engine.GetPlayerExecutableDirectory(), Mod.EmpirePath);
			Log.Write($"Uploading empire {path} for {this}.");
			ArchiveUploadAndDeleteArchive(new string[] { empfile }, url, "emp_file");
		}

		/// <summary>
		/// Checks if we have a turn file pending upload
		/// </summary>
		public bool IsReadyToUploadTurn()
		{
			if (Status == PlayerStatus.Waiting)
			{
				// get list of files
				var path = GetSavePath();
				var files = GetFiles(path, GenerateArgumentsOrFilter(Engine.PlayerTurnUploadFilter));

				var turnfile = files.SingleOrDefault();
				if (turnfile != null && File.GetLastWriteTime(turnfile) > TurnStartDate)
					return true;
			}
			return false;
		}

		/// <summary>
		/// Uploads player turn (commands) for this game.
		/// </summary>
		public void UploadTurn()
		{
			// get list of files
			var path = GetSavePath();
			var files = GetFiles(path, GenerateArgumentsOrFilter(Engine.PlayerTurnUploadFilter));

			// send to PBW
			var url = $"http://pbw.spaceempires.net/games/{Code}/player-turn/upload";
			if (files.Count() != 1)
				throw new InvalidOperationException("Can only upload one PLR file at a time. " + files.Count() + " files were submitted.");

			Log.Write($"Uploading player commands {path} for {this}.");
			Service.Upload(files.Single(), url, "plr_file");
			Status = PlayerStatus.Uploaded;
		}

		public void PlayTurn()
		{
			var cmd = GenerateArgumentsOrFilter(Engine.PlayerExecutable);
			var args = GenerateArgumentsOrFilter(Engine.PlayerArguments);
			Log.Write($"Launching {Engine} to play turn for {this}: {cmd} {args}");
			Process.Start(cmd, args);
		}

		private string GenerateArgumentsOrFilter(string basestring)
		{
			return basestring
				.Replace("{Executable}", Engine.PlayerExecutable)
				.Replace("{EnginePath}", Engine.GetPlayerExecutableDirectory())
				.Replace("{ModPath}", Mod.Path)
				.Replace("{SavePath}", Mod.SavePath)
				.Replace("{Password}", Password)
				.Replace("{GameCode}", Code)
				.Replace("{TurnNumber}", TurnNumber.ToString())
				.Replace("{PlayerNumber}", PlayerNumber.ToString())
				.Replace("{PlayerNumber2}", PlayerNumber.ToString("00"))
				.Replace("{PlayerNumber3}", PlayerNumber.ToString("000"))
				.Replace("{PlayerNumber4}", PlayerNumber.ToString("0000"));
		}
	}

	/// <summary>
	/// Turn processing modes.
	/// </summary>
	[Flags]
	public enum TurnMode
	{
		/// <summary>
		/// Turn is not processed automatically.
		/// </summary>
		Manual = 0,

		/// <summary>
		/// Turn is processed after all players have uploaded commands.
		/// </summary>
		AfterLastPlayerUpload = 1,

		/// <summary>
		/// Turn is processed after a time limit expires.
		/// </summary>
		Timed = 2,

		/// <summary>
		/// Turn is processed after either all players have uploaded commands, or a time limit expires.
		/// </summary>
		FullyAutomatic = AfterLastPlayerUpload | Timed,
	}

	/// <summary>
	/// Status of a game for players.
	/// </summary>
	public enum PlayerStatus
	{
		/// <summary>
		/// Game is not being played by this user.
		/// Games that are in this status are not currently returned by PBW.
		/// </summary>
		None,

		/// <summary>
		/// Waiting for EMP or PLR file from player.
		/// </summary>
		Waiting,

		/// <summary>
		/// EMP or PLR file has been uploaded.
		/// </summary>
		Uploaded,
	}
}
