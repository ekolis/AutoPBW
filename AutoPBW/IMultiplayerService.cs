using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AutoPBW
{
	public interface IMultiplayerService
	{
		/// <summary>
		/// The name of the service type.
		/// </summary>
		string ServiceTypeName { get; }

		/// <summary>
		/// The name of the service instance.
		/// </summary>
		string Name { get; set; }

		/// <summary>
		/// Logs in to the multiplayer service.
		/// </summary>
		/// <param name="username">The username to log in with.</param>
		/// <param name="password">The password to log in with.</param>
		/// <returns>true if successful, otherwise false</returns>
		bool Login(string username, string password);

		/// <summary>
		/// Is the user currently logged in?
		/// </summary>
		bool IsLoggedIn { get; }

		/// <summary>
		/// Gets games that we are hosting.
		/// </summary>
		/// <returns></returns>
		IEnumerable<HostGame> GetHostGames();

		/// <summary>
		/// Gets games that we are playing.
		/// </summary>
		/// <returns></returns>
		IEnumerable<PlayerGame> GetPlayerGames();

		/// <summary>
		/// Downloads a file from the service.
		/// </summary>
		/// <param name="url">The URL to download from..</param>
		/// <param name="filepath">The full path of the file to save.</param>
		/// <returns>true if successful, otherwise false</returns>
		bool Download(string url, string filepath);

		/// <summary>
		/// Uploads a file to the service.
		/// </summary>
		/// <param name="url">The URL to upload to.</param>
		/// <param name="uploadFormParam">The form parameter which will contain the uploaded file.</param>
		/// <param name="filepath">The full path of the file to upload.</param>
		/// <param name="expectedStatus">The expected HTTP status code.</param>
		/// <returns>true if successful, otherwise false</returns>
		bool Upload(string url, string uploadFormParam, string filepath, HttpStatusCode expectedStatus = HttpStatusCode.OK);

		/// <summary>
		/// Uploads the next turn from the host so that players can play.
		/// </summary>
		/// <param name="game">The game whose turn we are uploading.</param>
		/// <param name="files">The files to upload.</param>
		/// <returns>true if successful, otherwise false</returns>
		bool UploadHostTurn(HostGame game, IEnumerable<string> files);

		/// <summary>
		/// Places a hold on automatic processing of a game.
		/// </summary>
		/// <param name="g">The game.</param>
		/// <param name="reason">The reason for placing a hold.</param>
		/// <returns>true if successful, otherwise false</returns>
		bool PlaceHold(HostGame g, string? reason = null);

		/// <summary>
		/// Clears a hold on automatic processing of a game.
		/// </summary>
		/// <param name="g">The game.</param>
		/// <param name="reason">The reason for clearing the hold.</param>
		/// <returns>true if successful, otherwise false</returns>
		bool ClearHold(HostGame g, string? reason = null);
	}
}