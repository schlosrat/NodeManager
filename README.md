# Node Manager
Provides services for other mods needing to create, delete, and manage maneuver nodes.

This mod is primarily meant as a service provider to other mods, which can call functions in this one without needing to recreate all these functions in the base mod. Creating maneuver nodes from a KSP2 mod is not necessarily an intuitive thing to code, so having this as a resource you can call may save you time developing those capabilities internally to your mod.

## Node Manager Capabilities

* **AddNode(burnUT)**: Calling this method will create an empty (0 Delta-v) node at the (optionally) specified (double) burnUT. If burnUT is not specified, then the new node will be at the next Ap (if there are currently no nodes and the eccentricity is < 1), or if there are nodes, then it will be at `Min(Orbit.period / 10, 600)` seconds after the last node, or if there are no nodes and e >= 1, then it will be 30s after the current time. 
* **CreateManeuverNodeAtTA(burnVector, TrueAnomalyRad, burnDurationOffsetFactor)**: This method will create a new node using the supplied (Vector3d) burnVector at the next time the active vessel will be at the specified (double) true anomaly (in radians), applying the optional (double) burnDurationOffsetFactor. If the burnDurationOffsetFactor is not specified, a default value of -0.5 will be used resulting in the burn time starting before the specified true anomaly occurs by 1/2 of the node's burn duration (as estimated by the game for the active vessel).
* **CreateManeuverNodeAtUT(burnVector, burnUT, burnDurationOffsetFactor)**: This method will create a new node using the supplied (Vector3d) burnVector at the specified (double) time for burn, applying the optional (double) burnDurationOffsetFactor. If the burnDurationOffsetFactor is not specified, a default value of -0.5 will be used resulting in the burn time starting before the specified time by 1/2 of the node's burn duration (as estimated by the game for the active vessel).
* **DeleteNodes(SelectedNodeIndex)**: This method takes an integer node index and will delete the node at that index along will all following nodes. If you pass it 0, then all nodes will be deleted.
* **RefreshActiveVesselAndCurrentManeuver()**: Calling this method will force Node Manager to refresh its local copy of the active vessel and current node. This is done automatically by Node Manager as needed, but if you want to force it to update you can.
* RefreshManeuverNodes(): Calling this method will trigger a call to RefreshActiveVesselAndCurrentManeuver and then rebuild Node Manager's internal list of nodes for the active vessel. This is done automatically by Node Manager as needed, but if you want to force it to update you can.
* **RefreshNodes()**: Calling this method will call `maneuverPlanComponent.UpdateNodeDetails(node)` and `maneuverPlanComponent.RefreshManeuverNodeState(i)` for each node the active vessel has. This can be useful if your mod is adjusting a node's BurnVector or Time and you want to make sure that node and all following nodes are properly updated for these effects.
* **SpitNode(SelectedNodeIndex, isError)**: This method takes an integer node index and, (optionally) a Boolean value, and will output info about the indexed node to the BepInex log. The defalt for isError is false. If isError is set true, the the log will go out at the Error level.
* **SpitNode(node, isError)**: This method takes a node (type ManeuverNodeData) and, (optionally) a Boolean value, and will output info about the indexed node to the BepInex log. The defalt for isError is false. If isError is set true, the the log will go out at the Error level.

* **activeVessel**. Access via `NodeManagerPlugin.Instance.activeVessel`. You can access this to check Node Manager's understanding of the active vessel.
* **currentNode**. Access via `NodeManagerPlugin.Instance.currentNode`. You can access this to check Node Manager's understanding of the current node.
* **Nodes**. Access via `NodeManagerPlugin.Instance.Nodes`. You can use this to check the number of nodes as `NodeManagerPlugin.Instance.Nodes.Count`

To use this mod from your mod you will need to do one of the following:

## Hard Dependency via Nuget Package
If core capabilities in your mod will rely on calling Node Manager methods, then setting up a **hard dependency** like this is the way to go, plus it's actually easier to develop your mod this way. There are two ways to set up your mod for development with Node Manager as a hard dependency, and this is the easiest of the two, so is the recommended way. Fundamentally, it works just like what you're already doing to reference BepInEx and SpaceWarp.

* The **advantage** to this way is coding will be easier for you! Just call NodeManagerPlugin.Instance.*method_name()* for any public method in Node Manager!
* The **disadvantage** to this way is you've got a *hard dependency* and your mod will not even start up unless Node Manager is installed. You may want to ship a copy of Node Manager with your mod (put both the node_manager plugin folder and your mod's plugin folder into the BepInEx/plugins folder before zipping it up). There may be a way to do this with CKAN in some automated fashion. This guide will be updated with those details, or a link to them, at some point.

### Step 1: Update your csproj file
In your csproj file you probably already have an ItemGroup where BepInEx, HarmonyX, and SpaceWarp are added as PackageReferene includes. All you need to do is add another PackageReference for NodeManager like the one sown below.

```xml
    <ItemGroup>
        <PackageReference Include="BepInEx.Analyzers" Version="1.*" PrivateAssets="all" />
        <PackageReference Include="BepInEx.AssemblyPublicizer.MSBuild" Version="0.4.0" PrivateAssets="all" />
        <PackageReference Include="BepInEx.Core" Version="5.*" />
        <PackageReference Include="BepInEx.PluginInfoProps" Version="2.*" />
        <PackageReference Include="HarmonyX" Version="2.10.1" />
        <PackageReference Include="SpaceWarp" Version="1.1.1" />
        <PackageReference Include="NodeManager" Version="0.5.2" />
        <PackageReference Include="UnityEngine.Modules" Version="2020.3.33" IncludeAssets="compile" />
    </ItemGroup>
```

### Step 2: Configure Namespace and Add Dependency
Bring in the NodeManger namespace in the class you want to call it from, and add Node Manager as a BepInDependency.

```cs
    using NodeManager;
    
    namespace MyCoolModsNameSpace;
    
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
    [BepInDependency(NodeManagerPlugin.ModGuid, NodeManagerPlugin.ModVer)]
    public class MyCoolModName : BaseSpaceWarpPlugin
```

### Step 3: Call Node Manager Methods from your Mod
You can now call any of Node Manager's public methods directly and easily from your code. Here are some examples:

```cs
    NodeManagerPlugin.Instance.RefreshActiveVesselAndCurrentManeuver();
    NodeManagerPlugin.Instance.RefreshManeuverNodes();
    NodeManagerPlugin.Instance.SpitNode(SelectedNodeIndex); // int
    NodeManagerPlugin.Instance.SpitNode(node); // ManeuverNodeData
    NodeManagerPlugin.Instance.DeleteNodes(SelectedNodeIndex); // int
    pass = NodeManagerPlugin.Instance.CreateManeuverNodeAtTA(burnVector, TrueAnomalyRad, burnDurationOffsetFactor); // Vector3d, double, double (default = -0.5)
    pass = NodeManagerPlugin.Instance.CreateManeuverNodeAtUT(burnVector, burnUT, burnDurationOffsetFactor); // Vector3d, double, double (default = -0.5)
    NodeManagerPlugin.Instance.UpdateNode(nodeData, nodeTimeAdj); // ManeuverNodeData, double (default = 0)
    pass = NodeManagerPlugin.Instance.AddNode(orbit) // PatchedConicsOrbit
```

### Step 4: Profit!

## Hard Dependency via Local Copy of Node Manager DLL
This way works like the method above with a few minor differences in your csproj and what you need to do in your mod's development folder.

### Step 1: Configure Assemblies
Add a copy of the node_manager.dll to your mod's list of Assemblies. Generally, this means two things. First, put the node_manager.dll in a folder where your mod can find it. You may want to put it in the same folder you have Assembly-CSharp.dll. Secondly, add it to your csproj file similarly to how you're already referencing Assembly-CSharp.dll. Your mod will need to have access to it, and know where to find it, when you compile your mod. At run time your mod will be accessing the node_manager.dll from the node_manager plugins folder where Node Manager is installed, so you don't need to distribute the Node Manager DLL with your mod, but it will need to be installed in the players game for you to be able to access it.

In your csproj file locate the ItemGroup where you have local References defined. There will be at least one for Assembly-CSharp.dll. You'll need to add another one for Node Manager like this.

```xml
    <ItemGroup>
        <Reference Include="Assembly-CSharp">
            <HintPath>..\external_dlls\Assembly-CSharp.dll</HintPath>
            <Publicize>true</Publicize>
            <Private>false</Private>
        </Reference>
        <Reference Include="node_manager">
            <HintPath>..\external_dlls\node_manager.dll</HintPath>
            <Private>false</Private>
        </Reference>
    </ItemGroup>
```

### Step 2: Configure Namespace and Add Dependency
See Step 2 above as there is no difference.

### Step 3: Call Node Manager Methods from your Mod
See Step 3 above as there is no difference.

### Step 4: Profit!

## Soft Dependency
This is the way to go if optional capabilities in your mod will rely on functions in this one, and you don't want to for the user to have a hard dependency on Node Manager.

* The **advantage** to this way is that your mod's users don't need to have Node Manager installed if they prefer not to have it, and you can distribute your mod without needed a hard dependency on Node Manager - meaning you mod can launch and run without Node Manager, although there may be some capabilities that aren't available to your users if they choose to pass on installing Node Manager.
* The **disadvantage** to this way is you'll need to do a bit more coding in your mod to be able to call Node Manager's methods from your mod.

### Step 1: configure Assemblies
This is the same as for Hard Dependency above as you'll not be able to compile without this.

## Step 2: Configure Namespace and Variables
Bring in the NodeManger namespace and create some variables in the class you want to call it from. This is almost the same as above.

```cs
   using NodeManager;
   
   private bool NMLoaded;
   PluginInfo NM;
```

### Step 3: Check for Node Manager
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
This is where things get really different for you compared to what's needed to call Node Manager methods using a hard dependency. For a soft dependency to work you're going to need to create a reflection calling method for each of the Node Manager methods that you would like to call from your mod. Here's an example of one for calling Node Manager's CreateManeuverNodeAtUT method which will pass it the burn vector you want, the time to schedule the burn and optionally a time offset. Using a time offset of -0.5 will cause the maneuver node to be centered on the time you supply rather than starting on the time.

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
