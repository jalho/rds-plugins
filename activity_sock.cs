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

enum Category {
    /**
     * Case "player killed another player".
     */
    PvP,
    /**
     * Case e.g. "player got killed by NPC".
     */
    PvE,
    /**
     * Case e.g. "player collected wood".
     */
    Farm,
    /**
     * Case e.g. "crate spawned on cargo ship".
     */
    World,
}

class PlayerEventPvpKill : JSONSerializable {
    public Category category { get; set; }

    public ulong timestamp { get; set; }

    /** SteamID of the killer player. */
    public string id_subject { get; set; }

    /** SteamID of the killed player. */
    public string id_object { get; set; }
}

class PlayerEventPveDeath : JSONSerializable {
    public Category category { get; set; }

    public ulong timestamp { get; set; }

    /** Some identifier of the killer. */
    public string id_subject { get; set; }

    /** SteamID of the killed player. */
    public string id_object { get; set; }
}

class PlayerEventFarming : JSONSerializable {
    public Category category { get; set; }

    public ulong timestamp { get; set; }

    /** SteamID of the farming player. */
    public string id_subject { get; set; }

    /** Some identifier of what was farmed. */
    public string id_object { get; set; }

    /** How much was farmed. */
    public int quantity { get; set; }
}

class WorldEvent : JSONSerializable {
    public Category category { get; set; }

    public ulong timestamp { get; set; }

    /** Some identifier of the event. */
    public string id_subject { get; set; }
}

namespace Carbon.Plugins {
    [Info ( "activity_sock", "<jalho>", "0.1.0" )]
    [Description ( "Emit server activity messages over a Unix domain socket." )]
    public class activity_sock : CarbonPlugin {
        private string plugin_name = "activity_sock";
        private Socket socket = null;
        private UnixDomainSocketEndPoint endpoint = null;

        // constructor
        public activity_sock() {
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
                category = Category.Farm,
                timestamp = (ulong) timestamp,
                id_subject = (player.userID).ToString(),
                id_object = item.info.shortname,
                quantity = item.amount,
            };
            this.write_sock(farming_event);
            return (object) null;
        }

        /**
         * Carbon hook called e.g. when a player hits a tree for the last time
         * so that it falls down (as opposed to the initial hit, or its
         * subsequent hits that don't yet fall the tree).
         */
        void OnDispenserBonus(ResourceDispenser resource_dispencer, BasePlayer player, Item item) {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var farming_event = new PlayerEventFarming {
                category = Category.Farm,
                timestamp = (ulong) timestamp,
                id_subject = (player.userID).ToString(),
                id_object = item.info.shortname,
                quantity = item.amount,
            };
            this.write_sock(farming_event);
        }

        /**
         * Carbon hook called when a player gets killed.
         */
        object OnPlayerDeath(BasePlayer killed_player, HitInfo killer_info) {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            bool is_killer_player = killer_info?.InitiatorPlayer?.userID is ulong
                && !killer_info.InitiatorPlayer.IsNpc;
            bool is_suicide = is_killer_player
                && killer_info.InitiatorPlayer.userID == killed_player.userID;

            // case PvP
            if (is_killer_player && !is_suicide) {
                var death_event = new PlayerEventPvpKill {
                    category = Category.PvP,
                    timestamp = (ulong) timestamp,
                    id_subject = killer_info.InitiatorPlayer.userID.ToString(),
                    id_object = killed_player.userID.ToString(),
                };
                this.write_sock(death_event);
            }
            // case PvE
            else {
                string majority_damage_type;
                if (killer_info == null) {
                    majority_damage_type = "unknown PvE damage"; // ??
                } else {
                    majority_damage_type = killer_info.damageTypes.GetMajorityDamageType().ToString();
                }
                var death_event = new PlayerEventPveDeath {
                    category = Category.PvE,
                    timestamp = (ulong) timestamp,
                    id_subject = majority_damage_type,
                    id_object = killed_player.userID.ToString(),
                };
                this.write_sock(death_event);
            }
            return (object) null;
        }

        object OnGrowableGathered(GrowableEntity growable, Item gathered, BasePlayer player) {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var farming_event = new PlayerEventFarming {
                category = Category.Farm,
                timestamp = (ulong) timestamp,
                id_subject = (player.userID).ToString(),
                id_object = gathered.info.shortname,
                quantity = gathered.amount,
            };
            this.write_sock(farming_event);
            return (object) null;
        }

        /**
         * Carbon hook called e.g. when a player picks up a mushroom or a stump
         * (wood).
         */
        object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player, bool eat) {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var farming_event = new PlayerEventFarming {
                category = Category.Farm,
                timestamp = (ulong) timestamp,
                id_subject = (player.userID).ToString(),
                id_object = collectible.name,
                quantity = 1,
            };
            this.write_sock(farming_event);
            return (object) null;
        }

        object OnCargoShipSpawnCrate(CargoShip self) {
            // this.inspect_object(self);
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var world_event = new WorldEvent {
                category = Category.World,
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
