using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Partiality.Modloader;
using UnityEngine;

namespace LinkedStashChests
{
    public class LinkedStashChestsMod : PartialityMod
    {
        public LinkedStashChestsMod()
        {
            ModID = "LinkedStashChests";
            Version = "4";
            author = "Stian+Laymain";
        }

        public override void OnEnable()
        {
            base.OnEnable();
            On.SaveInstance.ApplyEnvironment += SaveInstance_ApplyEnvironment;
        }

        private static EnvironmentSave GetEnvironmentSave(SaveInstance save, string areaName)
        {
            var environmentSave = new EnvironmentSave { AreaName = areaName };
            if (!environmentSave.LoadFromFile(save.SavePath))
            {
                Debug.LogWarning($"Linked Stash Chests: Tried load non-existent area save '{areaName}'. Aborting.");
                return null;
            }

            return environmentSave;
        }

        private List<EnvironmentSave> AreasWithExistingStashChests(SaveInstance save, string excludedAreaName = null, bool excludeEmptyStashChests = true)
        {
            if (save == null)
            {
                Debug.LogWarning("Linked Stash Chests: Tried to stash chest area saves from non-existent save. Aborting.");
                return null;
            }

            var list = new List<EnvironmentSave>();
            foreach (string text in StashAreaToStashUID.Keys)
            {
                if (!string.Equals(text, excludedAreaName) && save.PathToSceneSaves.ContainsKey(text))
                {
                    EnvironmentSave environmentSave = GetEnvironmentSave(save, text);
                    if (excludeEmptyStashChests && GetSavedStashItems(environmentSave).Count == 0)
                    {
                        Debug.Log("Linked Stash Chests: Skipping empty stash chest in " + text);
                    }
                    else
                    {
                        list.Add(environmentSave);
                    }
                }
            }

            return list;
        }

        private BasicSaveData GetSavedStashChest(EnvironmentSave areaSave, string targetChestIdentifier = null)
        {
            if (areaSave == null)
            {
                Debug.LogWarning("Linked Stash Chests: Tried to get stash chest from non-existent area save. Aborting.");
                return null;
            }

            return GetSavedChest(StashAreaToStashUID[areaSave.AreaName], areaSave.ItemList, targetChestIdentifier);
        }

        private List<BasicSaveData> GetSavedStashItems(EnvironmentSave areaSave, string targetChestIdentifier = null)
        {
            if (areaSave == null)
            {
                Debug.LogWarning("Linked Stash Chests: Tried to get stash items from non-existent area save. Aborting.");
                return null;
            }

            return GetSavedChestItems(StashAreaToStashUID[areaSave.AreaName], areaSave.ItemList, targetChestIdentifier);
        }

        private void RemoveSavedStashItems(EnvironmentSave areaSave)
        {
            if (areaSave == null)
            {
                Debug.LogWarning("Linked Stash Chests: Tried to remove stash items from non-existent area save. Aborting.");
                return;
            }

            RemoveChestItems(StashAreaToStashUID[areaSave.AreaName], areaSave.ItemList);
        }

        private BasicSaveData GetSavedChest(string chestIdentifier, List<BasicSaveData> itemList, string targetChestIdentifier = null)
        {
            string chestSignature = "<UID>" + chestIdentifier + "</UID>";
            BasicSaveData basicSaveData = itemList.Find(saveData => saveData.SyncData.Contains(chestSignature));
            if (targetChestIdentifier != null)
            {
                basicSaveData = ConvertedChest(basicSaveData, targetChestIdentifier);
            }

            return basicSaveData;
        }

        private List<BasicSaveData> GetSavedChestItems(string chestIdentifier, List<BasicSaveData> itemList, string targetChestIdentifier = null)
        {
            string sourceChestContentSignature = "<Hierarchy>1" + chestIdentifier + ";1000000</Hierarchy>";
            List<BasicSaveData> list = itemList.FindAll(saveData => saveData.SyncData.Contains(sourceChestContentSignature));
            if (targetChestIdentifier != null)
            {
                list = ConvertedChestItems(chestIdentifier, list, targetChestIdentifier);
            }

            return list;
        }

        private BasicSaveData ConvertedChest(BasicSaveData sourceChest, string targetChestIdentifier)
        {
            if (string.Equals((string)sourceChest.Identifier, targetChestIdentifier))
            {
                return sourceChest;
            }

            string oldValue = "<UID>" + sourceChest.Identifier + "</UID>";
            string newValue = "<UID>" + targetChestIdentifier + "</UID>";
            return new BasicSaveData(targetChestIdentifier, sourceChest.SyncData.Replace(oldValue, newValue));
        }

        private static List<BasicSaveData> ConvertedChestItems(string sourceChestIdentifier, List<BasicSaveData> sourceChestItems, string targetChestIdentifier)
        {
            if (string.Equals(sourceChestIdentifier, targetChestIdentifier))
            {
                return sourceChestItems;
            }

            string sourceChestContentSignature = "<Hierarchy>1" + sourceChestIdentifier + ";1000000</Hierarchy>";
            string targetChestContentSignature = "<Hierarchy>1" + targetChestIdentifier + ";1000000</Hierarchy>";
            return sourceChestItems.ConvertAll(saveData =>
                new BasicSaveData((string)saveData.Identifier, saveData.SyncData.Replace(sourceChestContentSignature, targetChestContentSignature)));
        }

        private static void RemoveChestItems(string chestIdentifier, List<BasicSaveData> itemList)
        {
            string targetChestContentSignature = "<Hierarchy>1" + chestIdentifier + ";1000000</Hierarchy>";
            itemList.RemoveAll(saveData => saveData.SyncData.Contains(targetChestContentSignature));
        }

        private static void AddSavedStashSilver(int addedSilver, BasicSaveData chest)
        {
            int silver = GetSavedStashSilver(chest) + addedSilver;
            SetSavedStashSilver(silver, chest);
        }

        private static int GetSavedStashSilver(BasicSaveData chest)
        {
            int num = chest.SyncData.IndexOf("TreasureChestContainedSilver/") + "TreasureChestContainedSilver/".Length;
            int num2 = chest.SyncData.IndexOf(";TreasureChestGenCont/");
            return int.Parse(chest.SyncData.Substring(num, num2 - num));
        }

        private static void SetSavedStashSilver(int silver, BasicSaveData chest)
        {
            int length = chest.SyncData.IndexOf("TreasureChestContainedSilver/") + "TreasureChestContainedSilver/".Length;
            int startIndex = chest.SyncData.IndexOf(";TreasureChestGenCont/");
            object arg = chest.SyncData.Substring(0, length);
            string arg2 = chest.SyncData.Substring(startIndex);
            string syncData = arg.ToString() + silver + arg2;
            chest.SyncData = syncData;
        }

        private bool SaveInstance_ApplyEnvironment(On.SaveInstance.orig_ApplyEnvironment orig, SaveInstance self)
        {
            bool result = orig.Invoke(self);
            EnvironmentSave loadedScene = GetPrivatePart<EnvironmentSave, SaveInstance>(self, "m_loadedScene");
            if (loadedScene == null || !StashAreaToStashUID.ContainsKey(loadedScene.AreaName))
            {
                return result;
            }

            string areaName = loadedScene.AreaName;
            List<EnvironmentSave> otherAreaSaves = AreasWithExistingStashChests(self, areaName);
            if (otherAreaSaves.Count == 0)
            {
                Debug.Log("Linked Stash Chests: No other stash chests to sync with.");
                return result;
            }

            var treasureChest = (TreasureChest)ItemManager.Instance.GetItem(StashAreaToStashUID[areaName]);
            if (treasureChest == null)
            {
                Debug.LogWarning("Linked Stash Chests: Could not get loaded stash chest in " + areaName + ". Aborting.");
                return result;
            }

            var bufferedLog = new StringBuilder();
            try
            {
                BasicSaveData basicSaveData = null;
                var currentStashItems = new List<BasicSaveData>();
                if (self.PathToSceneSaves.ContainsKey(areaName))
                {
                    basicSaveData = GetSavedStashChest(loadedScene);
                    currentStashItems = GetSavedStashItems(loadedScene);
                }

                if (basicSaveData == null)
                {
                    basicSaveData = new BasicSaveData(treasureChest.UID, treasureChest.ToSaveData());
                }

                bufferedLog.AppendLine("--------------------------------- Linked Stash Chests ---------------------------------");
                bufferedLog.AppendLine($"{areaName}'s stash chest BEFORE pulling all other stash chest items into it:");
                bufferedLog.AppendLine($"{currentStashItems.Count} items: {basicSaveData.Identifier} {basicSaveData.SyncData}");
                foreach (EnvironmentSave otherAreaSave in otherAreaSaves)
                {
                    BasicSaveData otherStashChest = GetSavedStashChest(otherAreaSave);
                    List<BasicSaveData> otherStashItems = GetSavedStashItems(otherAreaSave, StashAreaToStashUID[areaName]);
                    bufferedLog.AppendLine();
                    bufferedLog.AppendLine($"{otherAreaSave.AreaName}'s SAVED stash chest BEFORE pulling all its items into {areaName}'s stash chest:");
                    bufferedLog.AppendLine($"{otherStashItems.Count} items: {(otherStashChest == null ? "null" : otherStashChest.Identifier + " " + otherStashChest.SyncData)}");

                    RemoveSavedStashItems(otherAreaSave);
                    ItemManager.Instance.LoadItems(otherStashItems);
                    int savedStashSilver = GetSavedStashSilver(otherStashChest);
                    SetSavedStashSilver(0, otherStashChest);
                    AddSavedStashSilver(savedStashSilver, basicSaveData);
                    ItemManager.Instance.LoadItems(new List<BasicSaveData>(1) { basicSaveData });
                    currentStashItems.AddRange(otherStashItems);
                    otherAreaSave.ProcessSave();

                    otherStashItems = GetSavedStashItems(otherAreaSave, StashAreaToStashUID[areaName]);
                    bufferedLog.AppendLine($"{otherAreaSave.AreaName}'s SAVED stash chest AFTER pulling all its items into {areaName}'s stash chest:");
                    bufferedLog.AppendLine($"{otherStashItems.Count} items): {(otherStashChest == null ? "null" : otherStashChest.Identifier + " " + otherStashChest.SyncData)}");
                }

                bufferedLog.AppendLine();
                bufferedLog.AppendLine($"{areaName}'s stash chest AFTER pulling all other stash chest items into it:");
                bufferedLog.AppendLine($"{currentStashItems.Count} items: {basicSaveData.Identifier + " " + basicSaveData.SyncData}");
                bufferedLog.AppendLine("---------------------------------------------------------------------------------------");
            }
            finally
            {
                Debug.Log(bufferedLog.ToString());
            }

            return result;
        }

        private static T GetPrivatePart<T, S>(S owner, string memberName) where T : class where S : class
        {
            FieldInfo field = typeof(S).GetField(memberName, BindingFlags.Instance | BindingFlags.NonPublic);
            return (field != null ? field.GetValue(owner) : null) as T;
        }

        public readonly Dictionary<string, string> StashAreaToStashUID = new Dictionary<string, string>
        {
            {
                "Berg",
                "ImqRiGAT80aE2WtUHfdcMw"
            },
            {
                "CierzoNewTerrain",
                "ImqRiGAT80aE2WtUHfdcMw"
            },
            {
                "Levant",
                "ZbPXNsPvlUeQVJRks3zBzg"
            },
            {
                "Monsoon",
                "ImqRiGAT80aE2WtUHfdcMw"
            },
            {
                "Harmattan",
                "ImqRiGAT80aE2WtUHfdcMw"
            }
        };
    }
}
