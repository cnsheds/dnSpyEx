using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using dnSpy.Contracts.Debugger;
using dnSpy.Contracts.Text;

namespace dnSpy.ScyllaHide {


	[Export(typeof(IDbgManagerStartListener))]
	sealed class DebugStart : IDbgManagerStartListener 
	{
		public static SynchronizationContext main;
		public static DbgManager dbg;
		public static DebugStart Instance;
		public static DllInvoke SycllaHidePlugin;

		[Import]
		public ScyllaHideSettings ProgrammSettings { get; set; }

		public void OnStart(DbgManager dbgManager) {

			MyLogger.Instance.WriteLine(TextColor.Red, $"Dbg Manager is OnStart");
			dbg = dbgManager;
		    main = SynchronizationContext.Current;
			Instance = this;

			if (IntPtr.Size == 4) {
				SycllaHidePlugin = new DllInvoke("ScyllaHideDnSpyPluginx86.dll");
			}
			else {
				SycllaHidePlugin = new DllInvoke("ScyllaHideDnSpyPluginx64.dll");
			}
			ScyllaHideInit = SycllaHidePlugin.Invoke("ScyllaHideInit", typeof(Api_ScyllaHideInit)) as Api_ScyllaHideInit;
			if (ScyllaHideInit == null)
				return;
			ScyllaHideReset = SycllaHidePlugin.Invoke("ScyllaHideReset", typeof(Api_ScyllaHideReset)) as Api_ScyllaHideReset;
			ScyllaHideDebugLoop = SycllaHidePlugin.Invoke("ScyllaHideDebugLoop", typeof(Api_ScyllaHideDebugLoop)) as Api_ScyllaHideDebugLoop;


			dbgManager.IsRunningChanged += (sender, message) => { EventOnDbgManager("IsRuningChanged"); };
			dbgManager.IsDebuggingChanged += (sender, message) => { EventOnDbgManager("IsDebugingChanged"); };
			dbgManager.DebugTagsChanged += (sender, message) => { EventOnDbgManager("DebugTags " +message.Objects[0]); };
			dbgManager.DbgManagerMessage += (sender, message) => { EventOnDbgManager("DebugManagerMessage"); };
			dbgManager.CurrentRuntimeChanged += (sender, message) => { EventOnDbgManager("CurrentRuntimeChanged"); };
			dbgManager.ProcessesChanged += (sender, message) => { EventOnDbgManager("ProcessChanged"); };
			dbgManager.CurrentProcessChanged += (sender, message) => { EventOnDbgManager("CurrentProcessChanged"); };
			dbgManager.Message += (sender, args) => { MessageFromDbg(dbgManager,args); };
		}

		private void EventOnDbgManager(string text)
		{
			MyLogger.Instance.WriteLine(TextColor.Red,text);
		}

		private void MyCustomMethod(DbgManager dbgManager, DbgCollectionChangedEventArgs<DbgProcess> args)
		{
		}

		private static int order;
		private static void MessageFromDbg(DbgManager dbgManager, DbgMessageEventArgs message)
		{
			MyLogger.Instance.WriteLine($"We have message type {message.Kind.ToString()}");
			if (message.Kind == DbgMessageKind.ModuleLoaded)
			{
				DbgMessageModuleLoadedEventArgs moduleLoaded = (DbgMessageModuleLoadedEventArgs) message;
				MyLogger.Instance.WriteLine($"ModuleLoaded: {moduleLoaded.Module.Filename}");
			}
			if (!Instance.ProgrammSettings.IsEnabledOption)
				return;

			if (dbgManager.Processes.Length>0) {
				for (int i = 0; i < dbgManager.Processes.Length; i++)
				{
					int pid = dbgManager.Processes[i].Id;
					StartScyllaDide(pid,dbgManager,message);
					MyLogger.Instance.WriteLine(TextColor.Red, $"PointerSize = {dbgManager.Processes[i].PointerSize}");
				}
			}
		}

		private static void StartScyllaDide(int proccessId, DbgManager dbgManager, DbgMessageEventArgs mesage) {
			switch (mesage.Kind) {
			case DbgMessageKind.ProcessCreated:
				string currentDirectory = System.Environment.CurrentDirectory;
				ScyllaHideInit(currentDirectory);
				MyLogger.Instance.WriteLine(TextColor.Red, $"InitScyllaHide");

				DbgMessageProcessCreatedEventArgs processCreated = (DbgMessageProcessCreatedEventArgs)mesage;
				ScyllaHideDebugLoop(ECREATE_PROCESS_DEBUG_EVENT, (int)proccessId, true, false);
				ScyllaHideDebugLoop(EBREAKPOINT_DEBUG_EVENT, (int)proccessId);

				MyLogger.Instance.WriteLine(TextColor.Red, $"PID:{proccessId}, PointerSize = {processCreated.Process.PointerSize}");
				break;

			case DbgMessageKind.ModuleLoaded:

				DbgMessageModuleLoadedEventArgs moduleLoaded = (DbgMessageModuleLoadedEventArgs)mesage;
				string filename = moduleLoaded.Module.Filename;
				if (filename.Contains(".dll")) {
					bool IsNtDLL = filename.Contains("ntdll.dll");
					ScyllaHideDebugLoop(ELOAD_DLL_DEBUG_EVENT, (int)proccessId, false, IsNtDLL);

					MyLogger.Instance.WriteLine(TextColor.Red, $"Scylla Hide dll loaded	");
				}

				break;

			case DbgMessageKind.BoundBreakpoint:

				ScyllaHideDebugLoop(EBREAKPOINT_DEBUG_EVENT, (int)proccessId);
				MyLogger.Instance.WriteLine(TextColor.Red, $"Scylla Hide Breakpoint");
				break;

			default:
				ScyllaHideDebugLoop(0, (int)proccessId);
				MyLogger.Instance.WriteLine(TextColor.Red, $"Scylla Hide otherDebug message");
				break;
			}

		}

		private static void InjectUsingProgram(ulong proccessId, string scyllaProg, string dll)
		{
			ProcessStartInfo startInfo = new ProcessStartInfo();
			startInfo.FileName = scyllaProg;
			startInfo.Arguments = $"pid:{proccessId} {dll}";
			startInfo.CreateNoWindow = true;

			Thread newThrad = new Thread(() => { Process.Start(startInfo); });
			newThrad.Start();
		}

		// DEBUG_EVENT_CODE 定义
		public static int EBREAKPOINT_DEBUG_EVENT = 1;
		public static int ECREATE_THREAD_DEBUG_EVENT = 2;
		public static int ECREATE_PROCESS_DEBUG_EVENT = 3;
		public static int EEXIT_THREAD_DEBUG_EVENT = 4;
		public static int EEXIT_PROCESS_DEBUG_EVENT = 5;
		public static int ELOAD_DLL_DEBUG_EVENT = 6;
		public static int EUNLOAD_DLL_DEBUG_EVENT = 7;
		public static int EOUTPUT_DEBUG_STRING_EVENT = 8;
		public static int ERIP_EVENT = 9;


		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void Api_ScyllaHideInit([Out, MarshalAsAttribute(UnmanagedType.LPWStr)] string directory);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] 
		public delegate void Api_ScyllaHideReset();

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)] 
		public delegate void Api_ScyllaHideDebugLoop(int DebugEvent, int ProcessID, bool lpStartAddressIsNull = false, bool lpBaseOfNtDll = false);

		static Api_ScyllaHideInit? ScyllaHideInit;
		static Api_ScyllaHideReset? ScyllaHideReset;
		static Api_ScyllaHideDebugLoop? ScyllaHideDebugLoop;

	}
} 
