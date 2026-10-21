using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using System.IO;
#endif

namespace Hypocycloid.Utils
{
    public static class LogHelper
    {
        static long start_time;
        static long now_time;
        public static int idx = 0;
        static readonly Dictionary<string, long> timesav = new Dictionary<string, long>();

        public static void Log(object content)
        {
#if UNITY_EDITOR
            Debug.Log($"[{SceneManager.GetActiveScene().name}]: {content}");
#endif
        }

        public static void LogWarning(object content)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[{SceneManager.GetActiveScene().name}]: {content}");
#endif
        }

        public static void LogError(object content)
        {
#if UNITY_EDITOR
            Debug.LogError($"[{SceneManager.GetActiveScene().name}]: {content}");
#endif
        }

        public static void Log(string scope, object content)
        {
#if UNITY_EDITOR
            Debug.Log($"[{scope}]: {content}");
#endif
        }

        public static void LogWarning(string scope, object content)
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[{scope}]: {content}");
#endif
        }

        public static void LogError(string scope, object content)
        {
#if UNITY_EDITOR
            Debug.LogError($"[{scope}]: {content}");
#endif
        }

        #region execute_timer

        // public static string ToString_DHMS(this TimeSpan ts)
        // {
        //     return string.Format(GameApp.Instance.Localization["TimeSpan.DHMS"], ts.Days, ts.Hours, ts.Minutes, ts.Seconds);
        // }

        public static void LogStart(string timetag)
        {
            Log("Test", $"Executing {timetag} ->>>>>>>>>>>");
            start_time = GetTimeStamp();
            if (timesav.ContainsKey(timetag))
                timesav[timetag] = start_time;
            else
                timesav.Add(timetag, start_time);
        }

        public static void LogRecord(string timetag, bool record = false)
        {
            if (timesav.ContainsKey(timetag))
            {
                now_time = GetTimeStamp() - timesav[timetag];
                if (now_time > 0)
                {
                    Log("Test", $"End executing {timetag} ->>>>>>>>> Cost = {now_time / 1000f}s");
#if UNITY_EDITOR
                    if (record)
                    {
                        bool hasFile = File.Exists("Temp/CheckRuntime.csv");
                        var fileStream = new StreamWriter("Temp/CheckRuntime.csv", true);
                        if (!hasFile) fileStream.Write("Executing, cost(ms)\n");
                        // var fileStream = new StreamWriter(path);
                        // fileStream.WriteLine(str);
                        fileStream.Write($"{timetag}, {now_time}\n");
                        fileStream.Close();
                    }
#endif
                    // timesav.Remove(timetag);
                }
            }
            else
            {
                Log("Test", $"Record not found.");
            }

        }

        public static long GetTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0);
            long ret = Convert.ToInt64(ts.TotalMilliseconds);
            return ret;
        }

        public static void SetStartTime(string timetag)
        {
            start_time = GetTimeStamp();
            if (timesav.ContainsKey(timetag))
                timesav[timetag] = start_time;
            else
                timesav.Add(timetag, start_time);
        }

        public static long GetDisTime(string timetag, long aTime = 0)
        {
            if (timesav.ContainsKey(timetag))
            {
                now_time = GetTimeStamp() - timesav[timetag] + aTime;
            }
            return now_time;
        }

        public static void ClearTime(string timetag)
        {
            if (timesav.ContainsKey(timetag))
            {
                timesav.Remove(timetag);
            }
        }

        public static bool CheckTimeTag(string timetag)
        {
            return timesav.ContainsKey(timetag);
        }

        #endregion

    }

}