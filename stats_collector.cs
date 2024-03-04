using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

class CsvRow {
    public virtual string to_csv_row() {
        var properties = this.GetType().GetProperties();
        var values = properties.Select(prop => prop.GetValue(this)?.ToString() ?? string.Empty);
        return string.Join(",", values);
    }
}

class PlayerEventPvpKill : CsvRow {
    public ulong timestamp { get; set; }

    /** SteamID of the killer player. */
    public ulong id_subject { get; set; }

    /** SteamID of the killed player. */
    public ulong id_object { get; set; }
}

class PlayerEventPveDeath : CsvRow {
    public ulong timestamp { get; set; }

    /** Some identifier of the killer. */
    public string id_subject { get; set; }

    /** SteamID of the killed player. */
    public ulong id_object { get; set; }
}

class PlayerEventFarming : CsvRow {
    public ulong timestamp { get; set; }

    /** SteamID of the farming player. */
    public ulong id_subject { get; set; }

    /** Some identifier of what was farmed. */
    public string id_object { get; set; }

    /** How much was farmed. */
    public int quantity { get; set; }
}

/* For reference implementation, see Statistics DB by misticos
   https://umod.org/plugins/statistics-db */

namespace Carbon.Plugins {
    [Info ( "stats_collector", "<jalho>", "0.1.0" )]
    [Description ( "Collect stats about player activity." )]
    public class stats_collector : CarbonPlugin {
        private List<PlayerEventPvpKill> player_event_pvp_kills = new List<PlayerEventPvpKill>();
        private List<PlayerEventPveDeath> player_event_pve_deaths = new List<PlayerEventPveDeath>();
        private List<PlayerEventFarming> player_event_farmings = new List<PlayerEventFarming>();
        private readonly string dumpfile_player_event_pvp_kills = @"carbon/data/stats_collector/PVP_timestamp-killer-killed.csv";
        private readonly string dumpfile_player_event_pve_deaths = @"carbon/data/stats_collector/PVE_timestamp-killer-killed.csv";
        private readonly string dumpfile_player_event_farmings = @"carbon/data/stats_collector/FARMING_timestamp-farmer-farm-quantity.csv";
        private readonly Timer flush_timer_disk;

        // constructor
        public stats_collector() {
            stats_collector.init_dump(this.dumpfile_player_event_pvp_kills);
            stats_collector.init_dump(this.dumpfile_player_event_pve_deaths);
            stats_collector.init_dump(this.dumpfile_player_event_farmings);

            this.flush_timer_disk = new Timer(5000);
            this.flush_timer_disk.Elapsed += this.flush_to_disk;
            this.flush_timer_disk.Start();
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
            this.player_event_farmings.Add(farming_event);
            return (object) null;
        }

        /**
         * Carbon hook called e.g. when a player hits a tree for the last time
         * so that it falls down (as opposed to the initial hit, or its
         * subsequent hits that don't yet fall the tree).
         *
         * Docs: https://docs.carbonmod.gg/docs/core/hooks/resource#ondispenserbonus
         */
        void OnDispenserBonus(ResourceDispenser resource_dispencer, BasePlayer player, Item item) {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var farming_event = new PlayerEventFarming {
                timestamp = (ulong) timestamp,
                id_subject = player.userID,
                id_object = item.info.shortname,
                quantity = item.amount,
            };
            this.player_event_farmings.Add(farming_event);
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
                    timestamp = (ulong) timestamp,
                    id_subject = killer_info.InitiatorPlayer.userID,
                    id_object = killed_player.userID,
                };
                this.player_event_pvp_kills.Add(death_event);
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
                    timestamp = (ulong) timestamp,
                    id_subject = majority_damage_type,
                    id_object = killed_player.userID,
                };
                this.player_event_pve_deaths.Add(death_event);
            }
            return (object) null;
        }

        object OnGrowableGathered(GrowableEntity growable, Item gathered, BasePlayer player) {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var farming_event = new PlayerEventFarming {
                timestamp = (ulong) timestamp,
                id_subject = player.userID,
                id_object = gathered.info.shortname,
                quantity = gathered.amount,
            };
            this.player_event_farmings.Add(farming_event);
            return (object) null;
        }

        /**
         * Carbon hook called e.g. when a player picks up a mushroom or a stump
         * (wood).
         */
        object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player, bool eat) {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var farming_event = new PlayerEventFarming {
                timestamp = (ulong) timestamp,
                id_subject = player.userID,
                id_object = collectible.name,
                quantity = 1,
            };
            this.player_event_farmings.Add(farming_event);
            return (object) null;
        }

        /**
         * Called by Carbon to perform any plugin cleanup at unload.
         */
        void Unload() {
            flush_timer_disk.Stop();
        }

        /**
         * Flush stats collected in memory to disk.
         */
        private void flush_to_disk(object sender, ElapsedEventArgs e) {
            var farmings_serialized = new List<string>();
            var pvp_kills_serialized = new List<string>();
            var pve_deaths_serialized = new List<string>();

            if (this.player_event_farmings.Count > 0) {
                var flush_count = this.player_event_farmings.Count;
                for (int i = 0; i < flush_count; i++) {
                    var event_serializable = this.player_event_farmings[i];
                    var serialized = event_serializable.to_csv_row();
                    farmings_serialized.Add(serialized);
                }
                this.player_event_farmings.RemoveRange(0, flush_count);
            }

            if (this.player_event_pvp_kills.Count > 0) {
                var flush_count = this.player_event_pvp_kills.Count;
                for (int i = 0; i < flush_count; i++) {
                    var event_serializable = this.player_event_pvp_kills[i];
                    var serialized = event_serializable.to_csv_row();
                    pvp_kills_serialized.Add(serialized);
                }
                this.player_event_pvp_kills.RemoveRange(0, flush_count);
            }

            if (this.player_event_pve_deaths.Count > 0) {
                var flush_count = this.player_event_pve_deaths.Count;
                for (int i = 0; i < flush_count; i++) {
                    var event_serializable = this.player_event_pve_deaths[i];
                    var serialized = event_serializable.to_csv_row();
                    pve_deaths_serialized.Add(serialized);
                }
                this.player_event_pve_deaths.RemoveRange(0, flush_count);
            }

            using (StreamWriter writer = File.AppendText(this.dumpfile_player_event_farmings)) {
                foreach (string line in farmings_serialized) {
                    writer.WriteLine(line);
                }
            }

            using (StreamWriter writer = File.AppendText(this.dumpfile_player_event_pvp_kills)) {
                foreach (string line in pvp_kills_serialized) {
                    writer.WriteLine(line);
                }
            }

            using (StreamWriter writer = File.AppendText(this.dumpfile_player_event_pve_deaths)) {
                foreach (string line in pve_deaths_serialized) {
                    writer.WriteLine(line);
                }
            }
        }

        static void init_dump(string file_path) {
            stats_collector.create_directory_structure(file_path);
            stats_collector.create_file_if_not_exists(file_path);
        }

        static void create_directory_structure(string file_path) {
            string directory_path = Path.GetDirectoryName(file_path);
            if (!Directory.Exists(directory_path)) {
                Directory.CreateDirectory(directory_path);
            }
        }

        static void create_file_if_not_exists(string file_path) {
            if (!File.Exists(file_path)) {
                File.WriteAllText(file_path, "");
            }
        }

    }
}
