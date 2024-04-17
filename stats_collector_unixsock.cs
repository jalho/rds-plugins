using System;

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
        void OnTick()
        {
            this.log("OnTick was called!");
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
    }
}
