using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Unity.Physics.Tests
{
    static class VerifyConsoleMessages
    {
        [Serializable]
        class Entries
        {
            public Entry[] Array;
        }

        [Serializable]
        class LogMessage
        {
            public string LogType;
            public string Message;
        }

        [Serializable]
        class Entry
        {
            public string[] Scenes;
            public LogMessage[] Messages;
        }

        /// <summary>
        /// 这直接取自 unity/unity Runtime\Logging\LogAssert.h。编辑器中没有 C# 等效项
        /// 因此，当本机枚举发生变化时，它也应该更新。
        /// </summary>
        [Flags]
        enum LogMessageFlags : int
        {
            kNoLogMessageFlags = 0,
            kError = 1 << 0, // 消息描述了一个错误。
            kAssert = 1 << 1, // 消息描述断言失败。
            kLog = 1 << 2, // 消息是一般日志消息。
            kFatal = 1 << 4, // 消息描述了一个致命错误，程序现在应该退出。
            kAssetImportError = 1 << 6, // 消息描述了资产导入期间生成的错误。
            kAssetImportWarning = 1 << 7, // 消息描述资产导入期间生成的警告。
            kScriptingError = 1 << 8, // 消息描述了脚本代码产生的错误。
            kScriptingWarning = 1 << 9, // 消息描述了脚本代码产生的警告。
            kScriptingLog = 1 << 10, // 消息描述由脚本代码生成的一般日志消息。
            kScriptCompileError = 1 << 11, // 消息描述了脚本编译器产生的错误。
            kScriptCompileWarning = 1 << 12, // 消息描述了脚本编译器产生的警告。

            kStickyLog =
                1 << 13, // 消息是“粘性的”，当用户手动清除控制台窗口时不应将其删除。

            kMayIgnoreLineNumber =
                1 << 14, // 脚本运行时应跳过使用文件和行信息注释日志调用堆栈。

            kReportBug =
                1 << 15, // 与 kFatal 一起使用时，指示日志 system 应启动错误报告器。

            kDisplayPreviousErrorInStatusBar =
                1 << 16, // 除非此消息之前没有任何消息，否则此消息之前的消息应显示在 Unity 主窗口的底部。
            kScriptingException = 1 << 17, // 消息描述脚本代码产生的异常。
            kDontExtractStacktrace = 1 << 18, // 对于此消息，应跳过堆栈跟踪提取。
            kScriptingAssertion = 1 << 21, // 该消息描述了脚本代码中的断言失败。

            kStacktraceIsPostprocessed =
                1 << 22, // 堆栈跟踪已经过后处理，不需要进一步处理。
            kIsCalledFromManaged = 1 << 23, // 正在从托管代码调用该消息。

            FromEditor = kDontExtractStacktrace | kMayIgnoreLineNumber | kIsCalledFromManaged,

            DebugLog = kScriptingLog | FromEditor,
            DebugWarning = kScriptingWarning | FromEditor,
            DebugError = kScriptingError | FromEditor,
            DebugException = kScriptingException | FromEditor,
            DebugAssert = kScriptingAssertion | FromEditor
        }

        class LogMessageFlagsExtensions
        {
            public static bool IsInfo(int flags)
            {
                return (flags & (int)(LogMessageFlags.kLog | LogMessageFlags.kScriptingLog)) != 0;
            }

            public static bool IsWarning(int flags)
            {
                return (flags & (int)(LogMessageFlags.kScriptCompileWarning | LogMessageFlags.kScriptingWarning |
                    LogMessageFlags.kAssetImportWarning)) != 0;
            }

            public static bool IsError(int flags)
            {
                return (flags & (int)(LogMessageFlags.kFatal | LogMessageFlags.kAssert | LogMessageFlags.kError |
                    LogMessageFlags.kScriptCompileError |
                    LogMessageFlags.kScriptingError | LogMessageFlags.kAssetImportError |
                    LogMessageFlags.kScriptingAssertion | LogMessageFlags.kScriptingException)) != 0;
            }
        }

        /// <summary>
        /// 这是为了避免潜在的不稳定。
        /// 例如：大多数情况下，Worker0 会打印一条消息，有时，Worker1 也会打印相同的消息。
        /// 我们通过从消息中删除 [Workerx] 来避免这种情况
        /// </summary>
        static readonly Regex WorkerMessage = new Regex("\\[Worker[0-9]\\] ", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        static Entries ParseAllAllowListedLogMessages()
        {
            var allowListPath = Path.Combine("Assets", "Tests", "SamplesTest", "sceneLogAllowList.json");
            return JsonUtility.FromJson<Entries>(File.ReadAllText(allowListPath));
        }

        static IEnumerable<Entry> GetAllowListedMessagesForScene(string scenePath)
        {
            return ParseAllAllowListedLogMessages().Array.Where(x => x.Scenes.Any(sceneEntry => new Regex(sceneEntry).IsMatch(scenePath)));
        }

        [Conditional("UNITY_EDITOR")]
        public static void ClearMessagesInConsole()
        {
#if UNITY_EDITOR
            Assembly assembly = Assembly.GetAssembly(typeof(UnityEditor.Scripting.ManagedDebugger));
            Type logEntries = assembly.CreateInstance("UnityEditor.LogEntries").GetType();
            logEntries.GetMethod("Clear").Invoke(null, null);
#endif
        }

        /// <summary>
        /// 迭代控制台条目并验证警告和错误是否为 allowListed。
        /// 如果消息不是 allowListed，则测试立即失败。
        ///
        /// 我们无法通过公共 APIs 来获取特定的控制台日志条目。
        /// 我们使用反射（抱歉）来访问正确的 APIs。这可能容易破裂。
        /// </summary>
        /// <param name="scenePath">Which 示例 scene 消息源自 from.</param>
        /// <exception cref="NotImplementedException">If 该消息源自未知 mode.</exception>
        [Conditional("UNITY_EDITOR")]
        public static void VerifyPrintedMessages(string scenePath)
        {
#if UNITY_EDITOR
            Assembly assembly = Assembly.GetAssembly(typeof(UnityEditor.Scripting.ManagedDebugger));
            Type logEntries = assembly.CreateInstance("UnityEditor.LogEntries").GetType();
            MethodInfo getCount = logEntries.GetMethod("GetCount");

            int logCount = (int)getCount.Invoke(null, null);
            object entry = assembly.CreateInstance("UnityEditor.LogEntry");
            Type entryType = entry.GetType();

            try
            {
                IEnumerable<Entry> expected = GetAllowListedMessagesForScene(scenePath);
                logEntries.GetMethod("StartGettingEntries").Invoke(null, null);
                for (int i = 0; i < logCount; i++)
                {
                    logEntries.GetMethod("GetEntryInternal").Invoke(null, new[] { i, entry });
                    var message = WorkerMessage.Replace(entryType.GetField("message").GetValue(entry).ToString(), string.Empty);
                    var mode = (int)entryType.GetField("mode").GetValue(entry);

                    foreach (var expectedMessages in expected)
                    {
                        if (LogMessageFlagsExtensions.IsInfo(mode))
                        {
                            // 跳过信息消息
                        }
                        else if (LogMessageFlagsExtensions.IsWarning(mode))
                        {
                            var warningMessage = message.Split("UnityEngine.Debug:LogWarning (object)")[0];
                            if (expectedMessages.Messages.Where(x => x.LogType.ToLowerInvariant() == "warning")
                                .All(x => !Regex.IsMatch(warningMessage, Regex.Escape(x.Message).Replace("__any__", ".*"))))
                            {
                                Assert.Fail($"{LogType.Warning}: was unexpected with message: {warningMessage}");
                            }
                        }
                        else if (LogMessageFlagsExtensions.IsError(mode))
                        {
                            var errorMessage = message.Split("UnityEngine.Debug:LogError (object)")[0];
                            if (expectedMessages.Messages.Where(x => x.LogType.ToLowerInvariant() == "error")
                                .All(x => !Regex.IsMatch(errorMessage, Regex.Escape(x.Message).Replace("__any__", ".*"))))
                            {
                                Assert.Fail($"{LogType.Error}: was unexpected with message: {errorMessage}");
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException($"{Enum.Parse<LogMessageFlags>(mode.ToString())}: mode was not expected for message: {message}");
                        }
                    }
                }
            }
            finally
            {
                logEntries.GetMethod("EndGettingEntries").Invoke(null, null);
                logEntries.GetMethod("Clear").Invoke(null, null);
            }
#endif
        }
    }
}
