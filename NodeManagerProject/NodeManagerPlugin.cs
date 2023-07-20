using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using KSP.Game;
using KSP.Map;
using KSP.Messages;
using KSP.Sim;
using KSP.Sim.impl;
using KSP.Sim.Maneuver;
using KSP.UI.Binding;
using SpaceWarp;
using SpaceWarp.API.Mods;
using SpaceWarp.API.UI;
using UnityEngine;
using System.Collections;

/* This mod is primarily meant as a service provider to other mods, which can call functions in this one
 * without needing to recreate all these functios in the base mod. To use this mod in your mod you will
 * need to do the following:
 * 
 * Add the node_manager.dll to your mods list of Assemblies. Generally, put the dll in the same folder
 * you hace Assembly-CSharp.dll in and add it to your csproj file the same way. Your mod will need to
 * have access to it this way during compile time. At run time your mod will be accessing the 
 * node_manager.dll from the plugins folder where Node Manager is installed.
 * 
 * Bring in the NodeManger namespace
 * 
 *     using NodeManager;
 * 
 * Check to make sure Node Manager is loaded somewhere before you use it (e.g., OnInitialized())
 * 
    if (Chainloader.PluginInfos.TryGetValue(NodeManagerPlugin.ModGuid, var out NM))
    {
        NMLoaded = true;
        Logger.LogInfo("Node Manager installed and available");
        Logger.LogInfo($"MNC = {NM}");
    }
    else NMLoaded = false;
 * 
 * Create a reflection caller for each of the functions in this mod that you would like to call
 * similar to this example
 * 
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
 *
 * Call your reflection method wherever you need to invoke the corresponding Node Manager method
 * 
 * Profit!
 */

namespace NodeManager;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
public class NodeManagerPlugin : BaseSpaceWarpPlugin
{
    // These are useful in case some other mod wants to add a dependency to this one
    public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    public const string ModName = MyPluginInfo.PLUGIN_NAME;
    public const string ModVer = MyPluginInfo.PLUGIN_VERSION;

    public static NodeManagerPlugin Instance { get; set; }

    private bool loaded = false;
    private bool showGUI = false;
    private bool GUIenabled = true;
    private Rect _windowRect;
    private readonly int windowWidth = 320;
    private readonly int windowHeight = 320;

    private const string ToolbarFlightButtonID = "BTN-NodeManagerFlight";
    // private const string ToolbarOABButtonID = "BTN-NodeManagerOAB";

    private Rect closeBtnRect;
    private GUIStyle closeBtnStyle;

    public VesselComponent activeVessel;
    public ManeuverNodeData currentNode;
    public List<ManeuverNodeData> Nodes = new();
    private int maxNodes = 9; // This seems to be a hard limit on KSP2's capacity for nodes. Even making them manually
                              // in the game you will get NREs if you try to make a 10th one.

    // private static ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("ManeuverNodeController.Utility");
    //public ManualLogSource logger;
    public new static ManualLogSource Logger { get; set; }
    // private GameInstance game;

    /// <summary>
    /// Runs when the mod is first initialized.
    /// </summary>
    public override void OnInitialized()
    {
        base.OnInitialized();

        Instance = this;

        // game = GameManager.Instance.Game;
        Logger = base.Logger;

        // GameManager.Instance.Game.Messages.Subscribe<ManeuverRemovedMessage>(msg =>
        Game.Messages.Subscribe<ManeuverRemovedMessage>(msg =>
        {
            MessageCenterMessage message = (ManeuverRemovedMessage)msg;
            OnManeuverRemovedMessage(message);
        });

        // GameManager.Instance.Game.Messages.Subscribe<ManeuverCreatedMessage>(msg =>
        Game.Messages.Subscribe<ManeuverCreatedMessage>(msg =>
        {
            MessageCenterMessage message = (ManeuverCreatedMessage)msg;
            OnManeuverCreatedMessage(message);
        });

        Logger.LogInfo("Loaded");
        if (loaded)
        {
            Destroy(this);
        }
        loaded = true;

        closeBtnStyle = new GUIStyle(Skins.ConsoleSkin.button)
        {
            fontSize = 8
        };

        closeBtnRect = new Rect(windowWidth - 23, 6, 16, 16);

        // Register Flight AppBar button
        //Appbar.RegisterAppButton(
        //    "Node Manager",
        //    ToolbarFlightButtonID,
        //    AssetManager.GetAsset<Texture2D>($"{Info.Metadata.GUID}/images/icon.png"),
        //    // Toggle the GUI the MicroEngineer way
        //    delegate { showGUI = !showGUI; }
        //);

        //// Register OAB AppBar Button
        //Appbar.RegisterOABAppButton(
        //    "Node Manager",
        //    ToolbarOABButtonID,
        //    AssetManager.GetAsset<Texture2D>($"{Info.Metadata.GUID}/images/icon.png"),
        //    // Toggle the GUI the MicroEngineer way
        //    delegate { showGUI = !showGUI; }
        //);

        // Register all Harmony patches in the project
        Harmony.CreateAndPatchAll(typeof(NodeManagerPlugin).Assembly);

        //// Try to get the currently active vessel, set its throttle to 100% and toggle on the landing gear
        //try
        //{
        //    var currentVessel = Vehicle.ActiveVesselVehicle;
        //    if (currentVessel != null)
        //    {
        //        currentVessel.SetMainThrottle(1.0f);
        //        currentVessel.SetGearState(true);
        //    }
        //}
        //catch (Exception e) {}
        
        //// Fetch a configuration value or create a default one if it does not exist
        //var defaultValue = "my_value";
        //var configValue = Config.Bind<string>("Settings section", "Option 1", defaultValue, "Option description");
        
        //// Log the config value into <KSP2 Root>/BepInEx/LogOutput.log
        //Logger.LogInfo($"Option 1: {configValue.Value}");
    }

    private void OnManeuverRemovedMessage(MessageCenterMessage message)
    {
        // Update the lsit of nodes to capture the effect of the node deletion
        RefreshManeuverNodes();
    }

    private void OnManeuverCreatedMessage(MessageCenterMessage message)
    {
        // Update the lsit of nodes to capture the effect of the node deletion
        RefreshManeuverNodes();
    }

    //void Awake()
    //{
    //    windowRect = new Rect((Screen.width * 0.7f) - (windowWidth / 2), (Screen.height / 2) - (windowHeight / 2), 0, 0);
    //}

    //void Update()
    //{
    //    if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.H) && ValidScene)
    //        ToggleButton(!showGUI);
    //}

    //void ToggleButton(bool toggle)
    //{
    //    showGUI = toggle;
    //    GameObject.Find(ToolbarFlightButtonID)?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(toggle);
    //}

    /// <summary>
    /// Draws a simple UI window when <code>this._isWindowOpen</code> is set to <code>true</code>.
    /// </summary>
    private void OnGUI()
    {
        GUIenabled = false;
        var gameState = Game.GlobalGameState.GetState();
        if (gameState == GameState.Map3DView) GUIenabled = true;
        if (gameState == GameState.FlightView) GUIenabled = true;

        // Set the UI
        GUI.skin = Skins.ConsoleSkin;
        RefreshActiveVesselAndCurrentManeuver();
        if (showGUI && GUIenabled && Nodes.Count > 0) // activeVessel != null
        {
            _windowRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                _windowRect,
                FillWindow,
                "<color=#696DFF>// NODE MANAGER</color>",
                GUILayout.Height(0),
                GUILayout.Width(windowWidth)
            );
        }
    }

    /// <summary>
    /// Defines the content of the UI window drawn in the <code>OnGui</code> method.
    /// </summary>
    /// <param name="windowID"></param>
    private void FillWindow(int windowID)
    {
        if (CloseButton())
        {
            CloseWindow();
        }
        GUILayout.Label("Node Manager - Provides services for other mods to create, delete, and manage maneuver nodes.");
        GUI.DragWindow(new Rect(0, 0, windowWidth, windowHeight));
    }

    //public static ManeuverNodeData getCurrentNode(ref List<ManeuverNodeData> activeNodes)
    //{
    //    activeNodes = GameManager.Instance.Game.SpaceSimulation.Maneuvers.GetNodesForVessel(GameManager.Instance.Game.ViewController.GetActiveVehicle(true).Guid);
    //    return (activeNodes.Count() > 0) ? activeNodes[0] : null;
    //}

    private bool CloseButton()
    {
        return GUI.Button(closeBtnRect, "x", closeBtnStyle);
    }
    private void CloseWindow()
    {
        GameObject.Find("BTN-ResonantOrbitCalculatorFlight")?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
        GameObject.Find("BTN-ResonantOrbitCalculatorOAB")?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(false);
        showGUI = false;
        // GameManager.Instance.Game.Input.Enable();
        Game.Input.Enable();
    }

    public void RefreshActiveVesselAndCurrentManeuver()
    {
        activeVessel = GameManager.Instance?.Game?.ViewController?.GetActiveVehicle(true)?.GetSimVessel(true);
        currentNode = activeVessel != null ? GameManager.Instance?.Game?.SpaceSimulation.Maneuvers.GetNodesForVessel(activeVessel.GlobalId).FirstOrDefault() : null;
    }

    // Public in case other mods want to foce NodeManager to refresh the list. May not need to, but they can.
    public int RefreshManeuverNodes()
    {
        RefreshActiveVesselAndCurrentManeuver();
        ManeuverPlanComponent activeVesselPlan = activeVessel?.SimulationObject?.FindComponent<ManeuverPlanComponent>();
        if (activeVesselPlan != null)
        {
            Nodes = activeVesselPlan.GetNodes();
        }
        // else Logger.LogDebug("RefreshManeuverNodes: activeVesselPlan is null, Nodes list not updated.");

        return Nodes.Count;
    }

    // Spit out Node info to the log based on the requested index for a node
    public void SpitNode(int SelectedNodeIndex, bool isError = false)
    {
        RefreshManeuverNodes();
        if (SelectedNodeIndex < Nodes.Count)
        {
            ManeuverNodeData node = Nodes[SelectedNodeIndex];
            if (isError)
            {
                Logger.LogError($"SpitNode: Node[{SelectedNodeIndex}]");
                Logger.LogError($"BurnDuration:             {node.BurnDuration} s");
                Logger.LogError($"BurnRequiredDV:           {node.BurnRequiredDV} m/s");
                Logger.LogError($"BurnVector:               [{node.BurnVector.x}, {node.BurnVector.y}, {node.BurnVector.z}] = {node.BurnVector.magnitude} m/s");
                Logger.LogError($"CachedManeuverPatchEndUT: {node.CachedManeuverPatchEndUT} s");
                Logger.LogError($"IsOnManeuverTrajectory:   {node.IsOnManeuverTrajectory}");
                Logger.LogError($"ManeuverTrajectoryPatch:  {node.ManeuverTrajectoryPatch}");
                Logger.LogError($"NodeID:                   {node.NodeID}");
                Logger.LogError($"NodeName:                 {node.NodeName}");
                Logger.LogError($"RelatedSimID:             {node.RelatedSimID}");
                Logger.LogError($"SimTransform:             {node.SimTransform}");
            }
            else
            {
                Logger.LogInfo($"SpitNode: Node[{SelectedNodeIndex}]");
                Logger.LogInfo($"BurnDuration:             {node.BurnDuration} s");
                Logger.LogInfo($"BurnRequiredDV:           {node.BurnRequiredDV} m/s");
                Logger.LogInfo($"BurnVector:               [{node.BurnVector.x}, {node.BurnVector.y}, {node.BurnVector.z}] = {node.BurnVector.magnitude} m/s");
                Logger.LogInfo($"CachedManeuverPatchEndUT: {node.CachedManeuverPatchEndUT} s");
                Logger.LogInfo($"IsOnManeuverTrajectory:   {node.IsOnManeuverTrajectory}");
                Logger.LogInfo($"ManeuverTrajectoryPatch:  {node.ManeuverTrajectoryPatch}");
                Logger.LogInfo($"NodeID:                   {node.NodeID}");
                Logger.LogInfo($"NodeName:                 {node.NodeName}");
                Logger.LogInfo($"RelatedSimID:             {node.RelatedSimID}");
                Logger.LogInfo($"SimTransform:             {node.SimTransform}");
            }
        }
    }

    // Spit out Node info to the log based on the node passed in
    public void SpitNode(ManeuverNodeData node, bool isError = false)
    {
        if ( node != null )
        {
            if (isError)
            {
                Logger.LogError($"SpitNode: Node:");
                Logger.LogError($"BurnDuration:             {node.BurnDuration} s");
                Logger.LogError($"BurnRequiredDV:           {node.BurnRequiredDV} m/s");
                Logger.LogError($"BurnVector:               [{node.BurnVector.x}, {node.BurnVector.y}, {node.BurnVector.z}] = {node.BurnVector.magnitude} m/s");
                Logger.LogError($"CachedManeuverPatchEndUT: {node.CachedManeuverPatchEndUT} s");
                Logger.LogError($"IsOnManeuverTrajectory:   {node.IsOnManeuverTrajectory}");
                Logger.LogError($"ManeuverTrajectoryPatch:  {node.ManeuverTrajectoryPatch}");
                Logger.LogError($"NodeID:                   {node.NodeID}");
                Logger.LogError($"NodeName:                 {node.NodeName}");
                Logger.LogError($"RelatedSimID:             {node.RelatedSimID}");
                Logger.LogError($"SimTransform:             {node.SimTransform}");
            }
            else
            {
                Logger.LogInfo($"SpitNode: Node:");
                Logger.LogInfo($"BurnDuration:             {node.BurnDuration} s");
                Logger.LogInfo($"BurnRequiredDV:           {node.BurnRequiredDV} m/s");
                Logger.LogInfo($"BurnVector:               [{node.BurnVector.x}, {node.BurnVector.y}, {node.BurnVector.z}] = {node.BurnVector.magnitude} m/s");
                Logger.LogInfo($"CachedManeuverPatchEndUT: {node.CachedManeuverPatchEndUT} s");
                Logger.LogInfo($"IsOnManeuverTrajectory:   {node.IsOnManeuverTrajectory}");
                Logger.LogInfo($"ManeuverTrajectoryPatch:  {node.ManeuverTrajectoryPatch}");
                Logger.LogInfo($"NodeID:                   {node.NodeID}");
                Logger.LogInfo($"NodeName:                 {node.NodeName}");
                Logger.LogInfo($"RelatedSimID:             {node.RelatedSimID}");
                Logger.LogInfo($"SimTransform:             {node.SimTransform}");
            }

        }
    }

    public void DeleteNode(int SelectedNodeIndex)
    {
        RefreshManeuverNodes();
        List<ManeuverNodeData> nodesToDelete = new List<ManeuverNodeData>();
        ManeuverNodeData nodeToDelete;
        // double UT = GameManager.Instance.Game.UniverseModel.UniversalTime;
        double UT = Game.UniverseModel.UniversalTime;

        // This should never happen, but better be safe
        if (SelectedNodeIndex + 1 > Nodes.Count)
            SelectedNodeIndex = Math.Max(0, Nodes.Count - 1);

        if (Nodes[SelectedNodeIndex].Time < UT && !Nodes[SelectedNodeIndex].IsOnManeuverTrajectory)
        {
            nodeToDelete = Nodes[SelectedNodeIndex];
            nodesToDelete.Add(nodeToDelete);
            // GameManager.Instance.Game.SpaceSimulation.Maneuvers.RemoveNodesFromVessel(activeVessel.GlobalId, nodesToDelete);
            Game.SpaceSimulation.Maneuvers.RemoveNodesFromVessel(activeVessel.GlobalId, nodesToDelete);
        }
    }

    public void DeletePastNodes()
    {
        RefreshManeuverNodes();
        List<ManeuverNodeData> nodesToDelete = new List<ManeuverNodeData>();
        ManeuverNodeData nodeToDelete;
        // double UT = GameManager.Instance.Game.UniverseModel.UniversalTime;
        double UT = Game.UniverseModel.UniversalTime;

        foreach (ManeuverNodeData node in Nodes)
        {
            if (!nodesToDelete.Contains(node) && (!node.IsOnManeuverTrajectory || node.Time < UT))
                nodesToDelete.Add(node);
        }
        // GameManager.Instance.Game.SpaceSimulation.Maneuvers.RemoveNodesFromVessel(activeVessel.GlobalId, nodesToDelete);
        Game.SpaceSimulation.Maneuvers.RemoveNodesFromVessel(activeVessel.GlobalId, nodesToDelete);
    }

    public void DeleteNodes(int SelectedNodeIndex)
    {
        RefreshManeuverNodes();
        List<ManeuverNodeData> nodesToDelete = new List<ManeuverNodeData>();

        // This should never happen, but better be safe
        if (SelectedNodeIndex + 1 > Nodes.Count)
            SelectedNodeIndex = Math.Max(0, Nodes.Count - 1);

        ManeuverNodeData nodeToDelete = Nodes[SelectedNodeIndex];
        nodesToDelete.Add(nodeToDelete);

        foreach (ManeuverNodeData node in Nodes)
        {
            if (!nodesToDelete.Contains(node) && (!nodeToDelete.IsOnManeuverTrajectory || nodeToDelete.Time < node.Time))
                nodesToDelete.Add(node);
        }
        // GameManager.Instance.Game.SpaceSimulation.Maneuvers.RemoveNodesFromVessel(activeVessel.GlobalId, nodesToDelete);
        Game.SpaceSimulation.Maneuvers.RemoveNodesFromVessel(activeVessel.GlobalId, nodesToDelete);
    }

    private IPatchedOrbit GetLastOrbit(bool silent = true)
    {
        RefreshManeuverNodes();

        if (!silent)
            Logger.LogDebug($"GetLastOrbit: Nodes.Count = {Nodes.Count}");

        if (Nodes.Count == 0) // There are no nodes, so use the activeVessel.Orbit
        {
            if (activeVessel == null)
            {
                Logger.LogWarning($"GetLastOrbit: activeVessel is null. Unable to proceed.");
                return null;
            }

            if (!silent)
                Logger.LogDebug($"GetLastOrbit: last orbit is activeVessel.Orbit: {activeVessel.Orbit}");
            return activeVessel.Orbit;
        }

        // Get the last patch in the list
        IPatchedOrbit lastOrbit = Nodes[Nodes.Count - 1].ManeuverTrajectoryPatch;
        if (!silent)
        {
            Logger.LogDebug($"GetLastOrbit: last orbit is patch {Nodes.Count - 1}: {lastOrbit}");
        }

        return lastOrbit;
    }

    public bool CreateManeuverNodeAtTA(Vector3d burnVector, double TrueAnomalyRad, double burnDurationOffsetFactor = -0.5)
    {
        // Logger.LogDebug("CreateManeuverNodeAtTA");
        PatchedConicsOrbit referencedOrbit = GetLastOrbit(true) as PatchedConicsOrbit;
        if (referencedOrbit == null)
        {
            Logger.LogWarning("CreateManeuverNodeAtTA: referencedOrbit is null. Unable to proceed.");
            return false;
        }

        double UT = referencedOrbit.GetUTforTrueAnomaly(TrueAnomalyRad, 0);

        return CreateManeuverNodeAtUT(burnVector, UT, burnDurationOffsetFactor);
    }

    public bool CreateManeuverNodeAtUT(Vector3d burnVector, double burnUT, double burnDurationOffsetFactor = -0.5)
    {
        RefreshManeuverNodes();
        if (activeVessel == null)
        {
            Logger.LogWarning($"CreateManeuverNodeAtUT: activeVessel is null. Unable to proceed.");
            return false;
        }
        if (Nodes.Count >= maxNodes)
        {
            Logger.LogWarning($"CreateManeuverNodeAtUT: Max Nodes Limit ({maxNodes}) reached. Unable to proceed.");
            return false;
        }
        // var UT = GameManager.Instance.Game.UniverseModel.UniversalTime;
        double UT = Game.UniverseModel.UniversalTime;
        Logger.LogDebug($"CreateManeuverNodeAtUT: burnVector  = [{burnVector.x}, {burnVector.y}, {burnVector.z}] = {burnVector.magnitude} m/s");
        Logger.LogDebug($"CreateManeuverNodeAtUT: burnUT      = {burnUT - UT} s from now");
        Logger.LogDebug($"CreateManeuverNodeAtUT: offsetFac   = {burnDurationOffsetFactor}");
        Logger.LogDebug($"CreateManeuverNodeAtUT: Nodes.Count = {Nodes.Count}");

        if (burnUT < UT + 1) // Don't set node to now or in the past
            burnUT = UT + 1;

        IPatchedOrbit patch = null;
        bool isManeuver = false;
        // maneuverPlanSolver.FindPatchContainingUt(UT, maneuverPlanSolver.ManeuverTrajectory, out orbit, out int _);
        if ( Nodes.Count > 0 )
        {
            if ( burnUT > Nodes[0].Time )
            {
                isManeuver = true;
                // Get the patch to put this node on
                ManeuverPlanSolver maneuverPlanSolver = activeVessel.Orbiter?.ManeuverPlanSolver;
                maneuverPlanSolver.FindPatchContainingUt(UT, maneuverPlanSolver.ManeuverTrajectory, out patch, out int _);
            }
        }
        ManeuverNodeData maneuverNodeData = new ManeuverNodeData(activeVessel.SimulationObject.GlobalId, isManeuver, burnUT);
        if (maneuverNodeData.IsOnManeuverTrajectory)
            maneuverNodeData.SetManeuverState(patch as PatchedConicsOrbit);
        maneuverNodeData.BurnVector = burnVector;
        if (!Game.SpaceSimulation.Maneuvers.AddNodeToVessel(maneuverNodeData))
            return false;
        Game.Map.TryGetMapCore(out MapCore mapCore);
        if (mapCore != null)
        {
            try { mapCore.map3D.ManeuverManager.CreateGizmoForLocation(maneuverNodeData); }
            catch (Exception ex) { Logger.LogDebug($"UpdateNode: Suppressed Exception: {ex}"); }
            mapCore.map3D.ManeuverManager.HideManeuverPopup();
        }

        // For KSP2, We want to be able to start burns early to make them centered on the node
        double nodeTimeAdj = maneuverNodeData.BurnDuration * burnDurationOffsetFactor;

        // Update the node to put a gizmo on it
        // StartCoroutine(UpdateNode(maneuverNodeData, nodeTimeAdj));

        if ( nodeTimeAdj != 0 )
        {
            // var simObject = activeVessel?.SimulationObject;
            // ManeuverPlanComponent maneuverPlanComponent = simObject?.FindComponent<ManeuverPlanComponent>();
            try
            {
                Game.SpaceSimulation.Maneuvers.UpdateTimeOnNode(maneuverNodeData, maneuverNodeData.Time += nodeTimeAdj, mapCore.map3D.ManeuverManager._lookupGizmo.Values);
                Game.SpaceSimulation.Maneuvers.UpdateActiveNode();
                Game.SpaceSimulation.Maneuvers.UpdateAllChildNodeDetails(maneuverNodeData, mapCore.map3D.ManeuverManager._lookupGizmo.Values);
            }
            catch (Exception e) { Logger.LogError($"UpdateNode: Suppressed Exception: {e}"); }
        }

        // Refresh the node list
        RefreshManeuverNodes();

        return true;

        //IPatchedOrbit orbit = null;
        //if (Nodes.Count > 0)
        //{
        //    for (int i = 0; i < Nodes.Count - 1; i++)
        //    {
        //        if (burnUT > Nodes[i].Time && burnUT < Nodes[i + 1].Time)
        //        {
        //            orbit = Nodes[i + 1].ManeuverTrajectoryPatch;
        //            Logger.LogDebug($"CreateManeuverNodeAtUT: Attaching node to Node[{i + 1}]'s ManeuverTrajectoryPatch");
        //        }
        //    }
        //    if (orbit == null)
        //    {
        //        Logger.LogDebug($"CreateManeuverNodeAtUT: Attaching node to activeVessel.Orbit");
        //        orbit = activeVessel.Orbit;
        //    }
        //}
        //else
        //{
        //    Logger.LogDebug($"CreateManeuverNodeAtUT: Attaching node to activeVessel.Orbit");
        //    orbit = activeVessel.Orbit;
        //}

        //// IPatchedOrbit orbit = referencedOrbit;
        //// orbit.PatchStartTransition = PatchTransitionType.Maneuver;
        //// orbit.PatchEndTransition = PatchTransitionType.Final;
        ////Initial,
        ////Final,
        ////Encounter,
        ////Escape,
        ////Maneuver,
        ////Collision,
        ////EndThrust,
        ////PartialOutOfFuel,
        ////CompletelyOutOfFuel,

        //// Build the node data
        //ManeuverNodeData nodeData;
        //if (Nodes.Count == 0) // There are no nodes
        //{
        //    nodeData = new ManeuverNodeData(activeVessel.SimulationObject.GlobalId, false, burnUT);
        //    // orbit.PatchEndTransition = PatchTransitionType.Maneuver;
        //}
        //else
        //{
        //    if (burnUT < Nodes[0].Time) // request time is before the first node
        //    {
        //        nodeData = new ManeuverNodeData(activeVessel.SimulationObject.GlobalId, false, burnUT);
        //        orbit.PatchEndTransition = PatchTransitionType.Maneuver;
        //    }
        //    else if (burnUT > Nodes[Nodes.Count - 1].Time) // requested time is after the last node
        //    {
        //        nodeData = new ManeuverNodeData(activeVessel.SimulationObject.GlobalId, true, burnUT);
        //        orbit.PatchEndTransition = PatchTransitionType.Final;
        //    }
        //    else // request time is between existing nodes
        //    {
        //        nodeData = new ManeuverNodeData(activeVessel.SimulationObject.GlobalId, true, burnUT);
        //        orbit.PatchEndTransition = PatchTransitionType.Maneuver;
        //    }
        //    orbit.PatchStartTransition = PatchTransitionType.EndThrust;

        //    nodeData.SetManeuverState((PatchedConicsOrbit)orbit);
        //}

        //// orbit.PatchStartTransition = PatchTransitionType.EndThrust;

        //// nodeData.SetManeuverState((PatchedConicsOrbit)orbit);

        //nodeData.BurnVector = burnVector;

        //// Logger.LogDebug($"CreateManeuverNodeAtUT: BurnVector [{burnVector.x}, {burnVector.y}, {burnVector.z}] m/s");
        //// Logger.LogDebug($"CreateManeuverNodeAtUT: BurnDuration {nodeData.BurnDuration} s");
        //// Logger.LogDebug($"CreateManeuverNodeAtUT: Burn Time {nodeData.Time} s");

        //AddManeuverNode(nodeData, burnDurationOffsetFactor);

        //// Make sure this and any other nodes are OK
        //// StartCoroutine(RefreshNodes());

        //return true;
    }

    //private void AddManeuverNode(ManeuverNodeData nodeData, double burnDurationOffsetFactor)
    //{
    //    Logger.LogDebug("AddManeuverNode");

    //    // Add the node to the vessel's orbit. There are at least two ways to do this...
    //    bool useAddNode = false;
    //    if (useAddNode) // DONT USE THIS PATH!
    //    {
    //        // You can add nodes this way, but only so many before the game barfs (4 or 5)
    //        Logger.LogInfo("AddManeuverNode: Using AddNode method to create the node");
    //        ManeuverPlanComponent maneuverPlan;
    //        maneuverPlan = activeVessel.SimulationObject.ManeuverPlan;
    //        maneuverPlan.AddNode(nodeData, false); // false here because we're building a node, not rebuilding one
    //        activeVessel.Orbiter.ManeuverPlanSolver.UpdateManeuverTrajectory();
    //    }
    //    else
    //    {
    //        // You can reliably add as many nodes as you like this way
    //        // NOTE: AddNodeToVessel calls maneuverPlan.AddNode()
    //        Logger.LogInfo("AddManeuverNode: Using AddNodeToVessel method to create the node");
    //        Game.SpaceSimulation.Maneuvers.AddNodeToVessel(nodeData);
    //    }

    //    // For KSP2, We want the to start burns early to make them centered on the node
    //    double nodeTimeAdj = nodeData.BurnDuration * burnDurationOffsetFactor;

    //    // Update the node to put a gizmo on it
    //    StartCoroutine(UpdateNode(nodeData, nodeTimeAdj));

    //    // Refresh the node list
    //    RefreshManeuverNodes();

    //    //var PatchedConicsList = activeVessel?.Orbiter?.ManeuverPlanSolver?.PatchedConicsList;
    //    //var nextOrbit = PatchedConicsList[0];
    //    //Logger.LogDebug($"AddManeuverNode: Next Orbit: {nextOrbit}");

    //    // Logger.LogDebug("AddManeuverNode Done");
    //}

    // If we call Game.SpaceSimulation.Maneuvers.AddNodeToVessel
    // we get KSP.SimManeuver.ManeuverProvider.AddNodeToVessel
    //public bool AddNodeToVessel(ManeuverNodeData node)
    //{
    //    ManeuverPlanComponent component = this._game.UniverseModel.FindVesselComponent(node.RelatedSimID)?.SimulationObject.FindComponent<ManeuverPlanComponent>();
    //    if (component == null)
    //        return false;
    //    int num = component.AddNode(node, false) ? 1 : 0;
    //    if (num == 0)
    //        return num != 0;
    //    this.UpdateActiveNode();
    //    return num != 0;
    //}

    // If we call activeVessel.SimulationObject.ManeuverPlan.AddNode
    // we get: KSP.Sim.impl.ManeuverPlanComponent.AddNode:
    //public bool AddNode(ManeuverNodeData nodeData, bool rebuilding)
    //{
    //    bool flag = false;
    //    if (!this.TryGetNode(nodeData.NodeID, out ManeuverNodeData _))
    //    {
    //        Vector3d reducedBurnVectorChange = Vector3d.zero;
    //        if (rebuilding)
    //        {
    //            if (!this.EnoughDeltaVToAddNodeOnRebuild(nodeData, out reducedBurnVectorChange))
    //                return flag;
    //        }
    //        else if (!this.EnoughDeltaVToAddNode())
    //        {
    //            if (this.SimulationObject.VesselDeltaV.TotalDeltaVActual == 0.0 && this.IsEngineInAirbreathingMode(this.SimulationObject.VesselDeltaV))
    //                GameManager.Instance.Game.Messages.Publish<EngineInAirBreathingModeMessage>(GameManager.Instance.Game.Messages.CreateMessage<EngineInAirBreathingModeMessage>());
    //            else
    //                GameManager.Instance.Game.Messages.Publish<CannotPlaceManeuverNodeWhileOutOfFuelMessage>(GameManager.Instance.Game.Messages.CreateMessage<CannotPlaceManeuverNodeWhileOutOfFuelMessage>());
    //            return flag;
    //        }
    //        flag = true;
    //        this._currentNodes.Add(nodeData);
    //        this.UpdateNodeDetails(nodeData);
    //        if (reducedBurnVectorChange != Vector3d.zero)
    //            this.UpdateChangeOnNode(nodeData.NodeID, (Vector3)reducedBurnVectorChange);
    //        Action<System.Guid> maneuverNodeAdded = this.OnManeuverNodeAdded;
    //        if (maneuverNodeAdded != null)
    //            maneuverNodeAdded(nodeData.NodeID);
    //        this.CommitToState();
    //    }
    //    else
    //        GlobalLog.Error(LogFilter.Gameplay, (object)"Attempting to add node that already exists in _currentNodes");
    //    return flag;
    //}

    //private IEnumerator UpdateNode(ManeuverNodeData nodeData, double nodeTimeAdj)
    //{
    //    Logger.LogDebug("UpdateNode");

    //    if (nodeTimeAdj != 0)
    //    {
    //        var simObject = activeVessel?.SimulationObject;
    //        ManeuverPlanComponent maneuverPlanComponent = simObject?.FindComponent<ManeuverPlanComponent>();
    //        try { maneuverPlanComponent.UpdateTimeOnNode(nodeData, nodeData.Time += nodeTimeAdj); }
    //        catch (Exception e) { Logger.LogError($"UpdateNode: Suppressed Exception: {e}"); }
    //    }

    //    // Note from Untoldwind (5/22/23): As for the ManeuverPlanSolver: As far as I was able to a gather the actual solver runs
    //    // in a background thread using the "Unity.Jobs" system (i.e. outside the main game loop), so waiting for one-tick might
    //    // not be sufficient to get a result. I suppose you have to wait for "IPatchedOrbit.ActivePatch" to become true.

    //    while (!activeVessel.Orbit.ActivePatch)
    //        yield return new WaitForSeconds(0.1f); // WaitForFixedUpdate();

    //    bool mulligan = false;

    //    // GameManager.Instance.Game.Map.TryGetMapCore(out MapCore mapCore);
    //    Game.Map.TryGetMapCore(out MapCore mapCore);

    //    // Manage the maneuver on the map
    //    if (mapCore)
    //    {
    //        // This does it like it's done if the vessel just changed
    //        // mapCore.map3D.ManeuverManager.RemoveAll(); // Called right before GetNodeDataForVessels in OnVesselChanged
    //        // mapCore.map3D.ManeuverManager.GetNodeDataForVessels(); // calls GenerateGizmosForNodes, which calls CreateGizmoForLocation

    //        // This trys to do it like it's done if the player created the node manually
    //        // mapCore.map3D.ManeuverManager.CreateGizmoForLocation(nodeData);

    //        // This is the Mulligan way with an extra yield return if we hit a snag
    //        try { mapCore.map3D.ManeuverManager.CreateGizmoForLocation(nodeData); }
    //        catch (Exception e)
    //        {
    //            Logger.LogDebug($"UpdateNode: Suppressed Exception: {e}");
    //            Logger.LogDebug($"UpdateNode: Wating and Trying Again...");
    //            mulligan = true;
    //        }
    //        if (mulligan)
    //        {
    //            yield return new WaitForSeconds(0.1f); // WaitForFixedUpdate();
    //            mapCore.map3D.ManeuverManager.CreateGizmoForLocation(nodeData);
    //            //mapCore.map3D.ManeuverManager.GetNodeDataForVessels();
    //            //mapCore.map3D.ManeuverManager.UpdatePositionForGizmo(nodeData.NodeID);
    //        }

    //        // Call sequence when the player creates a node maually in the GUI
    //        //UnityEngine.EventSystems.EventSystem:Update()
    //        //ModifiedInputModule: Process()
    //        //ModifiedInputModule: ProcessMouseEvent()
    //        //ModifiedInputModule: ProcessMouseEvent(Int32)
    //        //ModifiedInputModule: ProcessMousePress(MouseButtonEventData, MouseButton)
    //        //UnityEngine.EventSystems.ExecuteEvents:Execute(GameObject, BaseEventData, EventFunction1)
    //        //UIAction_Void_Button: OnButtonLeftDown()
    //        //KSP.Api.CoreTypes.DelegateAction:Invoke(Object[])
    //        //KSP.Api.CoreTypes.DelegateAction:InternalInvoke(Boolean, Boolean, Object[])
    //        //System.Delegate:DynamicInvoke(Object[])
    //        //KSP.Map.Map3DManeuvers:OnAddManeuver()
    //        //KSP.Map.Map3DManeuvers:CreateGizmoForLocation(ManeuverNodeData)
    //        //KSP.Map.Map3DManeuvers:AcquireRawGizmoObject()
    //        //KSP.Map.MapManeuverGizmo:Configure(Camera)
    //        //KSP.Messages.MessageCenter:Publish(ManeuverCreatedMessage)
    //        //KSP.Messages.MessageCenter:Publish(Type, MessageCenterMessage)

    //    }

    //    yield return new WaitForFixedUpdate();
    //}

    public bool AddNode(double burnUT = 0)
    {
        RefreshActiveVesselAndCurrentManeuver();

        // Define empty node data
        // double UT = GameManager.Instance.Game.UniverseModel.UniversalTime;
        double UT = Game.UniverseModel.UniversalTime;
        if (burnUT < UT)
        {
            if (activeVessel.Orbit.eccentricity < 1 && Nodes.Count == 0)
            {
                burnUT = UT + activeVessel.Orbit.TimeToAp;
            }
            else
            {
                if (Nodes.Count > 0)
                    burnUT = Nodes[Nodes.Count - 1].Time + Math.Min(activeVessel.Orbit.period / 10, 600);
                else
                    burnUT = UT + 30;
            }
        }

        Vector3d burnVector;
        burnVector.x = 0;
        burnVector.y = 0;
        burnVector.z = 0;

        return CreateManeuverNodeAtUT(burnVector, burnUT, 0);
    }

    public IEnumerator RefreshNodes()
    {
        // Refresh the node list
        RefreshManeuverNodes();

        yield return (object)new WaitForFixedUpdate();

        if (activeVessel == null)
        {
            Logger.LogWarning("RefreshNodes: activeVessel is null. Unable to proceed.");
            yield return null;
        }

        var simObject = activeVessel?.SimulationObject;
        ManeuverPlanComponent maneuverPlanComponent = simObject?.FindComponent<ManeuverPlanComponent>();

        // yield return (object)new WaitForFixedUpdate();

        for (int i = 0; i < Nodes.Count; i++) // was i = SelectedNodeIndex
        {
            // Logger.LogDebug($"RefreshNodes: Updateing Node {i}");
            ManeuverNodeData node = Nodes[i];
            maneuverPlanComponent.UpdateNodeDetails(node);
        }

        for (int i = 0; i < Nodes.Count; i++) // was i = SelectedNodeIndex
        {
            // Logger.LogDebug($"RefreshNodes: Refreshing Node {i}");
            if (Nodes[i].ManeuverTrajectoryPatch != null)
            {
                try { maneuverPlanComponent.RefreshManeuverNodeState(i); }
                catch (NullReferenceException e)
                {
                    Logger.LogError($"RefreshNodes: Pass 1: Suppressed NRE for Node {i}: {e}");
                    SpitNode(i, true);
                }
            }
        }

        yield return (object)new WaitForFixedUpdate();

        for (int i = 0; i < Nodes.Count; i++) // was i = SelectedNodeIndex
        {
            // Logger.LogDebug($"RefreshNodes: Updateing Node {i}");
            ManeuverNodeData node = Nodes[i];
            maneuverPlanComponent.UpdateNodeDetails(node);
        }

        for (int i = 0; i < Nodes.Count; i++) // was i = SelectedNodeIndex
        {
            if (Nodes[i].ManeuverTrajectoryPatch != null)
            {
                // Logger.LogDebug($"RefreshNodes: Refreshing Node {i}");
                try { maneuverPlanComponent.RefreshManeuverNodeState(i); }
                catch (NullReferenceException e)
                {
                    Logger.LogError($"RefreshNodes: Pass 2: Suppressed NRE for Node {i}: {e}");
                    SpitNode(i, true);
                }
            }
        }
    }
}
