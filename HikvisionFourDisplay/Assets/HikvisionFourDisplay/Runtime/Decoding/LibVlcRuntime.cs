using System;
using System.IO;
using LibVLCSharp.Shared;
using UnityEngine;

namespace HikvisionFourDisplay.Runtime.Decoding
{
    /// <summary>
    /// One-time process-wide libvlc setup shared by every LibVlcStreamDecoder instance.
    /// Expects libvlc.dll/libvlccore.dll beside the VLC module tree's "plugins" folder —
    /// confirmed by testing that
    /// libvlc_new() fails with "Failed to perform instanciation on the native side" if the
    /// modules live anywhere else, even with VLC_PLUGIN_PATH pointed at them; Core.Initialize
    /// appears to derive the plugin search path from the native directory itself rather than
    /// honoring that environment variable (still set here as a defensive fallback).
    ///
    /// The Editor runtime (the two native DLLs plus ~230 modules) lives at
    /// HikvisionFourDisplay/VlcRuntime, a project-root sibling of Assets/, because
    /// Unity's native-plugin importer flattens every DLL under Assets/Plugins/<platform>
    /// into a single directory in the build output — it does not preserve the
    /// plugins/access/, plugins/codec/, ... subfolder layout libvlc's module scan requires.
    /// LibVlcBuildPostprocessor copies that source tree into
    /// &lt;Build&gt;_Data/Plugins/x86_64/plugins after each Player build; in the Editor this
    /// class reads the source tree directly.
    /// </summary>
    internal static class LibVlcRuntime
    {
        private static bool initialized;
        private static Exception initializationError;

        public static void EnsureInitialized()
        {
            if (initialized) return;
            if (initializationError != null) throw initializationError;

            try
            {
#if UNITY_EDITOR
                // VLC_PLUGIN_PATH alone is not enough during libvlc_new(): plugin-defined
                // command-line options such as --rtsp-tcp are parsed before that external
                // tree is reliably loaded. Keep the native DLLs beside the module tree.
                var nativeDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "VlcRuntime"));
#else
                var nativeDir = Path.GetFullPath(Path.Combine(Application.dataPath, "Plugins", "x86_64"));
#endif
                var pluginsDir = Path.GetFullPath(Path.Combine(nativeDir, "plugins"));

                if (!Directory.Exists(nativeDir))
                    throw new DirectoryNotFoundException($"libvlc native directory not found: {nativeDir}");
                if (!Directory.Exists(pluginsDir))
                    throw new DirectoryNotFoundException($"libvlc plugins directory not found: {pluginsDir}");

                Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", pluginsDir);
                Core.Initialize(nativeDir);
                initialized = true;

                Debug.Log($"[HFD_LIBVLC_INIT] nativeDir={nativeDir} pluginsDir={pluginsDir} status=OK");
            }
            catch (Exception ex)
            {
                initializationError = ex;
                throw;
            }
        }
    }
}
