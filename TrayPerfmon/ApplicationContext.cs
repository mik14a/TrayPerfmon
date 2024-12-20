using System;
using System.Collections.Generic;
using System.ComponentModel.Plugin;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;

using TrayPerfmon.Plugin;
using TrayPerfmon.Properties;

namespace TrayPerfmon
{
    class ApplicationContext : System.Windows.Forms.ApplicationContext, IPluginHost, IDisposable
    {
        static string Repository { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Application.ProductName);

        public ApplicationContext() {
            var plugins = PluginInfo<NotifyIconPlugin>.LoadPlugins(Application.StartupPath).ToDictionary(p => p.Name);
            var toolStripItems = plugins.Select(p => new ToolStripMenuItem(p.Key, null, PluginSelectHandler) {
                Tag = p.Value
            }).ToArray();
            ContextMenuStrip contextMenuStrip = new();
            contextMenuStrip.Items.AddRange(toolStripItems);
            contextMenuStrip.Items.Add(new ToolStripSeparator());
            contextMenuStrip.Items.Add(new ToolStripMenuItem("S&ave", null, SaveHandler));
            contextMenuStrip.Items.Add(new ToolStripMenuItem("E&xit", null, ExitHandler));

#if DEBUG
            var notifyIconText = Application.ProductName + "(Debug)";
#else
            var notifyIconText = Application.ProductName;
#endif

            _notifyIcon = new NotifyIcon() {
                Text = notifyIconText,
                Icon = Resources.Icon,
                Visible = true,
                ContextMenuStrip = contextMenuStrip
            };

            if (Directory.Exists(Repository)) {
                foreach (var file in Directory.EnumerateFiles(Repository, "*.xml")) {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (plugins.TryGetValue(name, out var info)) {
                        var serializer = new XmlSerializer(info.Type);
                        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var plugin = (NotifyIconPlugin)serializer.Deserialize(stream);
                        plugin.Construct();
                        _plugins.Add(plugin);
                    }
                }
            }
        }

        void PluginSelectHandler(object sender, EventArgs e) {
            Debug.Assert(sender is ToolStripMenuItem);
            var toolStripMenuItem = sender as ToolStripMenuItem;
            var pluginInfo = toolStripMenuItem.Tag as PluginInfo<NotifyIconPlugin>;
            var plugin = pluginInfo.CreateInstance();
            plugin.Construct();
            _plugins.Add(plugin);
        }

        void SaveHandler(object sender, EventArgs e) {
            if (!Directory.Exists(Repository)) {
                _ = Directory.CreateDirectory(Repository);
            }

            foreach (var plugin in _plugins) {
                var name = plugin.GetType().GetCustomAttribute<PluginAttribute>().Name;
                var path = Path.Combine(Repository, name + ".xml");
                var serializer = new XmlSerializer(plugin.GetType());
                using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.Write);
                serializer.Serialize(stream, plugin);
            }
        }

        void ExitHandler(object sender, EventArgs e) {
            ExitThread();
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                foreach (var plugin in _plugins) {
                    plugin.Dispose();
                }

                _plugins.Clear();
                _notifyIcon.Dispose();
            }

            base.Dispose(disposing);
        }

        readonly NotifyIcon _notifyIcon;
        readonly List<NotifyIconPlugin> _plugins = [];
    }
}
