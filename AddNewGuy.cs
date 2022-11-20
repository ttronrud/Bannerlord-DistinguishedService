//TODO
//
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.Actions;
using Fasterflect;
using TaleWorlds.ObjectSystem;
using TaleWorlds.TwoDimension;
using System.IO;
using System.Xml.Serialization;
using System.Reflection;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Roster;

//Changes since last upload:
/*
 * - Add option for random skill selection to speed up companion creation
 * - fixed "previously a ..." bug during skill selection
 * - adjustable number of skill selection rounds
 * - Add companion join party dialogue to all clan-led parties
 * - attempted to remove harmony file logging
 */

//TODO
/* 
 * 
 * - companions can have disagreements? Personality?
 *  try with HeroHelper.TraitHarmony()? NPCPersonalityClashWithNPC()?
 */

namespace DistinguishedService
{
    public class AddNewGuy
    {
        Random rand;
        public int tier_threshold { get; set; }
        //private double nomination_chance { get; set; }
        private int base_additional_skill_points { get; set; }
        private int leadership_points_per_50_extra_skill_points { get; set; }
        //public float combat_perf_nomination_chance_increase_per_kill { get; set; }
        public int battle_size_scale { get; set; }
        public int inf_kill_threshold { get; set; }
        public int cav_kill_threshold { get; set; }
        public int ran_kill_threshold { get; set; }
        public float outperform_percentile { get; set; }
        public int up_front_cost { get; set; }
        public int max_nominations { get; set; }
        public int mod_nominations { get; set; }
        public bool fill_perks { get; set; }
        public float kill_chance { get; set; }
        public float medicine_reduce { get; set; }
        public float companion_lethality { get; set; }

        public int skp_per_excess_kill { get; set; }

        public float ai_promotion_chance { get; set; }
        public bool ai_companion_death { get; set; }

        //clan companion limits
        public bool respect_companion_limit { get; set; }
        public int bonus_companion_slots_base { get; set; }
        public int bonus_companion_slots_per_clan_tier { get; set; }
        public int num_skill_bonuses { get; set; }
        public bool select_skills_randomly { get; set; }
        public int num_skill_rounds { get; set; }
        public int max_ai_comp_per_party { get; set; }
        public bool remove_tavern_companions { get; set; }
        /// ///
        public bool ignore_cautions { get; set; }

        public int begin_battle_size;
        public List<CharacterObject> nominations;
        public List<int> killcounts;
        public List<Hero> tocullonendmapevent;

        public bool using_extern_namelist { get; set; }
        public string extern_namelist { get; set; }

        public static AddNewGuy __instance = null;
        public static bool MyLittleWarbandLoaded = false;

        public AddNewGuy()
        {
            string path = Path.Combine(BasePath.Name, "Modules", "DistinguishedService", "Settings.xml");
            Settings currentsettings;
            using (Stream stream = (Stream)new FileStream(path, FileMode.Open))
                currentsettings = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream);

            rand = new Random();
            nominations = new List<CharacterObject>();
            killcounts = new List<int>();
            this.begin_battle_size = 0;
            this.ai_promotion_chance = currentsettings.ai_promotion_chance;
            this.ai_companion_death = currentsettings.cull_ai_companions_on_defeat;
            //this.combat_perf_nomination_chance_increase_per_kill = (float)currentsettings.combat_perf_nomination_chance_increase_per_kill;
            this.tier_threshold = currentsettings.tier_threshold;
            this.max_nominations = currentsettings.max_nominations;

            this.inf_kill_threshold = currentsettings.inf_kill_threshold;
            this.cav_kill_threshold = currentsettings.cav_kill_threshold;
            this.ran_kill_threshold = currentsettings.ran_kill_threshold;
            this.outperform_percentile = currentsettings.outperform_percentile;
            this.skp_per_excess_kill = currentsettings.skillpoints_per_excess_kill;

            this.up_front_cost = currentsettings.up_front_cost;
            this.fill_perks = currentsettings.fill_in_perks;

            this.respect_companion_limit = currentsettings.respect_companion_limit;
            this.bonus_companion_slots_base = currentsettings.bonus_companion_slots_base;
            this.bonus_companion_slots_per_clan_tier = currentsettings.bonus_companion_slots_per_clan_tier;

            this.companion_lethality = currentsettings.companion_extra_lethality;

            //this.nomination_chance = currentsettings.nomination_chance;
            this.base_additional_skill_points = currentsettings.base_additional_skill_points;
            this.leadership_points_per_50_extra_skill_points = currentsettings.leadership_points_per_50_extra_skill_points;
            this.num_skill_bonuses = currentsettings.number_of_skill_bonuses;
            this.num_skill_rounds = currentsettings.number_of_skill_rounds;
            this.select_skills_randomly = currentsettings.select_skills_randomly;
            this.max_ai_comp_per_party = currentsettings.max_ai_companions_per_party;
            this.remove_tavern_companions = currentsettings.remove_tavern_companions;
            this.ignore_cautions = currentsettings.disable_caution_text;
            this.using_extern_namelist = currentsettings.NAMES_FROM_EXTERNAL_FILE;
            this.extern_namelist = System.IO.Path.Combine(BasePath.Name, "Modules", "DistinguishedService", currentsettings.EXTERNAL_NAME_FILE);
            if (this.using_extern_namelist)
            {
                InformationManager.DisplayMessage(new InformationMessage("USING EXTERNAL NAMELIST FILE!\nThis file will be written back to/edited to knock out used names", Color.FromUint(4282569842U)));
            }

            //InformationManager.DisplayMessage(new InformationMessage("max nominations = " + this.max_nominations + "\ntier thresh = " + this.tier_threshold + "\nnom chance = " + this.nomination_chance + "\nKill thresholds:\nInf = " + this.inf_kill_threshold + ", cav = " + this.cav_kill_threshold + ", ranged = " + this.ran_kill_threshold + "\nadd skp = " + this.base_additional_skill_points + "\nld points = " + this.leadership_points_per_50_extra_skill_points + "\nBonus nomination chance per kill = " + this.combat_perf_nomination_chance_increase_per_kill + "\nBattle size scale = " + this.battle_size_scale, Color.FromUint(4282569842U)));
            InformationManager.DisplayMessage(new InformationMessage("Max nominations = " + this.max_nominations + "\nTier Thresh = " + this.tier_threshold + "\nKill Thresh:\nInf = " + this.inf_kill_threshold + " cav = " + this.cav_kill_threshold + " ran = " + this.ran_kill_threshold + "\nPerformance Thresh = " + this.outperform_percentile, Color.FromUint(4282569842U)));
            AddNewGuy.__instance = this;
            if (currentsettings.upgrade_to_hero)
            {
                InformationManager.DisplayMessage(new InformationMessage("CAUTION: Troops will auto-promote to heros at tier " + this.tier_threshold + "\nChange settings if this is unintended.", Colors.Yellow));
            }
            if (this.fill_perks)
            {
                InformationManager.DisplayMessage(new InformationMessage("CAUTION: New hero perks will be assigned automatically.\nChange settings if this is unintended.", Colors.Yellow));
            }
            if (this.companion_lethality > 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("WARNING: Non-zero additional chance of companion death.\nChange settings if this was unintended.", Colors.Red));
            }
            tocullonendmapevent = new List<Hero>();

        }


        public void OnHeroWounded(Hero _h)
        {
            //yeetus abortus out of the method if this isn't a valid hero
            if (_h.Clan == null || _h.CharacterObject.Occupation != Occupation.Wanderer)
            {
                return;
            }

            Random r = new Random();

            //player companion tree
            if (_h.Clan == Clan.PlayerClan)
            {
                if (r.NextDouble() > this.companion_lethality) //doesn't trigger
                {
                    return;
                }

                if (_h.CharacterObject.Occupation == Occupation.Wanderer)
                {
                    KillCharacterAction.ApplyByBattle(_h, null, true);
                }
            }

        }

        //run before a hero's killed
        public void PreHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification = true)
        {
            //victim's a player companion who's leading their own party
            if(victim.IsPlayerCompanion && victim.IsPartyLeader)
            {
                DisbandPartyAction.StartDisband(victim.PartyBelongedTo);
            }
        }

        //on save check through all heros, find player companions, and change their occupation back to "Wanderer"
        public void EnsureWanderers(CampaignGameStarter gcs)
        {
            foreach(Hero h in Hero.AllAliveHeroes)
            {
                //if (h.IsPlayerCompanion && !h.IsWanderer)
                if (h.CompanionOf != null && !h.IsWanderer)
                {
                    h.CharacterObject.TrySetPropertyValue("Occupation", Occupation.Wanderer);
                }
            }
        }


        public void MapEventEnded(MapEvent me)
        {
            //while this kinda feels like cheating, it's in C# so it's not like
            //performance is the goal anyway
            try
            {
                //only care about decisive field battles
                if (!(me.HasWinner))
                    return;

                Random r = new Random();

                //look at winning side
                foreach (MapEventParty p in me.PartiesOnSide(me.WinningSide))
                {
                    //ignore player-led or player-clan parties
                    if (p == null || p.Party == PartyBase.MainParty || p.Party.LeaderHero?.Clan == Clan.PlayerClan)
                    {
                        continue;
                    }
                    if (p.Party.LeaderHero != null) //needs a leader
                    {
                        //check probability
                        if (r.NextDouble() > this.ai_promotion_chance)
                            continue;

                        //select troop to promote
                        List<CharacterObject> _cos_ = p.Troops.Troops.ToList();
                        if (_cos_ == null)
                        {
                            continue;
                        }
                        this.Shuffle(_cos_);
                        List<CharacterObject> qualified = new List<CharacterObject>();
                        int num_comps_in_party = 0;
                        foreach (CharacterObject _co_ in _cos_)
                        {
                            if (_co_.IsHero && _co_.HeroObject.Occupation == Occupation.Wanderer)
                            {
                                num_comps_in_party++;
                            }
                            //find first qualifying CharacterObject
                            if (!(_co_ == null) && !_co_.IsHero && _co_.IsSoldier && AddNewGuy.is_soldier_qualified(_co_))
                            {
                                qualified.Add(_co_);

                            }
                        }
                        if (num_comps_in_party < max_ai_comp_per_party && qualified.Count > 0)
                        {
                            this.AddNewGuyToParty(qualified[0], p.Party.MobileParty);
                        }
                    }
                }

                //if we're not culling the ai-promoted heros on defeat
                if (!ai_companion_death)
                    return;
                //run a check on the losing side to kill AI-promoted companions
                foreach (MapEventParty p in me.PartiesOnSide(me.DefeatedSide))
                {
                    //ignore player-led or player-clan parties
                    if (p == null || p.Party == PartyBase.MainParty || p.Party.LeaderHero?.Clan == Clan.PlayerClan)
                    {
                        continue;
                    }
                    List<CharacterObject> _cos_ = p.Troops.Troops.ToList();

                    foreach (CharacterObject _co_ in _cos_)
                    {
                        //look through and gank the losing AI-promoted heros
                        if ((_co_ != null) && _co_.IsHero && _co_.Occupation == Occupation.Wanderer && _co_.HeroObject.Clan != Clan.PlayerClan)
                        {
                            KillCharacterAction.ApplyByBattle(_co_.HeroObject, null, true);
                        }
                    }
                }
                //run a check on the losing side to kill AI-promoted companions
                foreach (MapEventParty p in me.PartiesOnSide(me.WinningSide))
                {
                    //ignore player-led or player-clan parties
                    if (p == null || p.Party == PartyBase.MainParty || p.Party.LeaderHero?.Clan == Clan.PlayerClan)
                    {
                        continue;
                    }
                    //scan through prisoners to delete captured AI companions
                    List<CharacterObject> _cos_ = p.Party.PrisonRoster.ToFlattenedRoster().Troops.ToList();

                    foreach (CharacterObject _co_ in _cos_)
                    {
                        //look through and gank the losing AI-promoted heros
                        if ((_co_ != null) && _co_.IsHero && _co_.Occupation == Occupation.Wanderer && _co_.HeroObject.Clan != Clan.PlayerClan)
                        {
                            KillCharacterAction.ApplyByBattle(_co_.HeroObject, null, true);
                        }
                    }
                }
            }
            catch
            {
                //nop lel
            }
        }

        public void OnPCBattleEnded_results()
        {
            //check if we care about the companion limit, and if there's room
            if (this.respect_companion_limit && Clan.PlayerClan.Companions.Count >= Clan.PlayerClan.CompanionLimit)
            {
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("At maximum allowed companions. No nominations possible.").ToString(), Colors.Blue));
                return;
            }
            List<CharacterObject> _stripped_noms = new List<CharacterObject>();
            List<int> _stripped_kcs = new List<int>();
            //bonus from nomination list
            if (nominations.Count > 0 && killcounts.Count > 0)
            {
                for (int i = 0; i < nominations.Count; i++)
                {
                    if (MobileParty.MainParty.MemberRoster.Contains(nominations[i]) && nominations[i] != null && nominations[i].HitPoints > 0)
                    {
                        _stripped_noms.Add(nominations[i]);
                        _stripped_kcs.Add(killcounts[i]);
                    }
                }
            }

            List<CharacterObject> coList;
            double num = rand.NextDouble();
            //instead, if there's valid nominations
            if (_stripped_noms.Count > 0)//num <= this.nomination_chance * (double)rk + (double)nomlist)
            {
                //nominate the troops!
                coList = new List<CharacterObject>(_stripped_noms).OrderBy<CharacterObject, int>(o => _stripped_kcs[_stripped_noms.IndexOf(o)]).Reverse().ToList();
                _stripped_kcs = new List<int>(_stripped_kcs).OrderBy<int, int>(o => _stripped_kcs[_stripped_kcs.IndexOf(o)]).Reverse().ToList();

                //check if number of possible nominations would put us over the companion limit
                if (this.respect_companion_limit && (coList.Count + Clan.PlayerClan.Companions.Count) > Clan.PlayerClan.CompanionLimit)
                {
                    this.mod_nominations = Clan.PlayerClan.CompanionLimit - Clan.PlayerClan.Companions.Count;
                }
                else
                {
                    this.mod_nominations = this.max_nominations;
                }
                //
                //InformationManager.
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Distinguished Soldiers", "Several soldiers made names for themselves in this battle. You can choose up to " + this.mod_nominations + " (or none, by exiting) to fight at your side as a companion.", this.gen_inquiryelements(coList, _stripped_kcs), true, this.mod_nominations, "DONE", "RANDOM", new Action<List<InquiryElement>>(this.OnNomineeSelect), (Action<List<InquiryElement>>)null, ""), true);
                return;

            }

        }
        public List<InquiryElement> gen_inquiryelements(List<CharacterObject> _cos, List<int> _kills)
        {
            List<InquiryElement> _ies = new List<InquiryElement>();
            for(int q = 0; q < _cos.Count; q++)
            {
                if (MobileParty.MainParty.MemberRoster.Contains(_cos[q]))
                {
                    _ies.Add(new InquiryElement((object)_cos[q], _cos[q].Name.ToString(), new ImageIdentifier(CharacterCode.CreateFrom((BasicCharacterObject)_cos[q])), true, _kills[q].ToString() + " kills"));
                }
            }

            return _ies;

        }
        public static bool is_soldier_qualified(CharacterObject co)
        {
            if(co == null)
            {
                return false;
            }
            //InformationManager.DisplayMessage(new InformationMessage(new TextObject("Testing " + co.Name).ToString(), Colors.Blue));
            if (AddNewGuy.__instance.tier_threshold < 0)
            {
                if(co.UpgradeTargets == null || co.UpgradeTargets.Length == 0)
                {
                    return true;
                }
                //InformationManager.DisplayMessage(new InformationMessage(new TextObject("\t" + co.Name + " failed b/c has upgrades", (Dictionary<string, TextObject>)null).ToString(), Colors.Red));
            }
            else
            {
                if(co.Tier >= AddNewGuy.__instance.tier_threshold)
                {
                    return true;
                }
                //InformationManager.DisplayMessage(new InformationMessage(new TextObject("\t" + co.Name + " failed b/c tier too low", (Dictionary<string, TextObject>)null).ToString(), Colors.Red));
            }
            return false;
        }
        public static List<InquiryElement> gen_transfer_list(List<CharacterObject> _cos, bool require_mainparty = true)
        {
            List<InquiryElement> _ies = new List<InquiryElement>();
            
            foreach (CharacterObject _co in _cos)
            {
                if ((!require_mainparty) || (require_mainparty && MobileParty.MainParty.MemberRoster.Contains(_co)))
                {
                    _ies.Add(new InquiryElement((object)_co, _co.Name.ToString(), new ImageIdentifier(CharacterCode.CreateFrom((BasicCharacterObject)_co)), true, " kills"));
                }
            }
            
            return _ies;

        }
        //HERE is where we can hunt down the corresponding kill count
        public void OnNomineeSelectRAND(List<InquiryElement> ies)
        {
            foreach (InquiryElement ie in ies)
            {
                CharacterObject _co_ = (CharacterObject)(ie.Identifier);
                int kc = 0;
                string killhint = ie.Hint.Split(' ')[0];
                if (int.TryParse(killhint, out kc))
                {
                    InformationManager.DisplayMessage(new InformationMessage(_co_.Name + " got " + kc.ToString(), Colors.Red));
                }
                else
                {
                    kc = -1;
                }
                this.giveNewGuy(_co_, kc, false);
                if (MobileParty.MainParty.MemberRoster.Contains(_co_))
                {
                    MobileParty.MainParty.MemberRoster.RemoveTroop(_co_);
                }
            }
        }
        public void OnNomineeSelect(List<InquiryElement> ies)
        {
            foreach (InquiryElement ie in ies) {
                CharacterObject _co_ = (CharacterObject)(ie.Identifier);
                int kc = 0;
                string killhint = ie.Hint.Split(' ')[0];
                if(int.TryParse(killhint, out kc))
                {
                    InformationManager.DisplayMessage(new InformationMessage(_co_.Name + " got " + kc.ToString(), Colors.Red));
                }
                else
                {
                    kc = -1;
                }
                this.giveNewGuy(_co_, kc);
                if (MobileParty.MainParty.MemberRoster.Contains(_co_))
                {
                    MobileParty.MainParty.MemberRoster.RemoveTroop(_co_);
                }
            }
        }

        public int getCharTier(CharacterObject co)
        {
            return co.Tier;
        }


        /*[CommandLineFunctionality.CommandLineArgumentFunction("give_squad", "dservice")]
        public static string GiveSquad(List<string> strings)
        {
            int num = 10;
            int i = 0;
            if (!CampaignCheats.CheckParameters(strings, 1) || CampaignCheats.CheckHelp(strings))
            {
                num = 10;
            }
            else
            {
                if (!int.TryParse(strings[0], out num))
                {
                    return "Incorrect number given.\nUsage: give_squad [num]";
                }
            }

            CultureObject playerculture = Hero.MainHero.Culture;
            while (i < num)
            {
                foreach (PartyTemplateStack pts in playerculture.EliteCaravanPartyTemplate.Stacks)
                {
                    CharacterObject _co_ = CharacterObject.CreateFrom(pts.Character);
                    if(MBRandom.RandomFloat < 0.5) //50% female
                        _co_.TrySetPropertyValue("IsFemale",true);
                    AddNewGuy.__instance.giveNewGuy(_co_);
                    i++;
                    if (i == num)
                        break;
                }
            }
            return "Squad of " + num + " granted.";
        }
        */
        /*[CommandLineFunctionality.CommandLineArgumentFunction("uplift_soldier", "dservice")]
        public static string NewGuyCheat(List<string> strings)
        {
            int tierthresh = -1;
            if (!CampaignCheats.CheckParameters(strings,1) || CampaignCheats.CheckHelp(strings))
                return "Usage: uplift_soldier [tier threshold = 0]";
            if (!int.TryParse(strings[0], out tierthresh))
                tierthresh = 0;
            List<CharacterObject> cos = new List<CharacterObject>(MobileParty.MainParty.MemberRoster.ToFlattenedRoster().Troops);
            AddNewGuy.__instance.Shuffle(cos);
            foreach(CharacterObject co in cos)
            {
                //if they're already a hero, or too low a tier
                if (co.IsHero || co.Tier < tierthresh)
                    continue;
                AddNewGuy.__instance.giveNewGuy(co);
                MobileParty.MainParty.MemberRoster.RemoveTroop(co);
                return "Created new companion!";
            }
            return "Nobody elegible!";
        }*/

        /*[CommandLineFunctionality.CommandLineArgumentFunction("convert_party_to_heroes", "dservice")]
        public static string PartyToHeroes(List<string> strings)
        {
            bool keep_looping = true;
            while (keep_looping)
            {
                keep_looping = false;
                foreach (CharacterObject co in MobileParty.MainParty.MemberRoster.ToFlattenedRoster().Troops)
                {
                    if (co.IsHero)
                        continue;
                    AddNewGuy.__instance.giveNewGuy(co);
                    MobileParty.MainParty.MemberRoster.RemoveTroop(co);
                }
                //if there are still non-hero character objects in the roster, keep going
                foreach (CharacterObject co in MobileParty.MainParty.MemberRoster.ToFlattenedRoster().Troops)
                {
                    if (!co.IsHero)
                    {
                        keep_looping = true;
                        break;
                    }
                }
            }
            return "Player party converted";
        }*/

        /*[CommandLineFunctionality.CommandLineArgumentFunction("give_party_heroes_perks", "dservice")]
        public static string HeroesTakePerks(List<string> strings)
        {
            foreach (CharacterObject co in MobileParty.MainParty.MemberRoster.ToFlattenedRoster().Troops)
            {
                if (!co.IsHero || co.HeroObject == Hero.MainHero)
                    continue;

                CharacterDevelopmentCampaignBehavior cdcb = CharacterDevelopmentCampaignBehavior.GetCampaignBehavior<CharacterDevelopmentCampaignBehavior>();
                if (cdcb != null)
                    cdcb.DevelopCharacterStats(co.HeroObject);
                
            }
            return "Party hero perks assigned";
        } */

        //recruited units can go straight to heros
        public void recruit_to_hero(CharacterObject troop, int amount)
        {
            if(!is_soldier_qualified(troop) || !MobileParty.MainParty.MemberRoster.Contains(troop))
                return;
            for (int i = 0; i < amount; i++)
            {
                if(this.respect_companion_limit && Clan.PlayerClan.Companions.Count >= Clan.PlayerClan.CompanionLimit)
                { //stop giving companions if over companion limit and respecting it
                    MobileParty.MainParty.MemberRoster.RemoveTroop(troop, i);
                    return;
                }
                AddNewGuy.__instance.giveNewGuy(troop);
            }
            MobileParty.MainParty.MemberRoster.RemoveTroop(troop, amount);
        }

        //so can hired prisoners
        public void prisoner_to_hero(FlattenedTroopRoster ftr)
        {
            foreach(CharacterObject co in ftr.Troops)
            {
                if (is_soldier_qualified(co) && MobileParty.MainParty.MemberRoster.Contains(co))
                {
                    AddNewGuy.__instance.giveNewGuy(co);
                    //MobileParty.MainParty.MemberRoster.RemoveTroop(co);
                }
            }
        }
        //units can be upgraded into heros, too
        public void upgrade_to_hero(CharacterObject upgradeFromTroop,CharacterObject upgradeToTroop,int number)
        {
            if (!is_soldier_qualified(upgradeToTroop))
                return;
            for (int i = 0; i < number; i++)
            {
                if (this.respect_companion_limit && Clan.PlayerClan.Companions.Count >= Clan.PlayerClan.CompanionLimit)
                { //stop giving companions if over companion limit and respecting it
                    MobileParty.MainParty.MemberRoster.RemoveTroop(upgradeToTroop, i);
                    return;
                }
                AddNewGuy.__instance.giveNewGuy(upgradeToTroop);
            }
            MobileParty.MainParty.MemberRoster.RemoveTroop(upgradeToTroop, number);
        }

        public string getNameSuffix(CharacterObject co)
        {
            List<string> potential_suff = new List<string>();
            if (co.IsRanged)
            { 
                potential_suff.AppendList(NameList.archer_suff);
            }
            else if (!co.FirstBattleEquipment[EquipmentIndex.Horse].IsEmpty)// || co.IsMounted)
            {
                potential_suff.AppendList(NameList.cavalry_suff);
            }
            else
            {
                potential_suff.AppendList(NameList.infantry_suff);
            }
            switch (co.Culture.GetCultureCode())
            {
                case CultureCode.Aserai:
                    potential_suff.AppendList(NameList.aserai_suff);
                    break;
                case CultureCode.Battania:
                    potential_suff.AppendList(NameList.battanian_suff);
                    break;
                case CultureCode.Khuzait:
                    potential_suff.AppendList(NameList.khuzait_suff);
                    break;
                case CultureCode.Sturgia:
                    potential_suff.AppendList(NameList.sturgian_suff);
                    break;
                case CultureCode.Nord:
                    potential_suff.AppendList(NameList.sturgian_suff);
                    break;
                case CultureCode.Vakken:
                    potential_suff.AppendList(NameList.sturgian_suff);
                    break;
                case CultureCode.Darshi:
                    potential_suff.AppendList(NameList.aserai_suff);
                    break;
                case CultureCode.Vlandia:
                    potential_suff.AppendList(NameList.vlandian_suff);
                    break;
                default:
                    break;
            }
            if(co.Culture.StringId == "criminals")
            {
                potential_suff.Clear();
                potential_suff.AppendList(NameList.criminal_suff);
            }
            else if (co.Culture.StringId == "criminals2")
            {
                potential_suff.Clear();
                potential_suff.AppendList(NameList.criminal2_suff);
            }
            else if (co.Culture.StringId == "criminals3")
            {
                potential_suff.Clear();
                potential_suff.AppendList(NameList.criminal3_suff);
            }
            else if(co.Culture.StringId == "pirates")
            {
                potential_suff.Clear();
                potential_suff.AppendList(NameList.pirates_suff);
            }
            else if (co.Culture.StringId == "shift_sand")
            {
                potential_suff.Clear();
                potential_suff.AppendList(NameList.shiftingsands_suff);
            }
            return potential_suff[MBRandom.RandomInt(potential_suff.Count)];
        }

        //external namelist file IO
        public string GetNameFromExternalFile()
        {
            string o = "ABORT"; //fuck-up code
            if (File.Exists(extern_namelist))
            {
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(extern_namelist);
                    if (lines.Length == 0)
                        return "ABORT";
                    int sel_index = MBRandom.RandomInt(0, lines.Length);
                    o = lines[sel_index];
                    string new_text = "";
                    for(int i = 0; i < lines.Length; i++)
                    {
                        if(i != sel_index)
                        {
                            new_text += lines[i] + "\n";
                        }
                    }
                    File.WriteAllText(extern_namelist, new_text);
                    return o;
                }
                catch(Exception e)
                {
                    InformationManager.DisplayMessage(new InformationMessage(e.Message, Colors.Red));
                    return "ABORT";
                }

            }
            else
            {
                return "ABORT";
            }
            return o;
        }

        public void giveNewGuy(CharacterObject co, int kills = -1, bool pick_skills = true)
        {
            CharacterObject nco = Game.Current.ObjectManager.GetObject<CharacterObject>(co.StringId);
            co = nco;
            ////
            //Debug.Print(co.Name + " promoted in party ");
            ////
            if (co == null)
            {
                return;
            }
            CharacterObject wanderer = co.Culture.NotableAndWandererTemplates.GetRandomElementWithPredicate<CharacterObject>((Func<CharacterObject, bool>)(x => x.Occupation == Occupation.Wanderer && x.IsFemale == co.IsFemale && x.CivilianEquipments != null));
            if (wanderer == null)
            {
                if (!ignore_cautions)
                    InformationManager.DisplayMessage(new InformationMessage("CAUTION: No wanderer template with culture " + co.Culture.Name + " available.\nChoosing randomly instead.", Colors.Yellow));
                wanderer = CharacterObject.PlayerCharacter.Culture.NotableAndWandererTemplates.GetRandomElementWithPredicate<CharacterObject>((Func<CharacterObject, bool>)(x => x.Occupation == Occupation.Wanderer && x.IsFemale == co.IsFemale && x.CivilianEquipments != null));
            }
            //final fallback...
            if (wanderer == null)
            {
                if (!ignore_cautions)
                    InformationManager.DisplayMessage(new InformationMessage("WARNING: SOMETHING ACTUALLY WENT WRONG WITH WANDERER TEMPLATES\nPICKING COMPLETELY RANDOMLY", Colors.Red));
                wanderer = CharacterObject.PlayerCharacter.Culture.NotableAndWandererTemplates.GetRandomElementWithPredicate<CharacterObject>((Func<CharacterObject, bool>)(x => x.Occupation == Occupation.Wanderer && x.IsFemale == co.IsFemale));
            }
            if (wanderer == null)
            {
                InformationManager.DisplayMessage(new InformationMessage("WARNING: Could not find valid wanderer template. You broke something.", Colors.Red));
                return;
            }

            Hero specialHero = HeroCreator.CreateSpecialHero(wanderer, (Settlement)null, (Clan)null, (Clan)null, rand.Next(20, 50));
            bool external_name_successful = false;
            string new_name = "ABORT";
            if (using_extern_namelist)
            {
                new_name = GetNameFromExternalFile();
                if (new_name.Equals("ABORT"))
                {
                    external_name_successful = false;
                }
                else
                {
                    external_name_successful = true;
                    specialHero.SetName(new TextObject(new_name + getNameSuffix(co)), new TextObject(new_name));
                }
            }
            if (!using_extern_namelist || !external_name_successful)
            {
                specialHero.SetName(new TextObject(specialHero.FirstName.ToString() + getNameSuffix(co)), specialHero.FirstName);
            }
            specialHero.Culture = co.Culture;

            specialHero.CharacterObject.TrySetPropertyValue("DefaultFormationClass", co.DefaultFormationClass);
            specialHero.CharacterObject.TrySetPropertyValue("DefaultFormationGroup", co.DefaultFormationGroup);


            specialHero.ChangeState(Hero.CharacterStates.Active);
            AddCompanionAction.Apply(Clan.PlayerClan, specialHero);
            AddHeroToPartyAction.Apply(specialHero, MobileParty.MainParty, true);
            CampaignEventDispatcher.Instance.OnHeroCreated(specialHero, false);

            addTraitVariance(specialHero);
            float adjusted_cost = this.up_front_cost;
            //GI gives 30% discount
            if (Hero.MainHero.GetPerkValue(DefaultPerks.Trade.GreatInvestor))
            {
                adjusted_cost *= 0.7f;
            }
            //PiP gives 25% discount
            if (Hero.MainHero.GetPerkValue(DefaultPerks.Steward.PaidInPromise))
            {
                adjusted_cost *= 0.75f;
            }
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, specialHero, (int)adjusted_cost);
            specialHero.HasMet = true;
            //special, equipment-formatting try-catch statement
            try
            {
                if(MyLittleWarbandLoaded)
                {
                    InformationManager.DisplayMessage(new InformationMessage("MLBW compatibility mode:\nUsing first equipment sets", Colors.Yellow));
                    specialHero.BattleEquipment.FillFrom(co.FirstBattleEquipment);
                    specialHero.CivilianEquipment.FillFrom(co.FirstCivilianEquipment);
                }
                else
                {
                    specialHero.BattleEquipment.FillFrom(co.RandomBattleEquipment);
                    specialHero.CivilianEquipment.FillFrom(co.RandomCivilianEquipment);
                }
                this.AdjustEquipment(specialHero);
            }
            catch (Exception e)
            {
                if (!ignore_cautions)
                {
                    InformationManager.DisplayMessage(new InformationMessage("CAUTION: Something went wrong with this unit's equipment.\nAborting process, not everything might be added properly.", Colors.Yellow));
                    Debug.Print("Equipment format issue, providing default equipment instead! Exception details:\n" + e.Message);
                }
                //leave them naked, alone, and afraid
            }

            specialHero.HeroDeveloper.SetInitialLevel(co.Level);
            Dictionary<SkillObject, int> baseline_skills = new Dictionary<SkillObject, int>();
            foreach (SkillObject sk in Skills.All)
            {
                baseline_skills[sk] = Math.Min(co.GetSkillValue(sk),300);
                //specialHero.HeroDeveloper.SetInitialSkillLevel(sk, co.GetSkillValue(sk));
            }
            int curr_sk = 0;
            foreach (SkillObject sk in Skills.All)
            {
                curr_sk = specialHero.GetSkillValue(sk);
                specialHero.HeroDeveloper.ChangeSkillLevel(sk, baseline_skills[sk] - curr_sk);
            }
            //List<SkillObject> shuffled_skills = new List<SkillObject>(Skills.All);
            //Shuffle(shuffled_skills);
            int skp_to_assign = base_additional_skill_points + 50 * Hero.MainHero.GetSkillValue(DefaultSkills.Leadership) / leadership_points_per_50_extra_skill_points;
            if (kills > 0)
            {
                if(co.IsMounted)
                {
                    skp_to_assign += AddNewGuy.__instance.skp_per_excess_kill * (kills - AddNewGuy.__instance.cav_kill_threshold);
                }
                else if (co.IsRanged)
                {
                    skp_to_assign += AddNewGuy.__instance.skp_per_excess_kill * (kills - AddNewGuy.__instance.ran_kill_threshold);
                }
                else
                {
                    skp_to_assign += AddNewGuy.__instance.skp_per_excess_kill * (kills - AddNewGuy.__instance.inf_kill_threshold);
                }
            }

            //int bonus = 0;
            if (!select_skills_randomly)
            {
                for (int i = 1; i <= num_skill_rounds; i++)
                {
                    AssignSkills(specialHero, skp_to_assign / i, this.num_skill_bonuses, "Round " + i.ToString(), co.Name.ToString());
                }
            }
            else
            {
                for (int i = 1; i <= num_skill_rounds; i++)
                {
                    AssignSkillsRandomly(specialHero, skp_to_assign / i, this.num_skill_bonuses);
                }
            }
            //AssignSkills(specialHero, skp_to_assign, this.num_skill_bonuses, "Primary");
            //AssignSkills(specialHero, skp_to_assign / 2, MathF.Floor(1.5 * this.num_skill_bonuses), "Secondary"); //1.5 times, but round down
            //AssignSkills(specialHero, skp_to_assign / 4, 2 * this.num_skill_bonuses, "Tertiary");

            int tot_to_add = specialHero.HeroDeveloper.UnspentAttributePoints;
            specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Vigor, 2, false);
            specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Control, 2, false);
            specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Cunning, 2, false);
            specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Endurance, 2, false);
            specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Intelligence, 2, false);
            specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Social, 2, false);
            tot_to_add -= 12;

            int to_add = 0;
            if (tot_to_add > 0)
            {
                if (co.IsMounted)
                {
                    to_add = rand.Next(3);
                    specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Endurance, to_add, false);
                    tot_to_add -= to_add;
                }
                else if (co.IsRanged)
                {
                    to_add = rand.Next(3);
                    specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Control, to_add, false);
                    tot_to_add -= to_add;
                }
                else
                {
                    to_add = rand.Next(3);
                    specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Vigor, to_add, false);
                    tot_to_add -= to_add;
                }
                List<CharacterAttribute> shuffled_attrs = new List<CharacterAttribute>(Attributes.All);
                Shuffle(shuffled_attrs);
                foreach(CharacterAttribute ca in shuffled_attrs)
                {
                    to_add = rand.Next(2);
                    specialHero.HeroDeveloper.AddAttribute(ca, to_add, false);
                    tot_to_add -= to_add;
                    if (tot_to_add <= 0)
                        break;
                }
            }

            if (this.fill_perks)
            {
                CharacterDevelopmentCampaignBehavior cdcb = CharacterDevelopmentCampaignBehavior.GetCampaignBehavior<CharacterDevelopmentCampaignBehavior>();
                if (cdcb != null)
                    cdcb.DevelopCharacterStats(specialHero);
            }
            /*
            foreach (SkillObject sk in Skills.All)
            {
                if(specialHero.GetSkillValue(sk) > max_hero_skill_val)
                {
                    specialHero.HeroDeveloper.SetInitialSkillLevel(sk, max_hero_skill_val);
                }
            }
            */
            specialHero.HeroDeveloper.UnspentAttributePoints = 0;


        }

        //select skills for specified skill points
        public void AssignSkills(Hero specialHero, int skill_points_to_assign, int num_skills_to_select, string title_prefix, string prev = " basic soldier")
        {
            //Add options only if points can be added to them in a valid way
            List<InquiryElement> iqes = new List<InquiryElement>();
            if(specialHero.GetSkillValue(DefaultSkills.Scouting) < 300)
                iqes.Add(new InquiryElement("scout_bonus", "Ranged with the Scouts", null, true, "+" + skill_points_to_assign.ToString() + " scouting"));
            if (specialHero.GetSkillValue(DefaultSkills.Crafting) < 300)
                iqes.Add(new InquiryElement("smithing_bonus", "Repaired the party's weapons", null, true, "+" + skill_points_to_assign.ToString() + " smithing"));
            if (specialHero.GetSkillValue(DefaultSkills.Athletics) < 300)
                iqes.Add(new InquiryElement("athletics_bonus", "Trained for combat", null, true, "+" + skill_points_to_assign.ToString() + " athletics"));
            if (specialHero.GetSkillValue(DefaultSkills.Riding) < 300)
                iqes.Add(new InquiryElement("riding_bonus", "Rode horses", null, true, "+" + skill_points_to_assign.ToString() + " riding"));
            if (specialHero.GetSkillValue(DefaultSkills.Tactics) < 300)
                iqes.Add(new InquiryElement("tactics_bonus", "Studied past battles", null, true, "+" + skill_points_to_assign.ToString() + " tactics"));
            if (specialHero.GetSkillValue(DefaultSkills.Roguery) < 300)
                iqes.Add(new InquiryElement("roguery_bonus", "Sold loot in the black market", null, true, "+" + skill_points_to_assign.ToString() + " roguery"));
            if (specialHero.GetSkillValue(DefaultSkills.Charm) < 300)
                iqes.Add(new InquiryElement("charm_bonus", "Chatted everyone up", null, true, "+" + skill_points_to_assign.ToString() + " charm"));
            if (specialHero.GetSkillValue(DefaultSkills.Leadership) < 300)
                iqes.Add(new InquiryElement("leadership_bonus", "Organized duties for the party", null, true, "+" + skill_points_to_assign.ToString() + " leadership"));
            if (specialHero.GetSkillValue(DefaultSkills.Trade) < 300)
                iqes.Add(new InquiryElement("trade_bonus", "Bought and sold items from towns you visited", null, true, "+" + skill_points_to_assign.ToString() + " trade"));
            if (specialHero.GetSkillValue(DefaultSkills.Steward) < 300)
                iqes.Add(new InquiryElement("steward_bonus", "Helped handle the party's accounts", null, true, "+" + skill_points_to_assign.ToString() + " stewardship"));
            if (specialHero.GetSkillValue(DefaultSkills.Medicine) < 300)
                iqes.Add(new InquiryElement("medicine_bonus", "Helped as a medic", null, true, "+" + skill_points_to_assign.ToString() + " medicine"));
            if (specialHero.GetSkillValue(DefaultSkills.Engineering) < 300)
                iqes.Add(new InquiryElement("engineering_bonus", "Helped construct and take down the camp", null, true, "+" + skill_points_to_assign.ToString() + " engineering"));

            MultiSelectionInquiryData msid = new MultiSelectionInquiryData("Select " + title_prefix + " Skill Focuses", specialHero.Name + " (previously a " + prev + ") has finally found glory on the battlefield. Before this, and besides training for battle they...", iqes, true, num_skills_to_select, "Accept", "Refuse", (Action<List<InquiryElement>>)((List<InquiryElement> ies) =>
            {
                int diff = 0;
                foreach (InquiryElement ie in ies)
                {
                    switch ((string)ie.Identifier)
                    {
                        case "scout_bonus":
                            try
                            {
                                //base_skills[DefaultSkills.Scouting] += skill_points_to_assign;
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Scouting); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Scouting, Math.Min(skill_points_to_assign,diff));
                            }
                            catch { }
                            break;
                        case "smithing_bonus":
                            try
                            {
                                //base_skills[DefaultSkills.Crafting] += skill_points_to_assign;
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Crafting); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Crafting, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "athletics_bonus":
                            try
                            {
                                //base_skills[DefaultSkills.Athletics] += skill_points_to_assign;
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Athletics); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Athletics, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "riding_bonus":
                            try
                            {
                                //base_skills[DefaultSkills.Riding] += skill_points_to_assign;
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Riding); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Riding, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "tactics_bonus":
                            try
                            {
                                //base_skills[DefaultSkills.Tactics] += skill_points_to_assign;
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Tactics); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Tactics, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "roguery_bonus":
                            try
                            {
                                //base_skills[DefaultSkills.Roguery] += skill_points_to_assign;
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Roguery); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Roguery, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "charm_bonus":
                            try
                            {
                                //base_skills[DefaultSkills.Charm] += skill_points_to_assign;
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Charm); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Charm, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "leadership_bonus":
                            try
                            {
                                //base_skills[DefaultSkills.Leadership] += skill_points_to_assign;
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Leadership); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Leadership, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "trade_bonus":
                            try
                            {
                                //base_skills[DefaultSkills.Trade] += skill_points_to_assign;
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Trade); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Trade, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "steward_bonus":
                            try
                            {
                                //base_skills[DefaultSkills.Steward] += skill_points_to_assign;
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Steward); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Steward, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "medicine_bonus":
                            try
                            {
                                //base_skills[DefaultSkills.Medicine] += skill_points_to_assign;
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Medicine); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Medicine, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "engineering_bonus":
                            try
                            {
                                //base_skills[DefaultSkills.Engineering] += skill_points_to_assign;
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Engineering); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Engineering, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                    }
                    try
                    {
                        specialHero.HeroDeveloper.CallMethod("CheckInitialLevel", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    }
                    catch (Exception e)
                    {
                        if (!ignore_cautions)
                            InformationManager.DisplayMessage(new InformationMessage("CAUTION: Reflection call to CheckInitialLevel failed. Potential version issue.", Colors.Yellow));
                    }
                }
            }),
            (Action<List<InquiryElement>>)null);
            MBInformationManager.ShowMultiSelectionInquiry(msid, true);
            //InformationManager.ShowMultiSelectionInquiry(msid, true);
        }

        public void AssignSkillsRandomly(Hero specialHero, int skill_points_to_assign, int num_skills_to_select)
        {
            for(int i = 0; i < num_skills_to_select; i++)
            {
                switch ((int)(MBRandom.RandomFloat*12) + 1)
                {
                    case 1:
                        try
                        {
                            //base_skills[DefaultSkills.Scouting] += skill_points_to_assign;
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Scouting, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 2:
                        try
                        {
                            //base_skills[DefaultSkills.Crafting] += skill_points_to_assign;
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Crafting, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 3:
                        try
                        {
                            //base_skills[DefaultSkills.Athletics] += skill_points_to_assign;
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Athletics, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 4:
                        try
                        {
                            //base_skills[DefaultSkills.Riding] += skill_points_to_assign;
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Riding, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 5:
                        try
                        {
                            //base_skills[DefaultSkills.Tactics] += skill_points_to_assign;
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Tactics, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 6:
                        try
                        {
                            //base_skills[DefaultSkills.Roguery] += skill_points_to_assign;
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Roguery, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 7:
                        try
                        {
                            //base_skills[DefaultSkills.Charm] += skill_points_to_assign;
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Charm, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 8:
                        try
                        {
                            //base_skills[DefaultSkills.Leadership] += skill_points_to_assign;
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Leadership, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 9:
                        try
                        {
                            //base_skills[DefaultSkills.Trade] += skill_points_to_assign;
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Trade, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 10:
                        try
                        {
                            //base_skills[DefaultSkills.Steward] += skill_points_to_assign;
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Steward, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 11:
                        try
                        {
                            //base_skills[DefaultSkills.Medicine] += skill_points_to_assign;
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Medicine, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 12:
                        try
                        {
                            //base_skills[DefaultSkills.Engineering] += skill_points_to_assign;
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Engineering, skill_points_to_assign);
                        }
                        catch { }
                        break;
                }
            }
            int diff = 0;
            foreach(SkillObject sk in Skills.All)
            {
                diff = 300 - specialHero.GetSkillValue(sk);
                if(diff < 0)
                {
                    specialHero.HeroDeveloper.ChangeSkillLevel(sk, diff); //subtract
                }
            }
            try
            {
                specialHero.HeroDeveloper.CallMethod("CheckInitialLevel", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            }
            catch (Exception e)
            {
                if (!ignore_cautions)
                    InformationManager.DisplayMessage(new InformationMessage("CAUTION: Reflection call to CheckInitialLevel failed. Potential version issue.", Colors.Yellow));
            }
        }

        //adds new hero to arbitrary party
        public void AddNewGuyToParty(CharacterObject co, MobileParty party)
        {
            if (co == null || party == null)
            {
                return;
            }
            Hero party_leader = party?.LeaderHero;
            if(party_leader == null)
            {
                return; //needs seed hero
            }

            CharacterObject wanderer = co.Culture.NotableAndWandererTemplates.GetRandomElementWithPredicate<CharacterObject>((Func<CharacterObject, bool>)(x => x.Occupation == Occupation.Wanderer && x.IsFemale == co.IsFemale && x.CivilianEquipments != null));
            if (wanderer == null)
            {
                wanderer = CharacterObject.PlayerCharacter.Culture.NotableAndWandererTemplates.GetRandomElementWithPredicate<CharacterObject>((Func<CharacterObject, bool>)(x => x.Occupation == Occupation.Wanderer && x.IsFemale == co.IsFemale && x.CivilianEquipments != null));
            }
            if (wanderer == null)
            {
                //big fuck-up here! No eligible wanderers at all
                return;
            }
            Hero specialHero = HeroCreator.CreateSpecialHero(wanderer, (Settlement)null, (Clan)null, (Clan)null, rand.Next(20, 50));
            specialHero.SetName(new TextObject(specialHero.FirstName.ToString() + getNameSuffix(co)), specialHero.FirstName);
            specialHero.Culture = co.Culture;

            //specialHero.CharacterObject.TrySetPropertyValue("Occupation", Occupation.Wanderer);
            specialHero.CharacterObject.TrySetPropertyValue("DefaultFormationClass", co.DefaultFormationClass);
            specialHero.CharacterObject.TrySetPropertyValue("DefaultFormationGroup", co.DefaultFormationGroup);


            specialHero.ChangeState(Hero.CharacterStates.Active);
            //AddCompanionAction.Apply(party_leader.Clan, specialHero); //maybe don't add to clan?
            AddHeroToPartyAction.Apply(specialHero, party, true);
            CampaignEventDispatcher.Instance.OnHeroCreated(specialHero, false);

            addTraitVariance(specialHero);
            GiveGoldAction.ApplyBetweenCharacters(party_leader, specialHero, this.up_front_cost, true);
            specialHero.HasMet = false;

            //special, equipment-formatting try-catch statement
            try
            {
                specialHero.BattleEquipment.FillFrom(co.FirstBattleEquipment);//co.RandomBattleEquipment);
                specialHero.CivilianEquipment.FillFrom(co.FirstCivilianEquipment);// co.RandomCivilianEquipment);
                this.AdjustEquipment(specialHero);
            }
            catch (Exception e)
            {
                if (!ignore_cautions)
                {
                    InformationManager.DisplayMessage(new InformationMessage("CAUTION: Something went wrong with this unit's equipment.\nAborting process, not everything might be added properly.", Colors.Yellow));
                    Debug.Print("Equipment format issue, providing default equipment instead! Exception details:\n" + e.Message);
                }
                //leave them naked, alone, and afraid
            }

            if (co.IsMounted)
            {
                specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Vigor, 2 + rand.Next(2), false);
                specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Control, 1 + rand.Next(2), false);
                specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Endurance, 4 + rand.Next(3), false);
            }
            else if (co.IsRanged)
            {
                specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Vigor, 2 + rand.Next(2), false);
                specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Control, 4 + rand.Next(3), false);
                specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Endurance, 1 + rand.Next(2), false);
            }
            else
            {
                specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Vigor, 3 + rand.Next(3), false);
                specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Control, 2 + rand.Next(2), false);
                specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Endurance, 2 + rand.Next(2), false);
            }
            specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Cunning, 1 + rand.Next(3), false);
            specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Social, 1 + rand.Next(3), false);
            specialHero.HeroDeveloper.AddAttribute(DefaultCharacterAttributes.Intelligence, 1 + rand.Next(3), false);


            foreach (SkillObject sk in Skills.All)
            {

                specialHero.HeroDeveloper.ChangeSkillLevel(sk, co.GetSkillValue(sk), false);
            }

            List<SkillObject> shuffled_skills = new List<SkillObject>(Skills.All);
            Shuffle(shuffled_skills);
            int skp_to_assign = base_additional_skill_points + 50 * party_leader.GetSkillValue(DefaultSkills.Leadership) / leadership_points_per_50_extra_skill_points;
            int bonus = 0;
            //specialHero.HeroDeveloper.SetInitialLevel(co.Level);
            foreach (SkillObject sk in shuffled_skills)
            {
                if (sk == DefaultSkills.OneHanded || sk == DefaultSkills.TwoHanded || sk == DefaultSkills.Polearm || sk == DefaultSkills.Bow || sk == DefaultSkills.Crossbow || sk == DefaultSkills.Throwing)
                { //give fewer bonus skill points to combat skills
                    bonus = rand.Next(10) + rand.Next(15);
                }
                else
                {
                    bonus = rand.Next(10) + rand.Next(15) + rand.Next(25);
                }
                skp_to_assign -= bonus;
                if (skp_to_assign < 0)
                    bonus += skp_to_assign;
                try
                {
                    specialHero.HeroDeveloper.ChangeSkillLevel(sk, bonus, false);
                }
                catch (Exception e)
                {
                    //InformationManager.DisplayMessage(new InformationMessage("CAUTION: Something went wrong assigning skill levels to " + sk.Name + "\nAborting process, not everything might be added properly.", Colors.Yellow));
                    //Debug.Print("Equipment format issue, providing default equipment instead! Exception details:\n" + e.Message);
                }

                //specialHero.HeroDeveloper.UnspentFocusPoints += specialHero.Level;

                if (skp_to_assign <= 0)
                    break;
            }
            try
            {
                specialHero.HeroDeveloper.CallMethod("CheckInitialLevel", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            }
            catch (Exception e)
            {
                //nothing, just prevent random crashes from out of nowhere
            }
            foreach (PerkObject po in ((HeroDeveloper)specialHero.HeroDeveloper).GetOneAvailablePerkForEachPerkPair())
            {
                specialHero.HeroDeveloper.AddPerk(po);
            }

            CharacterDevelopmentCampaignBehavior cdcb = CharacterDevelopmentCampaignBehavior.GetCampaignBehavior<CharacterDevelopmentCampaignBehavior>();
            if (cdcb != null)
                cdcb.DevelopCharacterStats(specialHero);
            
            //specialHero.AlwaysDie = true; //lord companions will just die if they're wounded

        }

        public void AdjustEquipment(Hero _h)
        {
            Equipment eq = _h.BattleEquipment;

            ItemModifier itemModifier1 = MBObjectManager.Instance.GetObject<ItemModifier>("companion_armor");
            ItemModifier itemModifier2 = MBObjectManager.Instance.GetObject<ItemModifier>("companion_weapon");
            ItemModifier itemModifier3 = MBObjectManager.Instance.GetObject<ItemModifier>("companion_horse");
            for (EquipmentIndex index = EquipmentIndex.WeaponItemBeginSlot; index < EquipmentIndex.NumEquipmentSetSlots; ++index)
            {
                EquipmentElement equipmentElement = eq[index];
                if (equipmentElement.Item != null)
                {
                    if (equipmentElement.Item.ArmorComponent != null)
                        eq[index] = new EquipmentElement(equipmentElement.Item, itemModifier1);
                    else if (equipmentElement.Item.HorseComponent != null)
                        eq[index] = new EquipmentElement(equipmentElement.Item, itemModifier3);
                    else if (equipmentElement.Item.WeaponComponent != null)
                        eq[index] = new EquipmentElement(equipmentElement.Item, itemModifier2);
                }
            }
        }

        public void addTraitVariance(Hero hero)
        {
            foreach (TraitObject trait in TraitObject.All)
            {
                if (trait == DefaultTraits.Honor || trait == DefaultTraits.Mercy || (trait == DefaultTraits.Generosity || trait == DefaultTraits.Valor) || trait == DefaultTraits.Calculating)
                {
                    int num1 = hero.CharacterObject.GetTraitLevel(trait);
                    float num2 = MBRandom.RandomFloat;
                    //skew towards player's traits
                    if (Hero.MainHero.GetTraitLevel(trait) >= 0.9)
                    {
                        num2 *= 1.2f;
                    }

                    if ((double)num2 < 0.1)
                    {
                        --num1;
                        if (num1 < -1)
                            num1 = -1;
                    }
                    if ((double)num2 > 0.9)
                    {
                        ++num1;
                        if (num1 > 1)
                            num1 = 1;
                    }

                    int num3 = MBMath.ClampInt(num1, trait.MinValue, trait.MaxValue);
                    hero.SetTraitLevel(trait, num3);
                    //hero.SetTraitLevel(trait, num3);
                }
            }
        }

        public void Shuffle<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rand.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        //Player dialogs for changing NPC name
        public static void addDialogs(CampaignGameStarter campaignGameStarter)
        {
            //name change
            campaignGameStarter.AddPlayerLine("companion_change_name_start", "hero_main_options", "companion_change_name_confirm", "I want you to be known by another name...", new ConversationSentence.OnConditionDelegate(namechangecondition), new ConversationSentence.OnConsequenceDelegate(namechanceconsequence), 100, (ConversationSentence.OnClickableConditionDelegate)null, (ConversationSentence.OnPersuasionOptionDelegate)null);
            campaignGameStarter.AddDialogLine("companion_change_name_confirm", "companion_change_name_confirm", "hero_main_options", "That is all.", (ConversationSentence.OnConditionDelegate)null, (ConversationSentence.OnConsequenceDelegate)null, 100, (ConversationSentence.OnClickableConditionDelegate)null);

            //give companion to party
            campaignGameStarter.AddPlayerLine("companion_transfer_start", "hero_main_options", "companion_transfer_confirm", "Take these companions into your party...", new ConversationSentence.OnConditionDelegate(givecomptoclanpartycondition), new ConversationSentence.OnConsequenceDelegate(givecomptoclanpartyconsequence), 100, (ConversationSentence.OnClickableConditionDelegate)null, (ConversationSentence.OnPersuasionOptionDelegate)null);
            campaignGameStarter.AddDialogLine("companion_transfer_confirm", "companion_transfer_confirm", "hero_main_options", "That is all.", (ConversationSentence.OnConditionDelegate)null, (ConversationSentence.OnConsequenceDelegate)null, 100, (ConversationSentence.OnClickableConditionDelegate)null);

            //take companion back from party
            campaignGameStarter.AddPlayerLine("companion_takeback_start", "hero_main_options", "companion_takeback_confirm", "I wish to reassign heroes in your party...", new ConversationSentence.OnConditionDelegate(takecompfromclanpartycondition), new ConversationSentence.OnConsequenceDelegate(takecompfromclanpartyconsequence), 100, (ConversationSentence.OnClickableConditionDelegate)null, (ConversationSentence.OnPersuasionOptionDelegate)null);
            campaignGameStarter.AddDialogLine("companion_takeback_confirm", "companion_takeback_confirm", "hero_main_options", "That is all.", (ConversationSentence.OnConditionDelegate)null, (ConversationSentence.OnConsequenceDelegate)null, 100, (ConversationSentence.OnClickableConditionDelegate)null);
            
            //poach a companion from a defeated party
            campaignGameStarter.AddPlayerLine("enemy_comp_recruit_1", "defeated_lord_answer", "companion_poach_confirm", "After this defeat, are you sure you wouldn't rather work for me?", new ConversationSentence.OnConditionDelegate(AddNewGuy.CapturedAIWandererCondition), (ConversationSentence.OnConsequenceDelegate)null, 100, (ConversationSentence.OnClickableConditionDelegate)null, (ConversationSentence.OnPersuasionOptionDelegate)null); //new ConversationSentence.OnClickableConditionDelegate(CanConvertWanderer)
            campaignGameStarter.AddDialogLine("enemy_comp_recruit_2", "companion_poach_confirm", "close_window", "{RECRUIT_RESPONSE}", (ConversationSentence.OnConditionDelegate)null, (ConversationSentence.OnConsequenceDelegate)null, 100, (ConversationSentence.OnClickableConditionDelegate)null);

        }

        private bool CanConvertWanderer(out TextObject textout)
        {
            textout = new TextObject("Bleh");
            return false;
        }
        private static bool CapturedAIWandererCondition()
        {
            if(Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.Clan != Clan.PlayerClan && Hero.OneToOneConversationHero.Occupation == Occupation.Wanderer)
            {
                //get potential relation with player
                float points = 0.0f;
                float max_contrib = 0; //track highest contribution to points
                float min_contrib = 0; //track lowest, to set rejection text
                int max_one = 0;
                int min_one = 0;
                float temp = 0;
                //calculating will like player for being better, not-calculating just vibesss
                temp = Mathf.Max(0.25f * MBRandom.RandomFloatRanged(0.1f, 0.5f) * Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Calculating), 0);
                if(temp > max_contrib)
                {
                    max_one = 1;
                }
                else if(temp < min_contrib)
                {
                    min_one = 1;
                }
                //impulsiveness
                temp = Mathf.Max(-0.25f * MBRandom.RandomFloatRanged(0.1f, 0.5f) * Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Calculating), 0);
                if (temp > max_contrib)
                {
                    max_one = 5;
                }
                else if (temp < min_contrib)
                {
                    min_one = 5;
                }
                points += temp;
                //honorable AI won't like idea of joining up with player
                //dishonorable will prefer it
                temp = -0.5f * MBRandom.RandomFloatRanged(0.1f, 0.5f) * Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Honor);
                if(-temp > max_contrib)
                {
                    max_one = 2;
                }
                else if(temp < min_contrib)
                {
                    min_one = 2;
                }
                points += temp;
                //Risk-taking AI will want to join
                //risk-averse won't
                temp = 0.5f * MBRandom.RandomFloatRanged(0.1f, 0.5f) * Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Gambler);
                if(temp > max_contrib)
                {
                    max_one = 3;
                }
                else if(temp < min_contrib)
                {
                    min_one = 3;
                }
                points += temp;
                //Valourous AI will think about how glorious it could be
                temp = 0.25f * MBRandom.RandomFloatRanged(0.1f,0.5f) * Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Valor);
                if (temp > max_contrib)
                {
                    max_one = 4;
                }
                else if (temp < min_contrib)
                {
                    min_one = 4;
                }
                points += temp;



                if (MBRandom.RandomFloat < points) //succeed
                {
                    //pick response
                    switch (max_one)
                    {
                        default: //somehow everything's zero, or it's fucked
                            MBTextManager.SetTextVariable("RECRUIT_RESPONSE", new TextObject("Well, uh, I guess it beats prison..."));
                            break;
                        case 1: //Calculating acceptance
                            MBTextManager.SetTextVariable("RECRUIT_RESPONSE", new TextObject("If it means I don't get chained up, it sounds good to me!"));
                            break;
                        case 2: //Dishonorable acceptance
                            MBTextManager.SetTextVariable("RECRUIT_RESPONSE", new TextObject("Deal - I never liked the other guy anyway."));
                            break;
                        case 3: //Gambler acceptance
                            MBTextManager.SetTextVariable("RECRUIT_RESPONSE", new TextObject("Deal. Working with you seems like the safest bet."));
                            break;
                        case 4: //Valour acceptance
                            MBTextManager.SetTextVariable("RECRUIT_RESPONSE", new TextObject("I like your proposition! We shall face more glorious battle together!"));
                            break;
                        case 5: //Impulsive acceptance
                            MBTextManager.SetTextVariable("RECRUIT_RESPONSE", new TextObject("Uh... Sure, why not."));
                            break;
                    }
                    Hero.OneToOneConversationHero.ChangeState(Hero.CharacterStates.Active);
                    AddCompanionAction.Apply(Clan.PlayerClan, Hero.OneToOneConversationHero);
                    AddHeroToPartyAction.Apply(Hero.OneToOneConversationHero, MobileParty.MainParty, true);

                    return true;
                }
                else //fail
                {
                    //pick response
                    switch (max_one)
                    {
                        default: //somehow everything's zero, or it's fucked
                            MBTextManager.SetTextVariable("RECRUIT_RESPONSE", new TextObject("I just can't accept!"));
                            break;
                        case 1: //Calculating rejection
                            MBTextManager.SetTextVariable("RECRUIT_RESPONSE", new TextObject("I can't just join with whoever's most convenient! The whole system would fall apart!"));
                            break;
                        case 2: //Honorable rejection
                            MBTextManager.SetTextVariable("RECRUIT_RESPONSE", new TextObject("I would never betray my commander like that!"));
                            break;
                        case 3: //Gambler rejection
                            MBTextManager.SetTextVariable("RECRUIT_RESPONSE", new TextObject("No - I won't throw my life away for somebody who hasn't earned it."));
                            break;
                        case 4: //Valour rejection
                            MBTextManager.SetTextVariable("RECRUIT_RESPONSE", new TextObject("I just want to retire after this! No deal!"));
                            break;
                    }

                    return true;
                }
            }
            return false;
        }

        //Name Change stuff
        private static bool namechangecondition()
        {
            return Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.Clan == Clan.PlayerClan && Hero.OneToOneConversationHero.IsPlayerCompanion;
        }
        private static void namechanceconsequence()
        {
            InformationManager.ShowTextInquiry(new TextInquiryData("Create a new name: ", string.Empty, true, false, GameTexts.FindText("str_done", (string)null).ToString(), (string)null, new Action<string>(AddNewGuy.change_hero_name), (Action)null, false), false);

        }
        private static void change_hero_name(string s)
        {
            Hero.OneToOneConversationHero.SetName(new TextObject(s), new TextObject(s));
        }

        //companion to party stuff
        private static bool givecomptoclanpartycondition()
        {
            return Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.Clan == Clan.PlayerClan && (Hero.OneToOneConversationHero.IsPartyLeader);
        }
        private static void givecomptoclanpartyconsequence()
        {
            //InformationManager
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Transfer Heroes", "You can select who to assign to " + Hero.OneToOneConversationHero.Name + "'s party.", AddNewGuy.gen_transfer_list(AddNewGuy.player_party_heroes()), true, PartyBase.MainParty.MemberRoster.Count, "DONE", "NOBODY", new Action<List<InquiryElement>>(AddNewGuy.transfer_characters_to_conversation), (Action<List<InquiryElement>>)null, ""), true);
        }

        private static List<CharacterObject> player_party_heroes()
        {
            List<CharacterObject> _hs_ = new List<CharacterObject>();
            foreach (TroopRosterElement tre in MobileParty.MainParty.MemberRoster.GetTroopRoster())
            {
                CharacterObject co = tre.Character;
                if (!co.IsHero || co.IsPlayerCharacter)
                    continue;
                _hs_.Add(co);
            }
            return _hs_;
        }

        private static void transfer_characters_to_conversation(List<InquiryElement> ies)
        {
            MobileParty conv = Hero.OneToOneConversationHero.PartyBelongedTo;
            foreach(InquiryElement ie in ies)
            {
                CharacterObject _co_ = (CharacterObject)(ie.Identifier);
                Hero _h_ = _co_.HeroObject;

                AddHeroToPartyAction.Apply(_h_, conv, true);
            }
        }

        //take companion back stuff
        private static bool takecompfromclanpartycondition()
        {
            return Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.Clan == Clan.PlayerClan && (Hero.OneToOneConversationHero.IsPartyLeader);
        }
        private static void takecompfromclanpartyconsequence()
        {
            //InformationManager
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Transfer Heroes", "You can select who to take from " + Hero.OneToOneConversationHero.Name + "'s party.", AddNewGuy.gen_transfer_list(AddNewGuy.conversation_party_heroes(),false), true, PartyBase.MainParty.MemberRoster.Count, "DONE", "NOBODY", new Action<List<InquiryElement>>(AddNewGuy.transfer_characters_from_conversation), (Action<List<InquiryElement>>)null, ""), true);
        }
        private static List<CharacterObject> conversation_party_heroes()
        {
            PartyBase convparty = Hero.OneToOneConversationHero.PartyBelongedTo.Party;
            List<CharacterObject> _hs_ = new List<CharacterObject>();
            foreach (TroopRosterElement tre in convparty.MemberRoster.GetTroopRoster())
            {
                CharacterObject co = tre.Character;
                if (!co.IsHero || co.HeroObject == Hero.OneToOneConversationHero)
                {
                    continue;
                }
                _hs_.Add(co);
            }
            return _hs_;
        }
        private static void transfer_characters_from_conversation(List<InquiryElement> ies)
        {
            foreach (InquiryElement ie in ies)
            {
                CharacterObject _co_ = (CharacterObject)(ie.Identifier);
                Hero _h_ = _co_.HeroObject;
                AddHeroToPartyAction.Apply(_h_, MobileParty.MainParty, true);
            }
        }

    }
}
