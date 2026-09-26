using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using MySys22.DialogueEngine.Core;

namespace MySys22.DialogueEngine.Editor
{
    public static class DialogueAotTools
    {
        [MenuItem("MySys22/Dialogue/Generate AOT link.xml", false, 34)]
        public static void Generate()
        {
            var byAssembly = new Dictionary<string, List<string>>();

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string assemblyName = assembly.GetName().Name;
                if (assemblyName == null || assemblyName.StartsWith("Unity", StringComparison.Ordinal)) continue;
                if (assemblyName.StartsWith("System", StringComparison.Ordinal)) continue;

                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                catch (Exception) { continue; }

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract) continue;
                    if (!typeof(IAction).IsAssignableFrom(type)) continue;
                    if (!Attribute.IsDefined(type, typeof(DialogueActionAttribute))) continue;

                    if (!byAssembly.TryGetValue(assemblyName, out List<string> list))
                    {
                        list = new List<string>();
                        byAssembly[assemblyName] = list;
                    }
                    list.Add(type.FullName);
                }
            }

            var builder = new StringBuilder();
            builder.AppendLine("<linker>");
            builder.AppendLine("  <assembly fullname=\"MySys22.DialogueEngine.Runtime\" preserve=\"all\" />");
            builder.AppendLine("  <assembly fullname=\"YamlDotNet\" preserve=\"all\" />");

            int count = 0;
            foreach (var pair in byAssembly.OrderBy(p => p.Key))
            {
                builder.AppendLine($"  <assembly fullname=\"{pair.Key}\">");
                foreach (string typeName in pair.Value.OrderBy(t => t))
                {
                    builder.AppendLine($"    <type fullname=\"{typeName}\" preserve=\"all\" />");
                    count++;
                }
                builder.AppendLine("  </assembly>");
            }
            builder.AppendLine("</linker>");

            string path = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Assets", "link.xml");
            File.WriteAllText(path, builder.ToString());
            AssetDatabase.Refresh();

            DialogueLogger.Log($"AOT link.xml written ({count} action type(s) preserved): {path}");
            EditorUtility.DisplayDialog("AOT link.xml",
                $"{count} action type(s) preserved in:\n{path}\n\n" +
                "Required for IL2CPP builds: without it the compiler strips the action classes " +
                "and dialogue functions stop being registered.", "OK");
        }
    }
}
