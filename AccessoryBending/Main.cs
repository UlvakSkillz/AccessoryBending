using HarmonyLib;
using Il2CppExitGames.Client.Photon;
using Il2CppPhoton.Pun;
using Il2CppPhoton.Realtime;
using Il2CppRUMBLE.Managers;
using Il2CppRUMBLE.Players;
using Il2CppRUMBLE.Utilities;
using MelonLoader;
using RumbleModdingAPI.RMAPI;
using System.Collections;
using System.Globalization;
using UIFramework;
using UnityEngine;

namespace AccessoryBending
{
    public static class BuildInfo
    {
        public const string ModName = "AccessoryBending";
        public const string ModVersion = "1.2.2";
        public const string Author = "UlvakSkillz";
    }

    public class AssetInfo
    {
        private GameObject assetToUse;
        private string boneToAttachTo;
        private Vector3 positionOffset;
        private Quaternion rotationOffset;
        private Vector3 localScale;
        private int layer;
        private string childPath;

        public AssetInfo(GameObject asset, string bone, Vector3 pOffset, Quaternion rOffset, Vector3 scale, int chosenLayer, string childsPath = "")
        {
            childPath = childsPath;
            assetToUse = asset;
            boneToAttachTo = bone;
            positionOffset = pOffset;
            rotationOffset = rOffset;
            localScale = scale;
            layer = chosenLayer;
        }

        public GameObject GetAssetToUse() { return assetToUse; }

        public string GetBoneToAttachTo() { return boneToAttachTo; }

        public Vector3 GetPositionOffset() { return positionOffset; }

        public Quaternion GetRotationOffset() { return rotationOffset; }

        public Vector3 GetLocalScale() { return localScale; }

        public int GetLayer() { return layer; }

        public string GetChildsPath() { return childPath; }

        //returns layer 0 so others can see it. get the layer itself if needed
        public string GetAssetInfo()
        {
            return $"{assetToUse.name}|" +
                $"{boneToAttachTo}|" +
                $"{positionOffset.x.ToString(CultureInfo.InvariantCulture)}:{positionOffset.y.ToString(CultureInfo.InvariantCulture)}:{positionOffset.z.ToString(CultureInfo.InvariantCulture)}|" +
                $"{rotationOffset.eulerAngles.x.ToString(CultureInfo.InvariantCulture)}:{rotationOffset.eulerAngles.y.ToString(CultureInfo.InvariantCulture)}:{rotationOffset.eulerAngles.z.ToString(CultureInfo.InvariantCulture)}|" +
                $"{localScale.x.ToString(CultureInfo.InvariantCulture)}:{localScale.y.ToString(CultureInfo.InvariantCulture)}:{localScale.z.ToString(CultureInfo.InvariantCulture)}|" +
                $"0|" + //layer so others can see
                $"{childPath}";
        }
    }

    public class Main : MelonMod
    {
        internal static List<AssetInfo> assetInfos = new List<AssetInfo>();
        private static byte myEventCode = 16;
        private static RaiseEventOptions eventOptions = new RaiseEventOptions() { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.AddToRoomCache };
        private GameObject parentObject;
        private static List<List<GameObject>> accessoriesToNuke = new List<List<GameObject>>();
        private static List<string> playersLoaded = new List<string>();
        private static Shader URPUnlit;

        private static void Log(string msg)
        {
            MelonLogger.Msg(msg);
        }

        public override void OnInitializeMelon()
        {
            URPUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            CheckFiles();
            Preferences.InitPrefs();
            UI.Register((MelonBase)this, Preferences.AccessoryBendingCategory, Preferences.AccessoriesCategory).OnModSaved += Save;
        }

        public override void OnLateInitializeMelon()
        {
            Actions.onMapInitialized += MapInit;
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            playersLoaded.Clear();
        }

        private void Save()
        {
            if (Preferences.PrefNukeOthers.Value)
            {
                Preferences.PrefNukeOthers.Value = false;
                NukeOthersAccessories();
            }
            Preferences.StoreLastSavedPrefs();
        }

        private void NukeOthersAccessories()
        {
            for (int i = 0; i < playersLoaded.Count; i++)
            {
                while (accessoriesToNuke[i].Count > 0)
                {
                    if (accessoriesToNuke[i][0] != null)
                    {
                        GameObject.Destroy(accessoriesToNuke[i][0]);
                    }
                    accessoriesToNuke[i].RemoveAt(0);
                }
                accessoriesToNuke[i].Clear();
            }
            accessoriesToNuke.Clear();
        }

        private void MapInit(string map)
        {
            PhotonNetwork.NetworkingClient.EventReceived += (Action<EventData>)OnEvent;
            if (map == "Gym")
            {
                CreateDressingRoomObjects();
            }
        }

        public void OnEvent(EventData eventData)
        {
            if (Preferences.PrefShowOthers.Value && (eventData.Code == myEventCode))
            {
                string recievedString = eventData.CustomData.ToString();
                string[] processedString = recievedString.Split(';');
                if (!playersLoaded.Contains(processedString[0]))
                {
                    Log($"Recieved Player Accessories:{Environment.NewLine}{recievedString}");
                    playersLoaded.Add(processedString[0]);
                    MelonCoroutines.Start(ProcessItems(processedString));
                }
            }
        }

        private IEnumerator ProcessItems(string[] processedString)
        {
            yield return new WaitForSeconds(1);
            for (int i = 1; i < PlayerManager.instance.AllPlayers.Count; i++)
            {
                if (PlayerManager.instance.AllPlayers[i].Data.GeneralData.PlayFabMasterId == processedString[0])
                {
                    PlayerController controller = PlayerManager.instance.AllPlayers[i].Controller;
                    foreach (string assetStringToLoad in processedString)
                    {
                        if (processedString[0] == assetStringToLoad)
                        {
                            Log("Processing For: " + assetStringToLoad + " " + controller.assignedPlayer.Data.GeneralData.PublicUsername);
                            accessoriesToNuke.Add(new List<GameObject>());
                            continue;
                        }
                        string[] assetInfo = assetStringToLoad.Split("|");
                        if (assetInfo[0].Length == 0)
                        {
                            continue;
                        }
                        bool assetFound = false;
                        for (int j = 0; j < assetInfos.Count; j++)
                        {
                            if (assetInfos[j].GetAssetToUse().name == assetInfo[0])
                            {
                                assetFound = true;
                                GameObject asset = assetInfos[j].GetAssetToUse();
                                string bone = assetInfo[1];
                                try
                                {
                                    string[] pOffsetString = assetInfo[2].Split(":");
                                    string[] rOffsetString = assetInfo[3].Split(":");
                                    string[] scaleString = assetInfo[4].Split(":");
                                    Vector3 pOffset = new Vector3(float.Parse(pOffsetString[0], CultureInfo.InvariantCulture), float.Parse(pOffsetString[1], CultureInfo.InvariantCulture), float.Parse(pOffsetString[2], CultureInfo.InvariantCulture));
                                    Quaternion rOffset = Quaternion.Euler(float.Parse(rOffsetString[0], CultureInfo.InvariantCulture), float.Parse(rOffsetString[1], CultureInfo.InvariantCulture), float.Parse(rOffsetString[2], CultureInfo.InvariantCulture));
                                    Vector3 scale = new Vector3(float.Parse(scaleString[0], CultureInfo.InvariantCulture), float.Parse(scaleString[1], CultureInfo.InvariantCulture), float.Parse(scaleString[2], CultureInfo.InvariantCulture));
                                    int layer = int.Parse(assetInfo[5]);
                                    string childsPath = (assetInfo.Length == 7) ? assetInfo[6] : "";
                                    AssetInfo newAsset = new AssetInfo(asset, bone, pOffset, rOffset, scale, 0, childsPath);
                                    PlaceAsset(controller, newAsset);
                                    string[] child = childsPath.Split("/");
                                    Log("Accessory" + assetInfo[0] + "Placed on " + child[child.Length - 1]);
                                }
                                catch (Exception e)
                                {
                                    MelonLogger.Error("ERROR READING INCOMING STRING FOR PLAYER: " + processedString[0]);
                                    MelonLogger.Error(e);
                                }
                                break;
                            }
                        }
                        if (!assetFound)
                        {
                            Log("Accessory Not Found: " + assetInfo[0]);
                        }
                    }
                    break;
                }
            }
        }

        private void CreateDressingRoomObjects()
        {
            for (int i = 0; i < assetInfos.Count; i++)
            {
                if (Preferences.PrefAccessoriesEnabled[i].Value)
                {
                    PlaceDressingRoomAsset(assetInfos[i]);
                }
            }
        }

        private void CheckFiles()
        {
            if (!Directory.Exists(@"UserData\AccessoryBending"))
            {
                Directory.CreateDirectory(@"UserData\AccessoryBending");
                return;
            }
            string[] files = Directory.GetFiles(@"UserData\AccessoryBending");
            if (files.Length == 0)
            {
                return;
            }
            List<string> assetFiles = new List<string>();
            parentObject = new GameObject();
            parentObject.name = "AssetBending Cosmetics";
            GameObject.DontDestroyOnLoad(parentObject);
            foreach (string file in files)
            {
                try
                {
                    if (!file.ToLower().EndsWith(".txt") && !file.ToLower().EndsWith(".cfg"))
                    {
                        assetFiles.Add(file);
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Error(e);
                }
            }
            foreach (string file in assetFiles)
            {
                if (!File.Exists(file + ".txt"))
                {
                    SaveFile($"Game Object Name{Environment.NewLine}Visuals/Skelington/Bone_Pelvis/Bone_Spine_A{Environment.NewLine}0|0|0{Environment.NewLine}0|0|0{Environment.NewLine}1|1|1{Environment.NewLine}true{Environment.NewLine}true{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}-------------------------------------------------------------------------------------------------------------------------------------------------{Environment.NewLine}{Environment.NewLine}Line 1: Name of Main Game Object (This is the Game Object Name Encompassing the Whole Cosmetic) (Found as Outter Most Object in Unity when Creating){Environment.NewLine}Line 2: The Bone Structure Path to the Spot you want it Parented (Cosmetic will Follow that Object).{Environment.NewLine}Line 3: Local Position Offset (0|0|0 will be in relation to the Parent's Rotation) (x, y, z){Environment.NewLine}Line 4: Local Rotation Offset (0|0|0 will be in relation to the Parent's Rotation) (x, y, z){Environment.NewLine}Line 5: Local Scale (1|1|1 will be the Normal Size) (x, y, z){Environment.NewLine}Line 6: Show in VR Headset (Set \"true\" or \"false\" to have the Cosmetic Visible or Not to the Headset){Environment.NewLine}Line 7: Show in Legacy Camera (Set \"true\" or \"false\" to have the Cosmetic Visible or Not to the Legacy Camera){Environment.NewLine}Line 8: If Something is in this Line, That Game Object will be Parented instead of the Line 1 Game Object. This should be the Line 1 Game Object's Child Path (not Including the Line 1 Game Object Name Itself){Environment.NewLine}", file + ".txt");
                }
                string[] fileText = File.ReadAllLines(file + ".txt");
                if (fileText.Length < 7)
                {
                    MelonLogger.Error("ASSET BUNDLE ERROR: " + file + ".txt HAS TOO FEW LINES!");
                    continue;
                }
                string name = fileText[0];
                string bone = fileText[1];
                string[] pOffset = fileText[2].Split("|");
                string[] rOffset = fileText[3].Split("|");
                string[] scale = fileText[4].Split("|");
                string showInHead = fileText[5];
                string showInLegacy = fileText[6];
                string childToMove = "";
                if (fileText.Length >= 8) { childToMove = fileText[7]; }
                if ((pOffset.Length < 3) || (rOffset.Length < 3) || (scale.Length < 3))
                {
                    MelonLogger.Error("ASSET BUNDLE ERROR: " + file + ".txt DOESNT HAVE ENOUGH DATA FOR POSITION ROTATION OR SCALE!");
                    continue;
                }
                float[][] assetValues = new float[3][];
                try
                {
                    assetValues[0] = new float[] { float.Parse(pOffset[0], CultureInfo.InvariantCulture), float.Parse(pOffset[1], CultureInfo.InvariantCulture), float.Parse(pOffset[2], CultureInfo.InvariantCulture) };
                    assetValues[1] = new float[] { float.Parse(rOffset[0], CultureInfo.InvariantCulture), float.Parse(rOffset[1], CultureInfo.InvariantCulture), float.Parse(rOffset[2], CultureInfo.InvariantCulture) };
                    assetValues[2] = new float[] { float.Parse(scale[0], CultureInfo.InvariantCulture), float.Parse(scale[1], CultureInfo.InvariantCulture), float.Parse(scale[2], CultureInfo.InvariantCulture) };
                }
                catch (Exception e)
                {
                    MelonLogger.Error("ASSET BUNDLE ERROR: " + file + ".txt DATA CONVERSION ERROR: POSITION ROTATION OR SCALE string -> float!");
                    MelonLogger.Error(e);
                    continue;
                }
                bool[] assetValuesBool = new bool[2];
                try
                {
                    assetValuesBool[0] = bool.Parse(showInHead);
                    assetValuesBool[1] = bool.Parse(showInLegacy);
                }
                catch (Exception e)
                {
                    MelonLogger.Error("ASSET BUNDLE ERROR: " + file + ".txt DATA CONVERSION ERROR: SHOW IN HEADSET SHOW IN LEGACY CAM string -> bool!");
                    MelonLogger.Error(e);
                    continue;
                }
                AssetInfo assetInfo;
                GameObject ddolAsset = SpawnDDOLAsset(file, name);
                ChangeShaderLitToUnlit(ddolAsset);
                if (ddolAsset != null)
                {
                    if (childToMove != "") { assetInfo = new AssetInfo(ddolAsset, bone, new Vector3(assetValues[0][0], assetValues[0][1], assetValues[0][2]), Quaternion.Euler(assetValues[1][0], assetValues[1][1], assetValues[1][2]), new Vector3(assetValues[2][0], assetValues[2][1], assetValues[2][2]), GetLayer(assetValuesBool[0], assetValuesBool[1]), childToMove); }
                    else { assetInfo = new AssetInfo(ddolAsset, bone, new Vector3(assetValues[0][0], assetValues[0][1], assetValues[0][2]), Quaternion.Euler(assetValues[1][0], assetValues[1][1], assetValues[1][2]), new Vector3(assetValues[2][0], assetValues[2][1], assetValues[2][2]), GetLayer(assetValuesBool[0], assetValuesBool[1])); }
                    assetInfos.Add(assetInfo);
                    Log($"Accessory Loaded: " + file + " | " + assetInfo.GetAssetToUse().name);
                }
                else
                {
                    if (ddolAsset != null)
                    {
                        GameObject.Destroy(ddolAsset);
                    }
                    MelonLogger.Error($"ASSET ERROR: {file} DOESNT HAVE ASSET: {name}");
                }
            }
        }

        private static void ChangeShaderLitToUnlit(GameObject asset)
        {
            Renderer parentRendderer = asset.GetComponent<Renderer>();
            if (parentRendderer != null)
            {
                for (int i = 0; i < parentRendderer.materials.Length; i++)
                {
                    if (parentRendderer.materials[i].shader.name == "Universal Render Pipeline/Lit")
                    {
                        parentRendderer.materials[i].shader = URPUnlit;
                    }
                }
            }
            foreach ( Renderer renderer in asset.GetComponentsInChildren<Renderer>())
            {
                for ( int i = 0; i < renderer.materials.Length;  i++ )
                {
                    if (renderer.materials[i].shader.name == "Universal Render Pipeline/Lit")
                    {
                        renderer.materials[i].shader = URPUnlit;
                    }
                }
            }
        }

        private int GetLayer(bool showInHead, bool showInLegacy)
        {
            if (showInHead && showInLegacy)
            {
                return 0;
            }
            else if (!showInHead && showInLegacy)
            {
                return 17;
            }
            else if (showInHead && !showInLegacy)
            {
                return 21;
            }
            else
            {
                return 18;
            }
        }

        public GameObject SpawnDDOLAsset(string assetBundlePath, string assetName)
        {
            GameObject asset = AssetBundles.LoadAssetFromFile<GameObject>(assetBundlePath, assetName);
            if (asset != null)
            {
                asset = GameObject.Instantiate(asset);
                GameObject.DontDestroyOnLoad(asset);
                asset.name = assetName.Replace("(Clone)", "");
                asset.SetActive(false);
                asset.transform.parent = parentObject.transform;
            }
            return asset;
        }

        private void SaveFile(string textToSave, string file)
        {
            FileStream fs = File.Create(file);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(textToSave);
            fs.Write(bytes);
            fs.Close();
            fs.Dispose();
        }

        [HarmonyPatch(typeof(PlayerController), "Initialize", new Type[] { typeof(Il2CppRUMBLE.Players.Player) })]
        public static class playerSpawn
        {
            private static void Postfix(ref PlayerController __instance, ref Il2CppRUMBLE.Players.Player player)
            {
                if (__instance.controllerType == ControllerType.Local)
                {
                    for (int i = 0; i < assetInfos.Count; i++)
                    {
                        if (Preferences.PrefAccessoriesEnabled[i].Value)
                        {
                            PlaceAsset(__instance, assetInfos[i]);
                        }
                    }
                }
                else
                {
                    SendRPCString();
                }
            }
        }

        [HarmonyPatch(typeof(Il2CppRUMBLE.Managers.PlayerManager), "RemovePlayer", new Type[] { typeof(Il2CppPhoton.Realtime.Player) })]
        public static class playerLeave
        {
            private static void Prefix(ref Il2CppPhoton.Realtime.Player player)
            {
                Il2CppRUMBLE.Players.Player otherPlayer = Calls.Players.GetPlayerByActorNo(player.ActorNumber);
                for(int i = 0; i < playersLoaded.Count; i++)
                {
                    if (playersLoaded[i] == otherPlayer.Data.GeneralData.PlayFabMasterId)
                    {
                        while (accessoriesToNuke[i].Count != 0)
                        {
                            if (accessoriesToNuke[i][0] != null)
                            {
                                Log("Removing Their Accessory: " + accessoriesToNuke[i][0].name);
                                GameObject.Destroy(accessoriesToNuke[i][0]);
                            }
                            accessoriesToNuke[i].RemoveAt(0);
                        }
                        playersLoaded.RemoveAt(i);
                        accessoriesToNuke.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private static void SendRPCString()
        {
            string assetString = PlayerManager.instance.localPlayer.Data.GeneralData.PlayFabMasterId + ";";
            for (int i = 0; i < assetInfos.Count; i++)
            {
                if (Preferences.PrefAccessoriesEnabled[i].Value)
                {
                    assetString += assetInfos[i].GetAssetInfo();
                    if (i != assetInfos.Count - 1)
                    {
                        assetString += ";";
                    }
                }
            }
            PhotonNetwork.RaiseEvent(myEventCode, assetString, eventOptions, SendOptions.SendReliable);
        }

        private static void PlaceAsset(PlayerController playerController, AssetInfo assetInfo)
        {
            int playerSpot = -1;
            for(int i = 0; i < playersLoaded.Count; i++)
            {
                if (playersLoaded[i] == playerController.assignedPlayer.Data.GeneralData.PlayFabMasterId)
                {
                    playerSpot = i;
                    break;
                }
            }
            if ((playerSpot == -1) && (playerController.controllerType == ControllerType.Remote))
            {
                MelonLogger.Error("REMOTE PLAYER NOT FOUND IN LIST!!!");
                return;
            }
            GameObject newAsset = GameObject.Instantiate(assetInfo.GetAssetToUse());
            newAsset.name = newAsset.name.Replace("(Clone)", "");
            newAsset.SetLayerRecursively(assetInfo.GetLayer());
            GameObject assetToMove = (assetInfo.GetChildsPath() != "") ? newAsset.transform.FindChild(assetInfo.GetChildsPath()).gameObject : assetToMove = newAsset;
            if (playerController.controllerType == ControllerType.Remote)
            {
                accessoriesToNuke[playerSpot].Add(newAsset);
                if (newAsset != assetToMove)
                {
                    accessoriesToNuke[playerSpot].Add(assetToMove);
                }
            }
            Transform bone = playerController.gameObject.transform.FindChild(assetInfo.GetBoneToAttachTo());
            newAsset.transform.position = bone.position;
            newAsset.transform.rotation = bone.rotation;
            assetToMove.transform.parent = bone;
            assetToMove.transform.localPosition = assetInfo.GetPositionOffset();
            assetToMove.transform.localRotation = assetInfo.GetRotationOffset();
            assetToMove.transform.localScale = assetInfo.GetLocalScale();
            newAsset.SetActive(true);
            assetToMove.SetActive(true);
        }

        private static void PlaceDressingRoomAsset(AssetInfo assetInfo)
        {
            GameObject newAsset = GameObject.Instantiate(assetInfo.GetAssetToUse());
            GameObject assetToMove = (assetInfo.GetChildsPath() != "") ? newAsset.transform.FindChild(assetInfo.GetChildsPath()).gameObject : assetToMove = newAsset;
            try
            {
                Transform bone = GameObjects.Gym.INTERACTABLES.DressingRoom.PreviewPlayerController.GetGameObject().transform.FindChild(assetInfo.GetBoneToAttachTo());
                assetToMove.transform.parent = bone;
                newAsset.transform.position = bone.position;
                newAsset.transform.rotation = bone.rotation;
                assetToMove.transform.localPosition = assetInfo.GetPositionOffset();
                assetToMove.transform.localRotation = assetInfo.GetRotationOffset();
                assetToMove.transform.localScale = assetInfo.GetLocalScale();
                newAsset.SetActive(true);
                assetToMove.SetActive(true);
            }
            catch
            {
                MelonLogger.Error($"({assetToMove.name}) DRESSING ROOM PATH NOT CORRECT! THIS IS MOST LIKELY DUE TO DIFFERENCES IN DRESSING ROOM PLAYER RIG AND NOT THE USER!");
                if (newAsset != null)
                {
                    GameObject.Destroy(newAsset);
                }
            }
        }
    }
}
