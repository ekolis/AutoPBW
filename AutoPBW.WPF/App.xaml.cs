using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using AutoPBW;
using WpfSingleInstanceByEventWaitHandle;

namespace AutoPBW.WPF
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			WpfSingleInstance.Make();
			Config.Load();

			this.DispatcherUnhandledException += App_DispatcherUnhandledException;
		}

		void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			MessageBox.Show("Unhandled exception occurred; please check errorlog.txt for more details.");
			var sw = new StreamWriter("errorlog.txt");
			sw.Write(e.Exception);
			sw.Close();
		}

		protected override void OnExit(ExitEventArgs e)
		{
			base.OnExit(e);
			Config.Save();
		}
	}
}
