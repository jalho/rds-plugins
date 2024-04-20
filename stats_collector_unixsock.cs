/* For reference implementation of a stats collecting plugin, see
   Statistics DB by misticos: https://umod.org/plugins/statistics-db */


using System;
using System.Text;
using Newtonsoft.Json;
using System.Net.Sockets;
using System.Reflection;

class JSONSerializable {
    public virtual string to_json() {
        return JsonConvert.SerializeObject(this);
    }
}

class PlayerEventPvpKill : JSONSerializable {
    public ulong timestamp { get; set; }

    /** SteamID of the killer player. */
    public ulong id_subject { get; set; }

    /** SteamID of the killed player. */
    public ulong id_object { get; set; }
}

class PlayerEventPveDeath : JSONSerializable {
    public ulong timestamp { get; set; }

    /** Some identifier of the killer. */
    public string id_subject { get; set; }

    /** SteamID of the killed player. */
    public ulong id_object { get; set; }
}

class PlayerEventFarming : JSONSerializable {
    public ulong timestamp { get; set; }

    /** SteamID of the farming player. */
    public ulong id_subject { get; set; }

    /** Some identifier of what was farmed. */
    public string id_object { get; set; }

    /** How much was farmed. */
    public int quantity { get; set; }
}

class WorldEvent : JSONSerializable {
    public ulong timestamp { get; set; }

    /** Some identifier of the event. */
    public string id_subject { get; set; }
}

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
         * Carbon hook called when a player gathers from a "dispenser", i.e.
         * e.g. a tree or a stone node.
         */
        object OnDispenserGather(ResourceDispenser resource_dispenser, BasePlayer player, Item item) {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var farming_event = new PlayerEventFarming {
                timestamp = (ulong) timestamp,
                id_subject = player.userID,
                id_object = item.info.shortname,
                quantity = item.amount,
            };
            this.write_sock(farming_event);
            return (object) null;
        }

        object OnCargoShipSpawnCrate(CargoShip self) {
            // this.inspect_object(self);
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var world_event = new WorldEvent {
                timestamp = (ulong) timestamp,
                id_subject = "OnCargoShipSpawnCrate",
            };
            this.write_sock(world_event);
            return (object) null;
        }

        /**
         * Debug helper method.
         */
        private void inspect_object(object inspectable) {
            Type inspectable_type = inspectable.GetType();
            PropertyInfo[] properties = inspectable_type.GetProperties();
            StringBuilder property_names = new StringBuilder();
            foreach (PropertyInfo property in properties)
            {
                property_names.Append(property.Name + "\n\t");
            }
            Console.WriteLine($"FullName: '{inspectable_type.FullName}', Property Names:\n\t{property_names}");
        }

        /**
         * Called by Carbon to perform any plugin cleanup at unload.
         */
        public void Unload() {
           this.socket.Dispose();
        }

        private void log(string message) {
            string timestamp_iso = DateTime.UtcNow.ToString("o");
            System.Console.WriteLine($"[{timestamp_iso}] {this.plugin_name}: {message}");
        }

        private void write_sock(JSONSerializable message) {
            try {
                byte[] data = Encoding.UTF8.GetBytes(message.to_json());
                this.socket.SendTo(data, this.endpoint);
            } catch (Exception ex) {
                this.log($"Error writing to Unix domain socket: {ex.Message}");
            }
        }

    }
}
