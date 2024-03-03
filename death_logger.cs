using System;
using System.Reflection;
using System.Text;

namespace Carbon.Plugins
{
    [Info ( "death_logger", "<jalho>", "0.1.0" )]
    [Description ( "Logs player deaths to stdout." )]
    public class death_logger : CarbonPlugin
    {
        object OnPlayerDeath(BasePlayer self, HitInfo info)
        {
            bool is_pvp = info?.InitiatorPlayer?.Connection is Network.Connection;
            string killed = self.Connection.ToString();
            string killer = is_pvp ? info.InitiatorPlayer.Connection.ToString() : "environment";
            string category = is_pvp ? "PvP" : "PvE";

            /**
             * EXAMPLE LOG -- CASE "PvE":
             *
             *   PvE: KILLER: 'environment', KILLED: '172.18.176.1:55941/76561198135242017/Raudus'
             *
             * EXAMPLE LOG -- CASE "PvP":
             *
             *   PvP: KILLER: '172.18.176.1:55941/76561198135242017/Raudus', KILLED: '172.18.176.1:55941/76561198135242017/Raudus'
             */
            Console.WriteLine($"{category}: KILLER: '{killer}', KILLED: '{killed}'");
            return (object) null;
        }
    }
}
