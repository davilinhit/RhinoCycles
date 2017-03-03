/**
Copyright 2014-2016 Robert McNeel and Associates

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
**/

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using ccl;
using Rhino;
using Rhino.PlugIns;
using Rhino.Render;
using RhinoCyclesCore.Core;

namespace RhinoCycles
{
	public class Plugin : PlugIn
	{
		/// <summary>
		/// Make sure we load AtStartup so that our view mode is
		/// available even when RhinoCycles isn't the current renderer
		/// </summary>
		public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

		protected override LoadReturnCode OnLoad(ref string errorMessage)
		{
			if (RhinoApp.RunningOnVMWare())
			{
				CSycles.putenv("CYCLES_OPENCL_TEST", "NONE");
			}
			// code got moved to separate DLL so use that to register from.
			var rccoreass = typeof(RcCore).Assembly;
			RenderContent.RegisterContent(rccoreass, Id);

			RenderContent.ContentFieldChanged += RenderContentOnContentFieldChanged;

			var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
			RcCore.It.PluginPath = path;
			var kernelPath = Path.Combine(path, "RhinoCycles");
			RcCore.It.KernelPath = kernelPath;
			var appPath = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
			RcCore.It.AppPath = appPath;
			kernelPath = RcCore.GetRelativePath(appPath, kernelPath);
			RcCore.It.KernelPathRelative = kernelPath;

			var dataPath = RhinoApp.GetDataDirectory(true, true);
			var userPath = Path.Combine(dataPath, "RhinoCycles", "data");

			RcCore.It.DataUserPath = userPath;

			CSycles.path_init(RcCore.It.KernelPath, RcCore.It.DataUserPath);

			RcCore.It.Initialised = false;
			AsyncInitialise();

			return LoadReturnCode.Success;
		}

		private static readonly object InitialiseLock = new object();
		private void AsyncInitialise()
		{
			var t = new Thread(InitialiseCSycles);
			t.Start();
		}

		/// <summary>
		/// Initialise Cycles if necessary.
		/// </summary>
		public void InitialiseCSycles()
		{
			lock (InitialiseLock)
			{
				if (!RcCore.It.Initialised)
				{
					CSycles.initialise();
					RcCore.It.Initialised = true;
					RcCore.It.EngineSettings.SelectedDevice = Settings.GetInteger("rc_renderdevice", -1);
					RcCore.It.EngineSettings.Samples = Settings.GetInteger("rc_samples", 10000);
					RcCore.It.EngineSettings.UseCustomSettings = Settings.GetBool("rc_usecustomsettings", false);
				}
			}
		}

		private void RenderContentOnContentFieldChanged(object sender, RenderContentFieldChangedEventArgs renderContentFieldChangedEventArgs)
		{
			//RhinoApp.WriteLine("... {0}", renderContentFieldChangedEventArgs.FieldName);
		}

		protected override void OnShutdown()
		{
			/* Clean up everything from C[CS]?ycles. */
			CSycles.shutdown();
			base.OnShutdown();
		}

	}
}
