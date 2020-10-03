using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;

namespace TrayPerfmon.Plugin
{
    public class PluginInfo<T>
        where T : IPlugin
    {
        public static PluginInfo<T>[] LoadPlugins(string directory) {
            if (!Directory.Exists(directory)) {
                return null;
            }

            var plugins = new List<PluginInfo<T>>();
            foreach (var dllfile in Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories)) {
                try {
                    var assembly = Assembly.LoadFrom(dllfile);
                    foreach (var type in assembly.GetTypes()) {
                        var isInstanceable = type.IsClass && type.IsPublic && !type.IsAbstract;
                        var isAssignable = typeof(T).IsAssignableFrom(type);
                        if (isInstanceable && isAssignable) {
                            plugins.Add(new PluginInfo<T>(assembly, type));
                        }
                    }
                } catch (Exception ex) {
                    Debug.Fail(ex.Message);
                }
            }
            return plugins.ToArray();
        }

        public Assembly Assembly { get; }

        public Type Type { get; }

        public PluginInfo(Assembly assembly, Type type) {
            Assembly = assembly;
            Type = type;
        }

        public T CreateInstance() {
            var construct = Expression.Lambda<Func<T>>(Expression.New(Type)).Compile();
            return construct();
        }
    }
}
