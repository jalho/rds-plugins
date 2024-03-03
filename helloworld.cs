using System;
using System.Reflection;
using System.Text;

namespace Carbon.Plugins
{
    [Info ( "helloworld", "<jalho>", "0.1.0" )]
    [Description ( "My first plugin" )]
    public class helloworld : CarbonPlugin
    {
        void OnEntitySpawn(BaseNetworkable networkable)
        {
            this.inspect_object(networkable);
        }

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
