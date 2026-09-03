using System;
using System.IO;
using UnityEngine;

namespace PortTraining.Core.Station
{
    public static class StationProfileLoader
    {
        private const string RelativePath = "Config/station.json";

        public static StationProfile Load()
        {
            var path = Path.Combine(Application.streamingAssetsPath, RelativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Station profile was not found.", path);
            }

            var json = File.ReadAllText(path);
            var data = JsonUtility.FromJson<StationProfileData>(json);
            if (data == null)
            {
                throw new InvalidOperationException($"Station profile is invalid: {path}");
            }

            return StationProfile.Create(data);
        }
    }
}

