using System.IO;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Training
{
    /// <summary>
    /// Resolves where this project's local runtime data (training run scores, teacher-published
    /// assessment tasks) lives on disk. Deliberately NOT Application.persistentDataPath: that
    /// path is keyed off Player Settings' Company Name/Product Name, and a misconfigured or
    /// later-changed name silently orphans previously-written data - hit for real on 2026-09-17,
    /// where it resolved to ".../YardCraneS7CommandWebSocketServer/..." instead of this
    /// project's own name, because Product Name had been left set to an unrelated sub-project's
    /// name at some point. A path relative to Application.dataPath instead ties the data to the
    /// project/build folder itself, which is stable regardless of what Player Settings says.
    /// </summary>
    public static class YardCraneLocalDataPaths
    {
        private const string RootDirectoryName = "LocalTrainingData";

        /// <summary>
        /// In the Editor, Application.dataPath is &lt;ProjectRoot&gt;/Assets, so one level up is
        /// the project root (next to Assets/, ProjectSettings/, ...) - not inside Assets/, so
        /// Unity's AssetDatabase never tries to import these files. In a built Player,
        /// Application.dataPath is &lt;BuildFolder&gt;/&lt;ProductName&gt;_Data, so one level up
        /// is the folder the executable itself lives in. Both cases land this next to something
        /// the project/build actually owns, not in a per-machine OS profile folder.
        /// </summary>
        public static string RootDirectory =>
            Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), RootDirectoryName);
    }
}
