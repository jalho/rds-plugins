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

        // constructor
        public stats_collector_unixsock() {
        }

        /**
         * Docs: https://docs.carbonmod.gg/docs/core/hooks/server
         * [Accessed 2024-04-17]
         */ 
        void OnTick() {
            // this.log("OnTick was called!");
            this.write_sock("/tmp/asdasd.sock", "hello from tick!");
        }

        /**
         * Called by Carbon to perform any plugin cleanup at unload.
         */
        public void Unload() {
           // TODO: Do any socket cleanup here?
        }

        private void log(string message) {
            string timestamp_iso = DateTime.UtcNow.ToString("o");
            System.Console.WriteLine($"[{timestamp_iso}] {this.plugin_name}: {message}");
        }

        private void write_sock(string socket_fs_path, string message) {
            UnixDomainSocketEndPoint endpoint = new UnixDomainSocketEndPoint(socket_fs_path);
            try {
                using (Socket socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)) {
                    socket.Connect(endpoint);
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    socket.Send(data);
                }
            } catch (Exception ex) {
                Console.WriteLine($"Error writing to Unix domain socket: {ex.Message}");
            }
        }
    }
}
