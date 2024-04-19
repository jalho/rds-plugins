using System;
using System.Text;
using System.Net.Sockets;

/* For reference implementation of a stats collecting plugin, see
   Statistics DB by misticos: https://umod.org/plugins/statistics-db */

namespace Carbon.Plugins {
    [Info ( "stats_collector_unixsock", "<jalho>", "0.1.0" )]
    [Description ( "Collect stats about player activity. Emits the stats over Unix domain sockets." )]
    public class stats_collector_unixsock : CarbonPlugin {
        private string plugin_name = "stats_collector_unixsock";
        private Socket socket = null;
        private UnixDomainSocketEndPoint endpoint = null;

        // constructor
        public stats_collector_unixsock() {
            this.endpoint = new UnixDomainSocketEndPoint("/tmp/rds-stats-collector.sock");
            this.socket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);
        }

        /**
         * Docs: https://docs.carbonmod.gg/docs/core/hooks/server
         * [Accessed 2024-04-17]
         */ 
        void OnTick() {
            this.write_sock("OnTick");
        }

        /**
         * Called by Carbon to perform any plugin cleanup at unload.
         */
        public void Unload() {
           this.socket.Close();
        }

        private void log(string message) {
            string timestamp_iso = DateTime.UtcNow.ToString("o");
            System.Console.WriteLine($"[{timestamp_iso}] {this.plugin_name}: {message}");
        }

        private void write_sock(string message) {
            try {
                byte[] data = Encoding.UTF8.GetBytes(message);
                this.socket.SendTo(data, this.endpoint);
            } catch (Exception ex) {
                this.log($"Error writing to Unix domain socket: {ex.Message}");
            }
        }

    }
}
