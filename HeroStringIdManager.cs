using Fasterflect;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using System;
using System.Linq;

namespace DistinguishedService
{
    //quick util functions
    public class Utils
    {
        public static int MBStringIdToInt(string stringId)
        {
            int result;
            return int.TryParse(new string(stringId.Trim().SkipWhile<char>((Func<char, bool>)(c => !"0123456789".Contains<char>(c))).ToArray<char>()), out result) ? result : -1;
        }

        public static string MBStringIdExtractCharString(string stringId)
        {
            return new string(stringId.Trim().SkipWhile<char>((Func<char, bool>)(c => "0123456789".Contains<char>(c))).ToArray<char>());
        }
    }

    public class HeroStringIdManager
    {
        private static Dictionary<string, CharacterObject> _characterObjectStringIdDict;
        private static HeroStringIdManager _instance;

        public static Dictionary<string, CharacterObject> CharacterObjectStringIdDict
        {
            get
            {
                if (HeroStringIdManager._characterObjectStringIdDict == null)
                    HeroStringIdManager._characterObjectStringIdDict = new Dictionary<string, CharacterObject>();
                return HeroStringIdManager._characterObjectStringIdDict;
            }
            private set
            {
                HeroStringIdManager._characterObjectStringIdDict = value;
            }
        }

        public static HeroStringIdManager Instance
        {
            get
            {
                if (HeroStringIdManager._instance == null)
                    HeroStringIdManager._instance = new HeroStringIdManager();
                return HeroStringIdManager._instance;
            }
            private set
            {
                HeroStringIdManager._instance = value;
            }
        }

        private HeroStringIdManager()
        {
            if (HeroStringIdManager._characterObjectStringIdDict != null)
                return;
            HeroStringIdManager._characterObjectStringIdDict = new Dictionary<string, CharacterObject>();
        }

        public static int SyncMBCharacterStringIdToHeroStringIdManager()
        {
            Dictionary<string, CharacterObject> objectStringIdDict = HeroStringIdManager.CharacterObjectStringIdDict;
            int num = 0;
            foreach (CharacterObject characterObject in CharacterObject.All)
            {
                if (characterObject.StringId.Contains("CharacterObject"))
                {
                    if (objectStringIdDict.ContainsKey(characterObject.StringId))
                        ++num;
                    else
                        objectStringIdDict.Add(characterObject.StringId, characterObject);
                }
            }
            return num;
        }

        public static int GetMBCharacterStringIdNumberSum()
        {
            return HeroStringIdManager.CharacterObjectStringIdDict.Count;
        }

        public static bool IsIdDuplicate(CharacterObject charaObj)
        {
            if (HeroStringIdManager.GetMBCharacterStringIdNumberSum() == 0)
                HeroStringIdManager.SyncMBCharacterStringIdToHeroStringIdManager();
            return !HeroStringIdManager.CharacterObjectStringIdDict.ContainsKey(charaObj.StringId);
        }

        public static bool AddCharacterStringIdToIdManager(CharacterObject charaObj)
        {
            if (!charaObj.StringId.Contains("CharacterObject") || HeroStringIdManager.CharacterObjectStringIdDict.ContainsKey(charaObj.StringId))
                return false;
            HeroStringIdManager.CharacterObjectStringIdDict.Add(charaObj.StringId, charaObj);
            return true;
        }

        public static int GetMaxStringIdAsNumOfCurrentManagerRecord()
        {
            if (HeroStringIdManager.GetMBCharacterStringIdNumberSum() == 0)
                HeroStringIdManager.SyncMBCharacterStringIdToHeroStringIdManager();
            int num1 = -1;
            foreach (KeyValuePair<string, CharacterObject> keyValuePair in HeroStringIdManager.CharacterObjectStringIdDict)
            {
                int num2 = Utils.MBStringIdToInt(keyValuePair.Key);
                if (num2 > num1)
                    num1 = num2;
            }
            return num1;
        }

        public static string GenerateNonDuplicateStringId()
        {
            string str = "TaleWorlds.CampaignSystem.CharacterObject";
            if (HeroStringIdManager.GetMBCharacterStringIdNumberSum() == 0)
                HeroStringIdManager.SyncMBCharacterStringIdToHeroStringIdManager();
            int num = HeroStringIdManager.GetMaxStringIdAsNumOfCurrentManagerRecord() + 1;
            return str + num.ToString();
        }

        public static int GenerateNonDuplicateStringIdNum()
        {
            if (HeroStringIdManager.GetMBCharacterStringIdNumberSum() == 0)
                HeroStringIdManager.SyncMBCharacterStringIdToHeroStringIdManager();
            return HeroStringIdManager.GetMaxStringIdAsNumOfCurrentManagerRecord() + 1;
        }

        public static void LogAllStringIdofManager()
        {
            if (HeroStringIdManager.GetMBCharacterStringIdNumberSum() != 0)
                return;
            HeroStringIdManager.SyncMBCharacterStringIdToHeroStringIdManager();
        }

        public static bool ChangeCharacterStringIdToNonDuplicate(
          CharacterObject charaObj,
          out string newId)
        {
            string duplicateStringId = HeroStringIdManager.GenerateNonDuplicateStringId();
            charaObj.TrySetFieldValue("_originCharacterStringId", (object)duplicateStringId);
            charaObj.TrySetFieldValue("StringId", (object)duplicateStringId);
            if ((string)charaObj.TryGetFieldValue("_originCharacterStringId") == duplicateStringId && (string)charaObj.TryGetFieldValue("StringId") == duplicateStringId)
            {
                newId = duplicateStringId;
                return true;
            }
            newId = "";
            return false;
        }
    }
}
