using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器日志过滤：屏蔽 Unity 编辑器在域重载（重编译 / 进出 Play）时
/// 由 UnitySkills 插件快照机制触发的 3 个已知内部异常（与项目代码无关）：
///  - NullReferenceException（GameObjectInspector.OnDisable）
///  - ArgumentNullException（GameObjectInspector.OnEnable / PrefabUtility.IsPartOfVariantPrefab）
///  - SerializedObjectNotCreatableException（TransformInspector.OnEnable）
/// 其余日志全部原样转发，不影响正常排查。
/// </summary>
[InitializeOnLoad]
public static class EditorLogFilter
{
    static EditorLogFilter()
    {
        var original = Debug.unityLogger.logHandler;
        Debug.unityLogger.logHandler = new KnownEditorNoiseFilter(original);
    }

    private sealed class KnownEditorNoiseFilter : ILogHandler
    {
        private readonly ILogHandler _next;

        public KnownEditorNoiseFilter(ILogHandler next)
        {
            _next = next;
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            string msg = args != null && args.Length > 0 ? string.Format(format, args) : format;
            if (IsKnownEditorNoise(logType, msg)) return;
            _next.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            if (IsKnownEditorNoise(LogType.Error, exception?.ToString())) return;
            _next.LogException(exception, context);
        }

        private static bool IsKnownEditorNoise(LogType type, string message)
        {
            if (string.IsNullOrEmpty(message)) return false;

            // UnitySkills 插件加载任务历史时的无害提示（非项目代码，固定文案，可安全屏蔽）
            if (type == LogType.Warning &&
                message.Contains("[UnitySkills]") &&
                message.Contains("stripped unsafe assetPath from tasks"))
                return true;

            // 编辑器内部异常：必须同时命中对应 Inspector 栈帧，避免误伤真实错误
            if (type == LogType.Error || type == LogType.Exception)
            {
                return (message.Contains("SerializedObjectNotCreatableException") && message.Contains("TransformInspector")) ||
                       (message.Contains("NullReferenceException") && message.Contains("GameObjectInspector")) ||
                       (message.Contains("ArgumentNullException") && message.Contains("GameObjectInspector")) ||
                       (message.Contains("PrefabUtility.IsPartOfVariantPrefab") && message.Contains("GameObjectInspector"));
            }
            return false;
        }
    }
}
