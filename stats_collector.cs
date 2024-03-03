using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

class StatsAccumulationEvent {
    public long timestamp { get; set; }   // e.g. 1709489791 (Unix timestamp in seconds, UTC)
    public string type { get; set; }      // e.g. "farming" or some PvP/PvE categorization key
    public string resource { get; set; }  // e.g. "wood" (the resource that was farmed)
    public int amount { get; set; }       // e.g. 10 (amount of wood farmed)
    public ulong subject_id { get; set; } // e.g. 76561198135242017 (ID of the other player associated in the event)
}

namespace Carbon.Plugins {
    [Info ( "stats_collector", "<jalho>", "0.1.0" )]
    [Description ( "Collect stats about player activity." )]
    public class stats_collector : CarbonPlugin {
        private readonly ConcurrentDictionary<ulong, List<string>> aggregated_lines;
        private readonly string stats_dump_dir = @"rds-stats";
        private readonly Timer flush_timer_disk;

        public stats_collector() {
            if (!Directory.Exists(this.stats_dump_dir)) {
                Directory.CreateDirectory(this.stats_dump_dir);
                Console.WriteLine("Directory created: {0}", this.stats_dump_dir);
            } else {
                Console.WriteLine("Directory already exists: {0}", this.stats_dump_dir);
            }

            this.aggregated_lines = new ConcurrentDictionary<ulong, List<string>>();

            this.flush_timer_disk = new Timer(5000);
            this.flush_timer_disk.Elapsed += FlushAsync;
            this.flush_timer_disk.Start();
        }

        object OnDispenserGather(ResourceDispenser resource_dispenser, BasePlayer player, Item item) {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            StatsAccumulationEvent gather_event = new StatsAccumulationEvent {
                timestamp = timestamp,
                type = "farming",
                resource = item.info.shortname,
                amount = item.amount,
            };
            string gather_event_serialized = JsonConvert.SerializeObject(gather_event);
            var player_lines = this.aggregated_lines.GetOrAdd(player.userID, _ => new List<string>());
            player_lines.Add(gather_event_serialized);
            return (object) null;
        }

        /**
         * Hook called e.g. when a player hits a tree for the last time so that
         * it falls down.
         *
         * Docs: https://docs.carbonmod.gg/docs/core/hooks/resource#ondispenserbonus
         */
        void OnDispenserBonus(ResourceDispenser resource_dispencer, BasePlayer player, Item item) {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            StatsAccumulationEvent gather_event = new StatsAccumulationEvent {
                timestamp = timestamp,
                type = "farming",
                resource = item.info.shortname,
                amount = item.amount,
            };
            string gather_event_serialized = JsonConvert.SerializeObject(gather_event);
            var player_lines = this.aggregated_lines.GetOrAdd(player.userID, _ => new List<string>());
            player_lines.Add(gather_event_serialized);
        }

        /**
         * Hook called when a player gets killed.
         */
        object OnPlayerDeath(BasePlayer killed_player, HitInfo killer_info) {
            bool is_pvp = killer_info?.InitiatorPlayer?.userID is ulong;
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string category = is_pvp ? "pvp" : "pve";

            // PvE death event only
            if (!is_pvp) {
                StatsAccumulationEvent pve_death_event = new StatsAccumulationEvent {
                    timestamp = timestamp,
                    type = "pve-death",
                    subject_id = killed_player.userID,
                };
                string pve_death_event_serialized = JsonConvert.SerializeObject(pve_death_event);
                var player_lines = this.aggregated_lines.GetOrAdd(killed_player.userID, _ => new List<string>());
                player_lines.Add(pve_death_event_serialized);
                return (object) null;
            } else {
                // PvP kill event for killer player
                StatsAccumulationEvent pvp_kill_event = new StatsAccumulationEvent {
                    timestamp = timestamp,
                    type = "pvp-kill",
                    subject_id = killed_player.userID,
                };
                string pvp_kill_event_serialized = JsonConvert.SerializeObject(pvp_kill_event);
                var lines_killer = this.aggregated_lines.GetOrAdd(killer_info.InitiatorPlayer.userID, _ => new List<string>());
                lines_killer.Add(pvp_kill_event_serialized);

                // PvP death event for killed player
                StatsAccumulationEvent pvp_death_event = new StatsAccumulationEvent {
                    timestamp = timestamp,
                    type = "pvp-death",
                    subject_id = killer_info.InitiatorPlayer.userID,
                };
                string pvp_death_event_serialized = JsonConvert.SerializeObject(pvp_death_event);
                var lines_killed = this.aggregated_lines.GetOrAdd(killed_player.userID, _ => new List<string>());
                lines_killed.Add(pvp_death_event_serialized);

                return (object) null;
            }

        }

        object OnGrowableGather(GrowableEntity growable_entity) {
            Puts("OnGrowableGather was called!");
            return (object) null;
        }

        /**
         * Hook called e.g. when a player picks up a mushroom or a stump (wood).
         */
        object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player, bool eat) {
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            StatsAccumulationEvent collect_event = new StatsAccumulationEvent {
                timestamp = timestamp,
                type = "collecting",
                resource = collectible.name,
                amount = 1,
            };
            string collect_event_serialized = JsonConvert.SerializeObject(collect_event);
            var player_lines = this.aggregated_lines.GetOrAdd(player.userID, _ => new List<string>());
            player_lines.Add(collect_event_serialized);
            return (object) null;
        }

        void Unload() {
            flush_timer_disk.Stop();
        }

        private void FlushAsync(object sender, ElapsedEventArgs e) {
            var players_lines_flushable = new List<KeyValuePair<ulong, List<string>>>();

            // snapshot all player lines to avoid modification during flush
            foreach (var (player_id, lines) in this.aggregated_lines) {
                if (lines.Count > 0) {
                    // copy the list before removing
                    players_lines_flushable.Add(new KeyValuePair<ulong, List<string>>(player_id, new List<string>(lines)));
                    // remove the list from the dictionary to avoid concurrent access during flush
                    this.aggregated_lines.TryRemove(player_id, out _);
                }
            }

            // flush each player's lines separately
            foreach (var (player_id, lines) in players_lines_flushable) {
                string file_path = $@"{this.stats_dump_dir}/{player_id}.txt";
                using (StreamWriter writer = File.AppendText(file_path)) {
                    foreach (string line in lines) {
                        writer.WriteLine(line);
                    }
                }
            }
        }

    }
}
