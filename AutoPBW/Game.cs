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
	/// A game that is being played or hosted on a hosting service.
	/// </summary>
	public abstract class Game
	{
		/// <summary>
		/// The unique short name of the game (as used in its URL).
		/// </summary>
		public string Code { get; set; }

		/// <summary>
		/// The full name of the game.
		/// </summary>
		public string Name { get; set; }

		public override string ToString()
			=> Name.IsBlank() ? Code : $"{Code}: {Name}";

		/// <summary>
		/// The multiplayer service on which this game is hosted.
		/// </summary>
		public IMultiplayerService Service { get; set; }

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
		public Engine Engine
			=> Mod.Engine;

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

		protected void DownloadExtractAndDelete(string url, string path)
		{
			// generate a temp file name
			var tempfile = MakeTempFile("7z");

			// download the archive
			Service.Download(url, tempfile);

			// extract the archive
			Log.Write($"Extracting {tempfile} into {path}");
			var x = new SevenZipExtractor(tempfile);
			x.ExtractArchive(path);

			// log file list
			Log.Write("List of files extracted:");
			foreach (var f in x.ArchiveFileNames.Select(f => Path.Combine(path, f)))
			{
				// lowercase it, requires 2 steps since Windows won't let you rename a file changing only the case
				// disabled this feature since it was causing issues with combat/movement files not being downloaded from PBW and it was only used as a workaround for a game with an uppercase name
				//var temp = MakeTempFile(Path.GetExtension(f));
				//File.Move(f, temp);
				//var f2 = f.ToLowerInvariant();
				var f2 = f;
				//File.Move(temp, f2);
				Log.Write("\t" + Path.GetFileName(f2));
			}

			// delete the archive
			Log.Write($"Deleting {tempfile}");
			File.Delete(tempfile);
		}

		protected void ArchiveUploadAndDeleteArchive(IEnumerable<string> files, string url, string uploadFormParam, HttpStatusCode expectedStatus = HttpStatusCode.OK)
		{
			// generate a temp file name
			var tempfile = MakeTempFile("7z");
			Log.Write($"Archiving files into {tempfile}:");

			// archive the files
			var c = new SevenZipCompressor();
			var files2 = files.ToArray();
			foreach (var f in files2)
				Log.Write("\t" + f);
			c.CompressFiles(tempfile, files2);

			// upload the archive
			Service.Upload(tempfile, url, uploadFormParam, expectedStatus);

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
}
