using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AutoPBW;
using AutoPBW.WPF.Properties;

namespace AutoPBW.WPF
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		private void window_Loaded(object sender, RoutedEventArgs e)
		{
			txtUsername.Text = Config.Instance.Username;
			txtPassword.Password = Config.Instance.Password;

			if (string.IsNullOrWhiteSpace(Config.Instance.Username) || string.IsNullOrWhiteSpace(Config.Instance.Password))
			{
				// let the user enter username/password
				tabSettings.Focus();
			}
			else
			{
				// load games list
				Login();
				RefreshData();
			}
		}

		private void RefreshData()
		{
			try
			{
				var playerGameViewSource = ((CollectionViewSource)(this.FindResource("playerGameViewSource")));
				playerGameViewSource.Source = PBW.GetPlayerGames();
				gridPlayerGames.GetBindingExpression(DataGrid.ItemsSourceProperty).UpdateTarget();
				var engineViewSource = ((CollectionViewSource)(this.FindResource("engineViewSource")));
				engineViewSource.Source = Config.Instance.Engines;
				lstEngines.GetBindingExpression(ListView.ItemsSourceProperty).UpdateTarget();
				ddlEngine.GetBindingExpression(ComboBox.ItemsSourceProperty).UpdateTarget();
				var modViewSource = ((CollectionViewSource)(this.FindResource("modViewSource")));
				modViewSource.Source = Config.Instance.Mods;
				lstMods.GetBindingExpression(ListView.ItemsSourceProperty).UpdateTarget();
			}
			catch (Exception ex)
			{
				MessageBox.Show("Unable to refresh games/engines/mods lists: " + ex.Message);
			}
		}

		private void Login()
		{
			try
			{
				PBW.Login(Config.Instance.Username, Config.Instance.Password);
			}
			catch (Exception ex)
			{
				MessageBox.Show("Unable to log in to PBW: " + ex.Message);
			}
		}

		private void btnSaveSettings_Click(object sender, RoutedEventArgs e)
		{
			Config.Instance.Username = txtUsername.Text;
			Config.Instance.Password = txtPassword.Password;
			Config.Save();
			Login();
			RefreshData();
			tabPlayerGames.Focus();
		}

		private void playerGameDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{

		}

		private T GetAncestor<T>(FrameworkElement source) where T : FrameworkElement
		{
			while (!(source is T || source == null))
				source = (FrameworkElement)VisualTreeHelper.GetParent(source);
			return (T)source;
		}

		private void lstEngines_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var engine = lstEngines.SelectedItem as Engine;
			gridEngineDetails.DataContext = engine;
		}

		private void btnSaveEngine_Click(object sender, RoutedEventArgs e)
		{
			var engine = lstEngines.SelectedItem as Engine;
			if (engine != null)
			{
				engine.Code = engineCodeTextBox.Text;
				engine.HostArguments = hostArgumentsTextBox.Text;
				engine.HostExecutable = hostExecutableTextBox.Text;
				engine.HostTurnUploadFilter = hostTurnUploadFilterTextBox.Text;
				engine.IsUnknown = false;
				engine.PlayerArguments = playerArgumentsTextBox.Text;
				engine.PlayerExecutable = playerExecutableTextBox.Text;
				engine.PlayerTurnUploadFilter = playerTurnUploadFilterTextBox.Text;
				Config.Save();
				RefreshData();
				lstEngines.SelectedItem = null;
			}
		}

		private void btnDeleteEngine_Click(object sender, RoutedEventArgs e)
		{
			var engine = lstEngines.SelectedItem as Engine;
			if (engine != null)
			{
				if (Config.Instance.Mods.Any(m => m.Engine == engine))
					MessageBox.Show("You cannot delete an engine which has mods.");
				else if (MessageBox.Show("Really delete the configuration for " + engine + "?", "Confirm Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
				{
					Config.Instance.Engines.Remove(engine);
					Config.Save();
					RefreshData();
					lstEngines.SelectedItem = null;
				}
			}
		}

		private void btnAddEngine_Click(object sender, RoutedEventArgs e)
		{
			var engine = Engine.Find("New Engine");
			RefreshData();
			lstEngines.SelectedItem = engine;
		}

		private void btnAddMod_Click(object sender, RoutedEventArgs e)
		{
			var mod = Mod.Find("New Mod");
			RefreshData();
			lstMods.SelectedItem = mod;
		}

		private void btnDeleteMod_Click(object sender, RoutedEventArgs e)
		{
			var mod = lstMods.SelectedItem as Mod;
			if (mod != null)
			{
				if (MessageBox.Show("Really delete the configuration for " + mod + "?", "Confirm Delete", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
				{
					Config.Instance.Mods.Remove(mod);
					Config.Save();
					RefreshData();
					lstMods.SelectedItem = null;
				}
			}
		}

		private void btnSaveMod_Click(object sender, RoutedEventArgs e)
		{
			var mod = lstMods.SelectedItem as Mod;
			if (mod != null)
			{
				mod.Code = modCodeTextBox.Text;
				mod.Engine = ddlEngine.SelectedItem as Engine;
				mod.Path = modPathTextBox.Text;
				mod.SavePath = savePathTextBox.Text;
				mod.EmpirePath = empirePathTextBox.Text;
				mod.IsUnknown = false;
				Config.Save();
				RefreshData();
				lstMods.SelectedItem = null;
			}
		}

		private void lstMods_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var mod = lstMods.SelectedItem as Mod;
			gridModDetails.DataContext = mod;
		}

		private void btnDownload_Click(object sender, RoutedEventArgs e)
		{
			var game = (PlayerGame)gridPlayerGames.SelectedItem;
			if (CheckModAndEngine(game))
			{
				Cursor = Cursors.Wait;
				try
				{
					game.DownloadTurn();
					Cursor = Cursors.Arrow;
					if (MessageBox.Show("Turn downloaded. Play it now?", "Turn Ready", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
					{
						try
						{
							game.PlayTurn();
						}
						catch (Exception ex)
						{
							MessageBox.Show("Could not play turn: " + ex.Message + ".");
						}
					}
				}
				catch (Exception ex)
				{
					MessageBox.Show("Could not download turn: " + ex.Message + ".");
				}
				Cursor = Cursors.Arrow;
			}
		}

		private void btnPlay_Click(object sender, RoutedEventArgs e)
		{
			var game = (PlayerGame)gridPlayerGames.SelectedItem;
			if (CheckModAndEngine(game))
			{
				Cursor = Cursors.Wait;
				try
				{
					game.PlayTurn();
				}
				catch (Exception ex)
				{
					MessageBox.Show("Could not play turn: " + ex.Message + ".");
				}
			}
		}

		private void btnUpload_Click(object sender, RoutedEventArgs e)
		{
			var game = (PlayerGame)gridPlayerGames.SelectedItem;
			if (CheckModAndEngine(game))
			{
				Cursor = Cursors.Wait;
				try
				{
					game.UploadTurn();
				}
				catch (Exception ex)
				{
					MessageBox.Show("Could not upload turn: " + ex.Message + ".");
				}
			}
		}

		private bool CheckModAndEngine(Game game)
		{
			if (game == null)
			{
				MessageBox.Show("No game is selected.");
				return false;
			}
			if (game.Mod.IsUnknown)
			{
				MessageBox.Show("Unknown mod " + game.Mod + " for " + game.Engine + ". Please configure it.");
				lstMods.SelectedItem = game.Mod;
				tabMods.Focus();
				return false;
			}
			else if (game.Engine.IsUnknown)
			{
				MessageBox.Show("Unknown game engine " + game.Engine + ". Please configure it.");
				lstEngines.SelectedItem = game.Engine;
				tabEngines.Focus();
				return false;
			}
			else
				return true;

		}
	}
}

