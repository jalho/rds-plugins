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
