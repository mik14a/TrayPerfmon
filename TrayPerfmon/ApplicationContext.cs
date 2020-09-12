using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using TrayPerfmon.Plugin;
using TrayPerfmon.Properties;

namespace TrayPerfmon
{
    class ApplicationContext : System.Windows.Forms.ApplicationContext
    {
        public ApplicationContext() {
            var catalog = new AggregateCatalog();
            catalog.Catalogs.Add(new DirectoryCatalog(Application.StartupPath));
            _container = new CompositionContainer(catalog);
            _container.ComposeParts(this);

            var toolStripItems = _plugins.Select(plugin => new ToolStripMenuItem(plugin.Metadata.Description, null, PluginSelectHandler) {
                    Tag = plugin
                })
                .ToArray();
            var contextMenuStrip = new ContextMenuStrip();
            contextMenuStrip.Items.AddRange(toolStripItems);
            contextMenuStrip.Items.Add(new ToolStripSeparator());
            contextMenuStrip.Items.Add(new ToolStripMenuItem("E&xit", null, ExitHandler));

            _notifyIcon = new NotifyIcon() {
                Text = Application.ProductName,
                Icon = Resources.Icon,
                Visible = true,
                ContextMenuStrip = contextMenuStrip
            };

            foreach (var plugin in _plugins) {
                Console.WriteLine(plugin.Metadata.Description);
            }
        }

        void PluginSelectHandler(object sender, EventArgs e) {
            Debug.Assert(sender is ToolStripMenuItem);
            var toolStripMenuItem = (ToolStripMenuItem)sender;
            var factory = (Lazy<INotifyIconPlugin>)toolStripMenuItem.Tag;
            var plugin = factory.Value;
            plugin.Construct();
        }

        void ExitHandler(object sender, EventArgs e) {
            ExitThread();
        }

        CompositionContainer _container;
        NotifyIcon _notifyIcon;

        [ImportMany]
        IEnumerable<Lazy<INotifyIconPlugin, INotifyIconPluginData>> _plugins = null;
    }
}
