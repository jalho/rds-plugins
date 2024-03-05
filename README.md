## Plugins

### `stats_collector`

Collects stats about player related events to memory and writes them to disk on
a regular interval. The written files are in .CSV format intended for easy
injection into some relational database.

Examples of the written files:

```
$ head ./carbon/data/stats_collector/*
==> carbon/data/stats_collector/FARMING_timestamp-farmer-farm-quantity.csv <==
1709569939,76561198135242017,assets/bundled/prefabs/autospawn/collectable/wood/wood-collectable.prefab,1
1709569940,76561198135242017,assets/bundled/prefabs/autospawn/collectable/wood/wood-collectable.prefab,1
1709569948,76561198135242017,wood,5
1709569949,76561198135242017,wood,5
1709569950,76561198135242017,wood,5
1709569952,76561198135242017,wood,5
1709569966,76561198135242017,wood,25
1709569967,76561198135242017,wood,25
1709569968,76561198135242017,wood,28
1709569970,76561198135242017,wood,25

==> carbon/data/stats_collector/PVE_timestamp-killer-killed.csv <==
1709569926,Suicide,76561198135242017
1709570186,Suicide,76561198135242017

==> carbon/data/stats_collector/PVP_timestamp-killer-killed.csv <==
```

The file names encode the column names.

## Example of a plugin

```cs
using System;
using System.Reflection;
using System.Text;

namespace Carbon.Plugins
{
    [Info ( "helloworld", "<jalho>", "0.1.0" )]
    [Description ( "My first plugin" )]
    public class helloworld : CarbonPlugin
    {
        /*
         * A hook: Something that Carbon calls when specific thing is detected
         * to have happened in the game.
         *
         * Docs for this specific hook:
         * - https://docs.carbonmod.gg/docs/core/hooks/entity
         */
        void OnEntitySpawn(BaseNetworkable networkable)
        {
            this.inspect_object(networkable);
        }

        /*
         * A private method made by us, to do whatever!
         *
         * Docs for the .NET API used here:
         * - https://learn.microsoft.com/en-us/dotnet/api/system.type.fullname?view=net-8.0#system-type-fullname
         */
        private void inspect_object(object inspectable)
        {
            Type inspectable_type = inspectable.GetType();
            PropertyInfo[] properties = inspectable_type.GetProperties();
            StringBuilder property_names = new StringBuilder();
            foreach (PropertyInfo property in properties)
            {
                property_names.Append(property.Name + "\n\t");
            }
            Console.WriteLine($"FullName: '{inspectable_type.FullName}', Property Names:\n\t{property_names}");
        }
    }
}
``` 

### Installing the plugin on a server:

Carbon detects change in filesystem automatically and reloads the plugin.

```
cp helloworld.cs /home/rust/carbon/plugins/
```
