using System;
using System.Runtime.InteropServices;

namespace TrayPerfmon.Plugin
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    [ComVisible(true)]
    public class PluginAttribute : Attribute
    {
        public string Name { get; }

        public string Description { get; }

        public PluginAttribute(string name, string description) {
            Name = name;
            Description = description;
        }
    }
}
