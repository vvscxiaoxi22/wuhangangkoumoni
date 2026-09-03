using System;
using System.IO;
using PortTraining.Core.Station;
using UnityEngine;

namespace PortTraining.Core.Identity
{
    public sealed class LocalStudentSessionService
    {
        private const string CacheFileName = "student-session.json";

        public StudentLoginResult Login(string studentId, string password, StationProfile station)
        {
            if (station == null)
            {
                return StudentLoginResult.Rejected("本机工位配置未就绪，暂时无法登录。");
            }

            studentId = (studentId ?? string.Empty).Trim();
            password = password ?? string.Empty;

            if (studentId.Length < 4)
            {
                return StudentLoginResult.Rejected("请输入不少于 4 位的学号。");
            }

            if (password.Length < 4)
            {
                return StudentLoginResult.Rejected("请输入不少于 4 位的密码。");
            }

            var session = new StudentSession
            {
                sessionId = Guid.NewGuid().ToString("N"),
                studentId = studentId,
                studentName = $"实训学员 {studentId}",
                role = station.MachineType == MachineType.QC ? StudentRole.QC_DRIVER : StudentRole.YC_DRIVER,
                stationId = station.StationId,
                loginMode = StudentLoginMode.OfflineCache,
                loginAt = DateTime.UtcNow.ToString("O")
            };

            Save(session);
            return StudentLoginResult.Accepted(session);
        }

        public StudentSession LoadLastSession()
        {
            var path = GetCachePath();
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<StudentSession>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PortTraining] Student session cache ignored: {exception.Message}");
                return null;
            }
        }

        private static void Save(StudentSession session)
        {
            var path = GetCachePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(session, true));
        }

        private static string GetCachePath()
        {
            return Path.Combine(Application.persistentDataPath, "Identity", CacheFileName);
        }
    }
}

