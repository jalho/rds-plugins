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
    public string type { get; set; }      // e.g. "farming" | "pvp"
    public long timestamp { get; set; }   // e.g. 1709489791
    public string resource { get; set; }  // e.g. "wood" (type "farming")
    public int amount { get; set; }       // e.g. 10 (type "farming)
    public ulong subject_id { get; set; } // e.g. 76561198135242017 (type "pvp" -- Steam ID of a player killed in PvP)
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
                type = "farming",
                timestamp = timestamp,
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
                type = "farming",
                timestamp = timestamp,
                resource = item.info.shortname,
                amount = item.amount,
            };
            string gather_event_serialized = JsonConvert.SerializeObject(gather_event);
            var player_lines = this.aggregated_lines.GetOrAdd(player.userID, _ => new List<string>());
            player_lines.Add(gather_event_serialized);
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
