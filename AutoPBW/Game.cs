using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SevenZip;

namespace AutoPBW
{
	/// <summary>
	/// A game that is being played or hosted on PBW.
	/// </summary>
	public abstract class Game
	{
		/// <summary>
		/// The unique short name of the game (as used in its URL).
		/// </summary>
		public string Code { get; set; }

		public override string ToString()
		{
			return Code;
		}

		/// <summary>
		/// The player or host password for this game.
		/// </summary>
		public string Password { get; set; }

		/// <summary>
		/// The mod used for this game.
		/// </summary>
		public Mod Mod { get; set; }

		/// <summary>
		/// The engine used for this game.
		/// </summary>
		public Engine Engine { get { return Mod.Engine; } }

		/// <summary>
		/// Turn processing mode for this game.
		/// </summary>
		public TurnMode TurnMode { get; set; }

		/// <summary>
		/// The current turn number.
		/// </summary>
		public int TurnNumber { get; set; }

		/// <summary>
		/// When the current turn started.
		/// Or null if the game hasn't started yet.
		/// </summary>
		public DateTime? TurnStartDate { get; set; }

		/// <summary>
		/// When the next turn is due.
		/// Or null if the game hasn't started yet.
		/// </summary>
		public DateTime? TurnDueDate { get; set; }

		/// <summary>
		/// Time left to play the turn.
		/// </summary>
		public TimeSpan? TimeLeft
		{
			get
			{
				if (TurnDueDate == null)
					return TimeSpan.MaxValue;
				if (TurnDueDate < DateTime.Now)
					return TimeSpan.Zero;
				return TurnDueDate - DateTime.Now;
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
				throw new ArgumentException("Invalid turn mode: " + s, "s");
		}

		protected void DownloadExtractAndDelete(string url, string path)
		{
			// generate a temp file name
			var tempfile = MakeTempFile("7z");

			// download the archive
			PBW.Download(url, tempfile);

			// extract the archive
			PBW.Log.Write("Extracting {0} into {1}".F(tempfile, path));
			var x = new SevenZipExtractor(tempfile);
			x.ExtractArchive(path);

			// log file list
			PBW.Log.Write("List of files extracted:");
			foreach (var f in x.ArchiveFileNames.Select(f => Path.Combine(path, f)))
			{
				// lowercase it, requires 2 steps since Windows won't let you rename a file changing only the case
				// disabled this feature since it was causing issues with combat/movement files not being downloaded from PBW and it was only used as a workaround for a game with an uppercase name
				//var temp = MakeTempFile(Path.GetExtension(f));
				//File.Move(f, temp);
				//var f2 = f.ToLowerInvariant();
				var f2 = f;
				//File.Move(temp, f2);
				PBW.Log.Write("\t" + Path.GetFileName(f2));
			}

			// delete the archive
			PBW.Log.Write("Deleting {0}".F(tempfile));
			File.Delete(tempfile);
		}

		protected void ArchiveUploadAndDeleteArchive(IEnumerable<string> files, string url, string uploadFormParam, HttpStatusCode expectedStatus = HttpStatusCode.OK)
		{
			// generate a temp file name
			var tempfile = MakeTempFile("7z");
			PBW.Log.Write("Archiving files into {0}:".F(tempfile));

			// archive the files
			var c = new SevenZipCompressor();
			var files2 = files.ToArray();
			foreach (var f in files2)
				PBW.Log.Write("\t" + f);
			c.CompressFiles(tempfile, files2);

			// upload the archive
			PBW.Upload(tempfile, url, uploadFormParam, expectedStatus);

			// delete the archive
			File.Delete(tempfile);
		}

		protected string MakeTempFile(string extension)
		{
			if (!Directory.Exists("temp"))
				Directory.CreateDirectory("temp");
			return Path.Combine("temp", DateTime.Now.Ticks + "." + extension);
		}

		protected IEnumerable<string> GetFiles(string path, string filters)
		{
			var files = new HashSet<string>();
			foreach (var filter in filters.Split(','))
			{
				foreach (var file in Directory.EnumerateFiles(path, filter.Trim()))
					if (files.Add(file))
						yield return file;
			}
		}
	}

	/// <summary>
	/// A game that is being hosted on PBW.
	/// </summary>
	public class HostGame : Game
	{
		/// <summary>
		/// The game's status for hosting purposes.
		/// </summary>
		public HostStatus Status { get; set; }

		/// <summary>
		/// Downloads empires for this game.
		/// </summary>
		public void DownloadEmpires()
		{
			var url = "http://pbw.spaceempires.net/games/{0}/host-empire/download".F(Code);
			var path = Path.Combine(Path.GetDirectoryName(Engine.HostExecutable.Trim('"')), Mod.EmpirePath);
			PBW.Log.Write($"Downloading empires for {this} and saving to {path}.");
			DownloadExtractAndDelete(url, path);
		}

		/// <summary>
		/// Downloads turns for this game.
		/// </summary>
		public void DownloadTurns()
		{
			var url = "http://pbw.spaceempires.net/games/{0}/host-turn/download".F(Code);
			var path = Path.Combine(Path.GetDirectoryName(Engine.HostExecutable.Trim('"')), Mod.SavePath);
			PBW.Log.Write($"Downloading player turns for {this} and saving to {path}.");
			DownloadExtractAndDelete(url, path);
		}

		/// <summary>
		/// The game that's currently processing/uploading.
		/// Don't attempt to process any more games if one is already processing, and don't attempt to upload the same game twice in a row!
		/// </summary>
		public static HostGame ProcessingGame { get; set; }

		/// <summary>
		/// Prepares to process the turn for this game.
		/// You will need to actually start the process yourself.
		/// This is so you can attach an event handler to the process exit event.
		/// Sets the "processing game" to this game, on the assumption that the process will be started immediately.
		/// Only one game can be processed at a time.
		/// </summary>
		public ProcessStartInfo ProcessTurnPrepare()
		{
			if (ProcessingGame != null)
				throw new InvalidOperationException("Cannot begin processing " + this + " because " + ProcessingGame + " is already being processed.");

			ProcessingGame = this;

			var cmd = GenerateArgumentsOrFilter(Engine.HostExecutable, false);
			var args = GenerateArgumentsOrFilter(Engine.HostArguments, false);
			PBW.Log.Write($"Executing command to process {this}: {cmd} {args}");
			return new ProcessStartInfo(cmd, args);
		}

		/// <summary>
		/// Uploads the next turn for this game.
		/// Sets the "processing game code" to null.
		/// </summary>
		public void UploadTurn()
		{
			// get list of files
			var path = Path.Combine(Path.GetDirectoryName(Engine.HostExecutable.Trim('"')), Mod.SavePath);
			var files = GetFiles(path, GenerateArgumentsOrFilter(Engine.HostTurnUploadFilter, true));

			// send to PBW
			var url = "http://pbw.spaceempires.net/games/{0}/host-turn/upload".F(Code);
			PBW.Log.Write($"Uploading next turn for {this}.");
			ArchiveUploadAndDeleteArchive(files, url, "turn_file", HttpStatusCode.Redirect); // for some reason PBW gives a 302 on host turn upload

			ProcessingGame = null;
		}

		// TODO - replace turn

		// TODO - rollback turn

		// TODO - upload host PLR

		// TODO - upload GSU

		public void PlaceHold(string reason)
		{
			var url = "http://pbw.spaceempires.net/games/{0}/hold-turn".F(Code);
			PBW.Log.Write("Attempting to place hold on auto processing for {0}.".F(Code));
			var fields = new Dictionary<string, string>
			{
				{"hold_message", reason},
			};
			PBW.SubmitForm(url, fields, "placing hold on " + Code);
		}

		public void ClearHold(string reason)
		{
			var url = "http://pbw.spaceempires.net/games/{0}/clear-hold".F(Code);
			PBW.Log.Write("Attempting to clear hold on auto processing for {0}.".F(Code));
			var fields = new Dictionary<string, string>();
			PBW.SubmitForm(url, fields, "clearing hold on " + Code);
		}

		private string GenerateArgumentsOrFilter(string basestring, bool nextTurn)
		{
			return basestring
				.Replace("{Executable}", Engine.HostExecutable)
				.Replace("{EnginePath}", Path.GetDirectoryName(Engine.HostExecutable.Trim('"')))
				.Replace("{ModPath}", Mod.Path)
				.Replace("{SavePath}", Mod.SavePath)
				.Replace("{Password}", Password)
				.Replace("{GameCode}", Code)
				.Replace("{TurnNumber}", (nextTurn ? TurnNumber + 1 : TurnNumber).ToString());
		}
	}

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
		public string DisplayStatus {
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
			PBW.Log.Write($"Launching {Engine} to create empire for {this}: {cmd}.");
			Launcher.Launch(cmd);
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
			var url = "http://pbw.spaceempires.net/games/{0}/player-turn/download".F(Code);
			var path = GetSavePath();
			PBW.Log.Write($"Downloading turn for {this} to {path}.");
			DownloadExtractAndDelete(url, path);
			HasDownloaded = true;
		}

		/// <summary>
		/// Uploads empire file for this game.
		/// </summary>
		public void UploadEmpire(string empfile)
		{
			var url = "http://pbw.spaceempires.net/games/{0}/player-empire/upload".F(Code);
			var path = Path.Combine(Engine.GetPlayerExecutableDirectory(), Mod.EmpirePath);
			PBW.Log.Write($"Uploading empire {path} for {this}.");
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
			var url = "http://pbw.spaceempires.net/games/{0}/player-turn/upload".F(Code);
			if (files.Count() != 1)
				throw new InvalidOperationException("Can only upload one PLR file at a time. " + files.Count() + " files were submitted.");

			PBW.Log.Write($"Uploading player commands {path} for {this}.");
			PBW.Upload(files.Single(), url, "plr_file");
			Status = PlayerStatus.Uploaded;
		}

		public void PlayTurn()
		{
			var cmd = GenerateArgumentsOrFilter(Engine.PlayerExecutable);
			var args = GenerateArgumentsOrFilter(Engine.PlayerArguments);
			PBW.Log.Write($"Launching {Engine} to play turn for {this}: {cmd} {args}");
			Launcher.Launch(cmd, args);
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
	/// Status of a game for hosting.
	/// </summary>
	public enum HostStatus
	{
		/// <summary>
		/// Game is not being hosted by this user.
		/// Games that are in this status are not currently returned by PBW.
		/// </summary>
		None,

		/// <summary>
		/// Empires have been uploaded; ready for first turn or host setup.
		/// </summary>
		EmpiresReady,

		/// <summary>
		/// Empires and host setup have been uploaded; ready for first turn.
		/// </summary>
		HostReady,

		/// <summary>
		/// Game is in progress and awaiting player commands.
		/// Games that are in this status are not currently returned by PBW.
		/// </summary>
		InProgress,

		/// <summary>
		/// Game is in progress and turn is ready to process, either due to all commands being uploaded or the turn timer expiring.
		/// </summary>
		PlayersReady,
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
