using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AutoPBW
{
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
			var url = $"http://pbw.spaceempires.net/games/{Code}/host-empire/download";
			var path = Path.Combine(Path.GetDirectoryName(Engine.HostExecutable.Trim('"')), Mod.EmpirePath);
			Log.Write($"Downloading empires for {this} and saving to {path}.");
			DownloadExtractAndDelete(url, path);
		}

		/// <summary>
		/// Downloads turns for this game.
		/// </summary>
		public void DownloadTurns()
		{
			var url = $"http://pbw.spaceempires.net/games/{Code}/host-turn/download";
			var path = Path.Combine(Path.GetDirectoryName(Engine.HostExecutable.Trim('"')), Mod.SavePath);
			Log.Write($"Downloading player turns for {this} and saving to {path}.");
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
			Log.Write($"Executing command to process {this}: {cmd} {args}");
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
			var url = $"http://pbw.spaceempires.net/games/{Code}/host-turn/upload";
			Log.Write($"Uploading next turn for {this}.");
			ArchiveUploadAndDeleteArchive(files, url, "turn_file", HttpStatusCode.Redirect); // for some reason PBW gives a 302 on host turn upload

			ProcessingGame = null;
		}

		// TODO - replace turn

		// TODO - rollback turn

		// TODO - upload host PLR

		// TODO - upload GSU

		public void PlaceHold(string? reason = null)
		{
			Service.PlaceHold(this, reason);
		}

		public void ClearHold(string? reason = null)
		{
			Service.ClearHold(this, reason);
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
}
