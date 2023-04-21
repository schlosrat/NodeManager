# Node Manager
Provides services for other mods needing to create, delete, and manage maneuver nodes

This mod is primarily meant as a service provider to other mods, which can call functions in this one without needing to recreate all these functios in the base mod. To use this mod in your mod you will need to do the following:

## Configure Assemblies
Add the node_manager.dll to your mod's list of Assemblies. Generally, this means two things. First, put the node_manager.dll in a folder where your mod can find it. You may want to put it in the same folder you hace Assembly-CSharp.dll. Secondly, add it to your csproj file similarly to how you're already referenceing Assembly-CSharp.dll. Your mod will need to have access to it, and know to refer to it, when you compile your mod. At run time your mod will be accessing the node_manager.dll from the node_manager plugins folder where Node Manager is installed, so you don't need to distribute the Node Manager DLL with your mod, but it will need to be installed in the players game for you to be able to access it.

## Configure Namespace and Variables

Bring in the NodeManger namespace and create some variables in the class you want to call it from.

```cs
   using NodeManager;
   
   private bool NMLoaded;
   PluginInfo NM;
```

## Check for Node Manger
Somewhere in your mod you need to check to make sure Node Manager is loaded before you use it (e.g., OnInitialized()).

```cs
    if (Chainloader.PluginInfos.TryGetValue(NodeManagerPlugin.ModGuid, out NM))
    {
        NMLoaded = true;
        Logger.LogInfo("Node Manager installed and available");
        Logger.LogInfo($"MNC = {NM}");
    }
    else NMLoaded = false;
```

## Create Reflection Method
Create a reflection calling method for each of the Node Manager method that you would like to call from your mod. Here's an example of one for calling Node Manager's  CreateManeuverNodeAtUT method which will pass it the burn vector you want, the time to schedule the burn and optionally a time offset. Using a time offset of -0.5 will cause the maneuver node to be centered on the time you supply rather than starting on the time.

```cs
    private void CreateNodeAtUt(Vector3d burnVector, double UT, double burnDurationOffsetFactor = -0.5)
    {
        if (NMLoaded)
        {
            // Reflections method to call Node Manager methods from your mod
            var nmType = Type.GetType($"NodeManager.NodeManagerPlugin, {NodeManagerPlugin.ModGuid}");
            Logger.LogDebug($"Type name: {nmType!.Name}");
            var instanceProperty = nmType!.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            Logger.LogDebug($"Property name: {instanceProperty!.Name}");
            var methodInfo = instanceProperty!.PropertyType.GetMethod("CreateManeuverNodeAtUT");
            Logger.LogDebug($"Method name: {methodInfo!.Name}");
            methodInfo!.Invoke(instanceProperty.GetValue(null), new object[] { burnVector, UT, burnDurationOffsetFactor });
        }
    }
```
This example include some (optional) debug loging that may be helpful if you are having trouble with the reflection calling method. You can safely remove those once it's working to your satisfaction.

## Call Reflection Method
Call your reflection method wherever you need to invoke the corresponding Node Manager method

```cs
CreateNodeAtUt(burnVector, burnUT, -0.5);
```

## Profit!
