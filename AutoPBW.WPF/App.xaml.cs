using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
		}

		protected override void OnExit(ExitEventArgs e)
		{
			base.OnExit(e);
			Config.Save();
		}
	}
}
