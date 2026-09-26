using System;
using System.Collections.Generic;
using System.Reflection;

namespace MySys22.DialogueEngine.Core
{

    public static class ActionRegistry
    {

        public sealed class ActionInfo
        {
            public string Id;
            public string Description;
            public string[] RequiredParameters = Array.Empty<string>();
            public string[] OptionalParameters = Array.Empty<string>();
            public bool WaitsForCompletion;
            public bool CompletesExternally;
        }

        private static readonly Dictionary<string, Func<IAction>> _factories =
            new Dictionary<string, Func<IAction>>();

        private static readonly Dictionary<string, ActionInfo> _info =
            new Dictionary<string, ActionInfo>();

        private static readonly Dictionary<string, bool> _completesExternally =
            new Dictionary<string, bool>();

        private static bool _builtInsRegistered;
        private static bool _discovered;

        public static void RegisterAction(string actionId, Func<IAction> factory, ActionInfo info = null)
        {
            if (string.IsNullOrEmpty(actionId)) throw new ArgumentNullException(nameof(actionId));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            if (_factories.ContainsKey(actionId))
                DialogueLogger.LogWarning($"Action '{actionId}' already registered. Overwriting.");

            _factories[actionId] = factory;
            if (info != null) _info[actionId] = info;
            _completesExternally[actionId] = info?.CompletesExternally ?? false;

            DialogueLogger.Log($"Action registered: {actionId}");
        }

        public static void EnsureBuiltIns()
        {
            if (_builtInsRegistered) return;
            _builtInsRegistered = true;

            foreach (string id in BuiltInActions.All)
            {
                if (_factories.ContainsKey(id)) continue;
                string captured = id;
                _factories[id] = () => BuiltInActions.Create(captured);

                ActionRegistry.ActionInfo info = BuiltInActions.Info(captured);
                _info[id] = info;

                _completesExternally[id] = info?.CompletesExternally ?? false;
            }
        }

        public static int DiscoverActions(params Assembly[] assemblies)
        {
            if (_discovered && (assemblies == null || assemblies.Length == 0)) return 0;
            _discovered = true;

            if (assemblies == null || assemblies.Length == 0)
                assemblies = AppDomain.CurrentDomain.GetAssemblies();

            int count = 0;
            foreach (Assembly assembly in assemblies)
            {
                if (!IsScannable(assembly)) continue;
                count += DiscoverInAssembly(assembly);
            }

            if (count > 0) DialogueLogger.Log($"Action discovery: {count} annotated action(s) registered.");
            return count;
        }

        private static bool IsScannable(Assembly assembly)
        {
            string name = assembly.GetName().Name ?? "";
            if (name.StartsWith("System", StringComparison.Ordinal)) return false;
            if (name.StartsWith("Microsoft", StringComparison.Ordinal)) return false;
            if (name.StartsWith("Unity", StringComparison.Ordinal) &&
                !name.StartsWith("Assembly-CSharp", StringComparison.Ordinal)) return false;
            if (name.StartsWith("netstandard", StringComparison.Ordinal)) return false;
            if (name.StartsWith("mscorlib", StringComparison.Ordinal)) return false;
            return true;
        }

        private static int DiscoverInAssembly(Assembly assembly)
        {
            int count = 0;
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }
            catch (Exception)
            {
                return 0;
            }

            foreach (Type type in types)
            {
                if (type == null || type.IsAbstract || type.IsInterface) continue;
                if (!typeof(IAction).IsAssignableFrom(type)) continue;

                if (typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(type))
                {
                    DialogueLogger.LogWarning(
                        $"Action '{type.Name}' is a MonoBehaviour and was ignored. Dialogue functions " +
                        "must be plain C# classes (use MySys22.Engine.API instead of scene references).");
                    continue;
                }

                object[] attributes;
                try
                {
                    attributes = type.GetCustomAttributes(typeof(DialogueActionAttribute), false);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (object attribute in attributes)
                {
                    if (!(attribute is DialogueActionAttribute declared)) continue;

                    if (IsRegistered(declared.Id)) continue;

                    Type captured = type;
                    var info = new ActionInfo
                    {
                        Id = declared.Id,
                        Description = declared.Description,
                        RequiredParameters = declared.RequiredParameters ?? Array.Empty<string>(),
                        OptionalParameters = declared.OptionalParameters ?? Array.Empty<string>(),
                        WaitsForCompletion = declared.WaitsForCompletion,
                        CompletesExternally = declared.CompletesExternally
                    };

                    RegisterAction(declared.Id, () => (IAction)Activator.CreateInstance(captured), info);
                    count++;
                }
            }
            return count;
        }

        public static bool IsRegistered(string actionId)
            => !string.IsNullOrEmpty(actionId) && _factories.ContainsKey(actionId);

        public static IReadOnlyCollection<string> RegisteredActions => _factories.Keys;

        public static ActionInfo GetInfo(string actionId)
            => !string.IsNullOrEmpty(actionId) && _info.TryGetValue(actionId, out ActionInfo info) ? info : null;

        public static IReadOnlyDictionary<string, ActionInfo> AllInfo => _info;

        public static bool CompletesExternally(string actionId)
            => !string.IsNullOrEmpty(actionId) &&
               _completesExternally.TryGetValue(actionId, out bool value) && value;

        public static IAction Resolve(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return null;

            if (_factories.TryGetValue(actionId, out var factory))
                return factory();

            DialogueLogger.LogError("305", "Action not registered", actionId);
            return null;
        }

        public static void Clear()
        {
            _factories.Clear();
            _info.Clear();
            _completesExternally.Clear();
            _builtInsRegistered = false;
            _discovered = false;
            DialogueLogger.Log("ActionRegistry cleared.");
        }
    }
}
