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

    /** Some identifier of the weapon. */
    public string weapon { get; set; }
    
    // TODO: Add location information?
}

class PlayerEventPveDeath : CsvRow {
    public ulong timestamp { get; set; }

    /** Some identifier of the killer. */
    public string id_subject { get; set; }

    /** SteamID of the killed player. */
    public ulong id_object { get; set; }
    
    // TODO: Add location information?
}

class PlayerEventFarming : CsvRow {
    public ulong timestamp { get; set; }

    /** SteamID of the farming player. */
    public ulong id_subject { get; set; }

    /** Some identifier of what was farmed. */
    public string id_object { get; set; }

    /** How much was farmed. */
    public int quantity { get; set; }
    
    // TODO: Add location information?
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
        private readonly string dumpfile_player_event_pvp_kills = @"carbon/data/stats_collector/pvp.csv";
        private readonly string dumpfile_player_event_pve_deaths = @"carbon/data/stats_collector/pve.csv";
        private readonly string dumpfile_player_event_farmings = @"carbon/data/stats_collector/farming.csv";
        private readonly Timer flush_timer_disk;

        // constructor
        public stats_collector() {
            stats_collector.init_dump(this.dumpfile_player_event_pvp_kills, "timestamp,killer,killed,weapon\n");
            stats_collector.init_dump(this.dumpfile_player_event_pve_deaths, "timestamp,killer,killed\n");
            stats_collector.init_dump(this.dumpfile_player_event_farmings, "timestamp,farm,quantity\n");

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
            bool is_pvp = killer_info?.InitiatorPlayer?.userID is ulong;
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string category = is_pvp ? "pvp" : "pve";

            // PvE death event only
            if (!is_pvp) {
                // StatsAccumulationEvent pve_death_event = new StatsAccumulationEvent {
                //     timestamp = timestamp,
                //     type = "pve-death",
                //     subject_id = killed_player.userID,
                // };
                // string pve_death_event_serialized = JsonConvert.SerializeObject(pve_death_event);
                // var player_lines = this.aggregated_lines.GetOrAdd(killed_player.userID, _ => new List<string>());
                // player_lines.Add(pve_death_event_serialized);
                return (object) null;
            } else {
                // case suicide
                if (killed_player.userID == killer_info.InitiatorPlayer.userID) {
                    // StatsAccumulationEvent suicide_event = new StatsAccumulationEvent {
                    //     timestamp = timestamp,
                    //     type = "suicide",
                    //     subject_id = killed_player.userID,
                    // };
                    // string suicide_event_serialized = JsonConvert.SerializeObject(suicide_event);
                    // var lines = this.aggregated_lines.GetOrAdd(killer_info.InitiatorPlayer.userID, _ => new List<string>());
                    // lines.Add(suicide_event_serialized);
                    return (object) null;
                }

                // PvP kill event for killer player
                // StatsAccumulationEvent pvp_kill_event = new StatsAccumulationEvent {
                //     timestamp = timestamp,
                //     type = "pvp-kill",
                //     subject_id = killed_player.userID,
                // };
                // string pvp_kill_event_serialized = JsonConvert.SerializeObject(pvp_kill_event);
                // var lines_killer = this.aggregated_lines.GetOrAdd(killer_info.InitiatorPlayer.userID, _ => new List<string>());
                // lines_killer.Add(pvp_kill_event_serialized);

                // PvP death event for killed player
                // StatsAccumulationEvent pvp_death_event = new StatsAccumulationEvent {
                //     timestamp = timestamp,
                //     type = "pvp-death",
                //     subject_id = killer_info.InitiatorPlayer.userID,
                // };
                // string pvp_death_event_serialized = JsonConvert.SerializeObject(pvp_death_event);
                // var lines_killed = this.aggregated_lines.GetOrAdd(killed_player.userID, _ => new List<string>());
                // lines_killed.Add(pvp_death_event_serialized);

                return (object) null;
            }

        }

        // TODO: Accumulate stats from OnGrowableGather!
        object OnGrowableGather(GrowableEntity growable_entity) {
            Puts("OnGrowableGather was called!");
            return (object) null;
        }

        /**
         * Carbon hook called e.g. when a player picks up a mushroom or a stump
         * (wood).
         */
        object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player, bool eat) {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // StatsAccumulationEvent collect_event = new StatsAccumulationEvent {
            //     timestamp = timestamp,
            //     type = "collecting",
            //     resource = collectible.name,
            //     amount = 1,
            // };
            // string collect_event_serialized = JsonConvert.SerializeObject(collect_event);
            // var player_lines = this.aggregated_lines.GetOrAdd(player.userID, _ => new List<string>());
            // player_lines.Add(collect_event_serialized);
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
            if (this.player_event_farmings.Count > 0) {
                var flush_count = this.player_event_farmings.Count;
                for (int i = 0; i < flush_count; i++) {
                    var farming_event = this.player_event_farmings[i];
                    var serialized = farming_event.to_csv_row();
                    farmings_serialized.Add(serialized);
                }
                this.player_event_farmings.RemoveRange(0, flush_count);
            }
            using (StreamWriter writer = File.AppendText(this.dumpfile_player_event_farmings)) {
                foreach (string line in farmings_serialized) {
                    writer.WriteLine(line);
                }
            }

            // foreach (var (player_id, lines) in this.aggregated_lines) {
            //     if (lines.Count > 0) {
            //         // copy the list before removing
            //         players_lines_flushable.Add(new KeyValuePair<ulong, List<string>>(player_id, new List<string>(lines)));
            //         // remove the list from the dictionary to avoid concurrent access during flush
            //         this.aggregated_lines.TryRemove(player_id, out _);
            //     }
            // }

            // flush each player's lines separately
            // foreach (var (player_id, lines) in players_lines_flushable) {
            //     string file_path = $@"{this.stats_dump_dir}/{player_id}.txt";
            //     using (StreamWriter writer = File.AppendText(file_path)) {
            //         foreach (string line in lines) {
            //             writer.WriteLine(line);
            //         }
            //     }
            // }
        }

        static void init_dump(string file_path, string initial_content) {
            stats_collector.create_directory_structure(file_path);
            stats_collector.create_file_if_not_exists(file_path, initial_content);
        }

        static void create_directory_structure(string file_path) {
            string directory_path = Path.GetDirectoryName(file_path);
            if (!Directory.Exists(directory_path)) {
                Directory.CreateDirectory(directory_path);
            }
        }

        static void create_file_if_not_exists(string file_path, string initial_content) {
            if (!File.Exists(file_path)) {
                File.WriteAllText(file_path, initial_content);
            }
        }

    }
}
