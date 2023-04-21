# Node Manager
Provides services for other mods needing to create, delete, and manage maneuver nodes.

This mod is primarily meant as a service provider to other mods, which can call functions in this one without needing to recreate all these functions in the base mod. To use this mod from your mod you will need to do one of the following:

## Hard Dependency
This is recommended if core capabilities in your mod will rely on functions in this one, and it's actually the easier one to set up.

* The **advantage** to this way is coding will be easier for you! Just call NodeManagerPlugin.Instance.*method_name()* for any public method in Node Manager!
* The **disadvantage** to this way is you've got a *hard dependency* and your mod will not even start up unless Node Manager is installed. You may want to ship a copy of Node Manager with your mod (put both the node_manager plugin folder and your mod's plugin folder into the BepInEx/plugins folder before zipping it up). There may be a way to do this with CKAN in some automated fashion. This guide will be updated with those details, or a link to them, at some point.

### Step 1: Configure Assemblies
Add the node_manager.dll to your mod's list of Assemblies. Generally, this means two things. First, put the node_manager.dll in a folder where your mod can find it. You may want to put it in the same folder you have Assembly-CSharp.dll. Secondly, add it to your csproj file similarly to how you're already referencing Assembly-CSharp.dll. Your mod will need to have access to it, and know where to find it, when you compile your mod. At run time your mod will be accessing the node_manager.dll from the node_manager plugins folder where Node Manager is installed, so you don't need to distribute the Node Manager DLL with your mod, but it will need to be installed in the players game for you to be able to access it.

### Step 2: Configure Namespace
Bring in the NodeManger namespace in the class you want to call it from.

```cs
   using NodeManager;
```

### Step 3: Call Node Manager Methods from your Mod
You can now call any of Node Manager's public methods directly and easily from your code. Here are some examples:

* NodeManagerPlugin.Instance.RefreshActiveVesselAndCurrentManeuver();
* NodeManagerPlugin.Instance.RefreshManeuverNodes();
* NodeManagerPlugin.Instance.SpitNode(SelectedNodeIndex); // int
* NodeManagerPlugin.Instance.SpitNode(node); // ManeuverNodeData
* NodeManagerPlugin.Instance.DeleteNodes(SelectedNodeIndex); // int
* NodeManagerPlugin.Instance.CreateManeuverNodeAtTA(burnVector, TrueAnomalyRad, burnDurationOffsetFactor); // Vector3d, double, double (default = -0.5)
* NodeManagerPlugin.Instance.CreateManeuverNodeAtUT(burnVector, burnUT, burnDurationOffsetFactor); // Vector3d, double, double (default = -0.5)
* NodeManagerPlugin.Instance.UpdateNode(nodeData, nodeTimeAdj); // ManeuverNodeData, double (default = 0)
* NodeManagerPlugin.Instance.AddNode(orbit) // PatchedConicsOrbit

### Step 4: Profit!

## Soft Dependency
This is the way to go if optional capabilities in your mod will rely on functions in this one, *and* you don't want to for the user to have a hard dependency on Node Manager.

* The **advantage** to this way is that your mod's users *don't need to have Node Manager installed* if they prefer not to have it, and you can distribute your mod without needed a hard dependency on Node Manager - meaning you mod can launch and run without Node Manager, although there may be some capabilities that aren't available to your users if they choose to pass on installing Node Manager.
* The **disadvantage** to this way is you'll need to do a bit more coding in your mod to be able to call Node Manager's methods from your mod.

### Step 1: Configure Assemblies
This is the same as for Hard Dependency above as you'll not be able to compile without this.

### Step 2: Configure Namespace and Variables
Bring in the NodeManger namespace *and create some variables* in the class you want to call it from. This is almost the same as above.

```cs
   using NodeManager;
   
   private bool NMLoaded;
   PluginInfo NM;
```

### Step 3: Check for Node Manger
Somewhere in your mod you need to check to make sure Node Manager is loaded before you use it (e.g., OnInitialized()). You don't need this with a hard dependency, but it's essential for a soft one.

```cs
    if (Chainloader.PluginInfos.TryGetValue(NodeManagerPlugin.ModGuid, out NM))
    {
        NMLoaded = true;
        Logger.LogInfo("Node Manager installed and available");
        Logger.LogInfo($"MNC = {NM}");
    }
    else NMLoaded = false;
```

### Step 4: Create Reflection Method
This is where things get really different for you compared to what's needed to call Node Manager methods using a hard dependency. For a soft dependency to work you're going to need to create a reflection calling method for each of the Node Manager methods that you would like to call from your mod. Here's an example of one for calling Node Manager's  CreateManeuverNodeAtUT method which will pass it the burn vector you want, the time to schedule the burn and optionally a time offset. Using a time offset of -0.5 will cause the maneuver node to be centered on the time you supply rather than starting on the time.

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
This example include some (optional) debug logging that may be helpful if you are having trouble with the reflection calling method. You can safely remove those once it's working to your satisfaction.

### Step 5: Call Reflection Method
Call your reflection method wherever you need to invoke the corresponding Node Manager method.

```cs
CreateNodeAtUt(burnVector, burnUT, -0.5);
```

### Step 6: Profit!
