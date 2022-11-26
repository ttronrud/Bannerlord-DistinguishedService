/*
 * Author: Thor Tronrud
 * PromotionManager.cs:
 * 
 * Pretty monolothic, and by accretion, not necessity. Acts as a big
 * state object with a lot of static methods providing the utilities
 * used to promote basic troops to companions.
 * 
 * It is fed a list of nominees by the Battle Behaviour class and presents
 * them to the player.
 * 
 * Also includes additional dialogue and supporting methods.
 */

using Fasterflect;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace DistinguishedService
{
    class PromotionManager
    {
        Random rand; //Single random object to use

        //Distinguished Service Specific Lists
        public static PromotionManager __instance = null; //our static instance
        public List<CharacterObject> nominations; //Who's currently nominated for a promotion?
        public List<int> killcounts; //What's their killcount?

        public static bool MyLittleWarbandLoaded = false; //is MLWB loaded? We'll need compatibility adjustments -_-

        //Settings values
        public bool using_extern_namelist { get; set; } //are we using an external namelist?
        public string extern_namelist { get; set; } //What is it?
        public int max_nominations { get; set; } //How many mooks can be promoted at once?
        public int tier_threshold { get; set; } //Minimum tier (-1 = end)
        public int inf_kill_threshold { get; set; } //Type-specific kill minimums to qualify
        public int cav_kill_threshold { get; set; }
        public int ran_kill_threshold { get; set; }
        public bool fill_perks { get; set; } //fill perks automatically on promotion?
        public float outperform_percentile { get; set; } //What percentile of kills should the nominee lie above?
        public int up_front_cost { get; set; } //Do they cost money to promote?
        public bool respect_companion_limit { get; set; } //Do we care about the game's companion limit?
        public bool ignore_cautions { get; set; } //Do you want to know if something might break?
        private int base_additional_skill_points { get; set; } //How many base skill points do we give these companions?
        private int leadership_points_per_50_extra_skill_points { get; set; } //And a bonus for high leadership
        private int skp_per_excess_kill { get; set; } //How many extra skills points do excess kills grant?
        public bool select_skills_randomly { get; set; } //No player input on skill selections? For games with lots of companions
        public int num_skill_rounds { get; set; } //How many rounds of skill selection do we go through? (Primary, secondary, tertiary, etc...)
        public int num_skill_bonuses { get; set; } //How many specific skills can be selected per round?

        public float ai_promotion_chance { get; set; } //Can AI lords promote troops?
        public int max_ai_comp_per_party { get; set; } //How many do we allow in their party at once?


        public PromotionManager()
        {
            //string path = Path.Combine(BasePath.Name, "Modules", "DistinguishedService", "Settings.xml");
            //start with what we know will work
            string path = Path.Combine(TaleWorlds.ModuleManager.ModuleHelper.GetModuleFullPath("DistinguishedService"), "Settings.xml");
            //check for a settings in the modules folder
            //if it exists, use it instead!
            if (File.Exists(Path.Combine(BasePath.Name, "Modules", "DistinguishedService", "Settings.xml")))
            {
                InformationManager.DisplayMessage(new InformationMessage("Using Modules/DistinguishedService Settings", Color.FromUint(4282569842U)));
                path = Path.Combine(BasePath.Name, "Modules", "DistinguishedService", "Settings.xml");
            }
            Settings currentsettings;
            using (Stream stream = (Stream)new FileStream(path, FileMode.Open))
                currentsettings = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream);
            //Set from settings
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
            this.base_additional_skill_points = currentsettings.base_additional_skill_points;
            this.leadership_points_per_50_extra_skill_points = currentsettings.leadership_points_per_50_extra_skill_points;
            this.num_skill_bonuses = currentsettings.number_of_skill_bonuses;
            this.num_skill_rounds = currentsettings.number_of_skill_rounds;
            this.select_skills_randomly = currentsettings.select_skills_randomly;

            this.ai_promotion_chance = currentsettings.ai_promotion_chance;
            this.max_ai_comp_per_party = currentsettings.max_ai_companions_per_party;

            rand = new Random();
            nominations = new List<CharacterObject>();
            killcounts = new List<int>();

            this.using_extern_namelist = currentsettings.NAMES_FROM_EXTERNAL_FILE;
            this.extern_namelist = Path.Combine(TaleWorlds.ModuleManager.ModuleHelper.GetModuleFullPath("DistinguishedService"), currentsettings.EXTERNAL_NAME_FILE); 
            //Do the same with the namelist -- use the easier-to-access Modules folder preferentially
            if(File.Exists(Path.Combine(BasePath.Name, "Modules", "DistinguishedService", currentsettings.EXTERNAL_NAME_FILE)))
            {
                this.extern_namelist = Path.Combine(BasePath.Name, "Modules", "DistinguishedService", currentsettings.EXTERNAL_NAME_FILE);
            }

            

            if (this.using_extern_namelist)
            {
                InformationManager.DisplayMessage(new InformationMessage("USING EXTERNAL NAMELIST FILE!\nThis file will be written back to/edited to knock out used names", Color.FromUint(4282569842U)));
            }
            //set other values from settings
            this.fill_perks = currentsettings.fill_in_perks;

            //Output final mod state to user, set static instance
            InformationManager.DisplayMessage(new InformationMessage("Max nominations = " + this.max_nominations + "\nTier Thresh = " + this.tier_threshold + "\nKill Thresh:\nInf = " + this.inf_kill_threshold + " cav = " + this.cav_kill_threshold + " ran = " + this.ran_kill_threshold + "\nPerformance Thresh = " + this.outperform_percentile, Color.FromUint(4282569842U)));
            PromotionManager.__instance = this;

            //Display warnings if chosen settings will cause non-player-controlled events
            //e.g. auto perk selection, auto-promotion, ignoring companion limit
            if (currentsettings.upgrade_to_hero)
            {
                InformationManager.DisplayMessage(new InformationMessage("CAUTION: Troops will auto-promote to heros at tier " + this.tier_threshold + "\nChange settings if this is unintended.", Colors.Yellow));
            }
            if (this.fill_perks)
            {
                InformationManager.DisplayMessage(new InformationMessage("CAUTION: New hero perks will be assigned automatically.\nChange settings if this is unintended.", Colors.Yellow));
            }
            if(!this.respect_companion_limit)
            {
                InformationManager.DisplayMessage(new InformationMessage("CAUTION: Ignoring companion limit.", Colors.Yellow));
            }
        }

        //Called when the battle is considered "over"
        //Doing it now sidesteps the UI elements being rendered underneath
        //the end-of-battle loading screen, which was a pretty insidious bug
        //The PM instance's nominations and killcounts are populated from the Battle Behaviour
        //and in this method we go through and make sure the nominations are valid
        public void OnPCBattleEndedResults()
        {
            //check if we care about the companion limit, and if there's room
            if (this.respect_companion_limit && Clan.PlayerClan.Companions.Count >= Clan.PlayerClan.CompanionLimit)
            {
                InformationManager.DisplayMessage(new InformationMessage(new TextObject("At maximum allowed companions. No nominations possible.").ToString(), Colors.Blue));
                return;
            }

            //Create a list of nominations and killcounts stripped of invalid entries
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

            //Finally, the list of selected characterobjects
            //up to the companion limit, if we're respecting it
            List<CharacterObject> coList;
            int mod_nominations = this.max_nominations;
            double num = rand.NextDouble();

            //If COs are in the final cut list, order them by killcount, and present them to the player
            //We reference two methods -- genInquiryElements, which creates the little presentation box for the unit,
            //and OnNomineeSelect, which takes each selected nominee and performs the "promotion"
            if (_stripped_noms.Count > 0)
            {
                coList = new List<CharacterObject>(_stripped_noms).OrderBy<CharacterObject, int>(o => _stripped_kcs[_stripped_noms.IndexOf(o)]).Reverse().ToList();
                _stripped_kcs = new List<int>(_stripped_kcs).OrderBy<int, int>(o => _stripped_kcs[_stripped_kcs.IndexOf(o)]).Reverse().ToList();

                //check if number of possible nominations would put us over the companion limit
                if (this.respect_companion_limit && (coList.Count + Clan.PlayerClan.Companions.Count) > Clan.PlayerClan.CompanionLimit)
                {
                    mod_nominations = Clan.PlayerClan.CompanionLimit - Clan.PlayerClan.Companions.Count;
                }
                else
                {
                    mod_nominations = this.max_nominations;
                }

                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Distinguished Soldiers", "Several soldiers made names for themselves in this battle. You can choose up to " + mod_nominations + " (or none, by exiting) to fight at your side as a companion.", this.GenInquiryelements(coList, _stripped_kcs), true, mod_nominations, "DONE", "RANDOM", new Action<List<InquiryElement>>(OnNomineeSelect), (Action<List<InquiryElement>>)null, ""), true);
                return;

            }

        }

        //First util function -- takes a character object list and killcount, creates a
        //corresponding list of InquiryElements showing the unit's preview and killcount tooltip
        public List<InquiryElement> GenInquiryelements(List<CharacterObject> _cos, List<int> _kills)
        {
            List<InquiryElement> _ies = new List<InquiryElement>();
            for (int q = 0; q < _cos.Count; q++)
            {
                if (MobileParty.MainParty.MemberRoster.Contains(_cos[q]))
                {
                    _ies.Add(new InquiryElement((object)_cos[q], _cos[q].Name.ToString(), new ImageIdentifier(CharacterCode.CreateFrom((BasicCharacterObject)_cos[q])), true, _kills[q].ToString() + " kills"));
                }
            }

            return _ies;

        }

        //Second util function -- Takes the list of selected inquiry elements, and feeds them through the
        //Hero-creation system
        public void OnNomineeSelect(List<InquiryElement> ies)
        {
            foreach (InquiryElement ie in ies)
            {
                //CharacterObject is actually just the "identifier", cast to a different type
                CharacterObject _co_ = (CharacterObject)(ie.Identifier);
                //We're going to try to use "over-performance", so we want to scrape the 
                //killcount from the tooltip
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
                //Finally, invoke the hero-creation, and remove the CO from the player party
                //We could do the final verification check before, but I'd rather err on
                //the side of the player getting a hero they didn't technically earn than
                //the player burning a selection accidentally
                this.PromoteUnit(_co_, kc);
                if (MobileParty.MainParty.MemberRoster.Contains(_co_))
                {
                    MobileParty.MainParty.MemberRoster.RemoveTroop(_co_);
                }
            }
        }

        //Third util function -- Simply returns whether a CO is qualified to be
        //nominated or not
        //Since end-tiers aren't uniform, we have to check if there are any upgrade targets
        //for the default branch
        public static bool IsSoldierQualified(CharacterObject co)
        {
            if (co == null)
            {
                return false;
            }
            if (PromotionManager.__instance.tier_threshold < 0)
            {
                if (co.UpgradeTargets == null || co.UpgradeTargets.Length == 0)
                {
                    return true;
                }
            }
            else
            {
                if (co.Tier >= PromotionManager.__instance.tier_threshold)
                {
                    return true;
                }
            }
            return false;
        }

        //Fourth util function -- Get a name from the provided external namelist
        //It's technically bad form to run file IO each time, but this way
        //the namelist can be modified on the fly, while the game is running
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
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (i != sel_index)
                        {
                            new_text += lines[i] + "\n";
                        }
                    }
                    File.WriteAllText(extern_namelist, new_text);
                    return o;
                }
                catch (Exception e)
                {
                    InformationManager.DisplayMessage(new InformationMessage(e.Message, Colors.Red));
                    return "ABORT";
                }

            }
            else
            {
                return "ABORT";
            }
        }

        //Fifth util function -- Get a name suffix from the mod's internal list
        //We build up a list of valid suffices and randomly pick from them at the end
        //while it's relatively incomplete, it can be fleshed out pretty easily
        //
        //TODO would probably be to make this use file IO, and maybe XML parsing to
        //allow external lists
        public string GetNameSuffix(CharacterObject co)
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
            if (co.Culture.StringId == "criminals")
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
            else if (co.Culture.StringId == "pirates")
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


        //Sixth util function -- Take a hero and nerf all their equipment
        //by applying the game's "companion" modifier
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

        //Seventh util function -- add variance to the game's main RPG traits
        //Making a hero with a "reputation" that we could potentially use
        //in the future for inter-companion (and inter-lord) conflict
        public void AddTraitVariance(Hero hero)
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
                }
            }
        }

        //Here's the primary function for this mod--
        //it takes in a CharacterObject, kill count, and option for player selection of skills
        //and creates a hero from that CO, tweaks that hero's skills and attributes,
        //and adds them to the player's party
        public void PromoteUnit(CharacterObject co, int kills = -1, bool pick_skills = true)
        {
            //Basic check against whether the CO exists
            CharacterObject nco = Game.Current.ObjectManager.GetObject<CharacterObject>(co.StringId);
            co = nco;
            if (co == null)
            {
                return;
            }
            //This set of functions attempts to populate the Hero template we want to mold into the input CharacterObject
            //We first start with more stringent criteria (e.g. first check against the Culture's wanderer templates), 
            //and if all of that has fallen through, we'll just take anything at all that matches the male/female
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

            //Past this point, we've populated the wanderer template, so we can actually create the hero
            //Using some of the game's in-built hero creation tools
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
                    specialHero.SetName(new TextObject(new_name + GetNameSuffix(co)), new TextObject(new_name));
                }
            }
            if (!using_extern_namelist || !external_name_successful)
            {
                specialHero.SetName(new TextObject(specialHero.FirstName.ToString() + GetNameSuffix(co)), specialHero.FirstName);
            }
            specialHero.Culture = co.Culture;

            specialHero.CharacterObject.TrySetPropertyValue("DefaultFormationClass", co.DefaultFormationClass);
            specialHero.CharacterObject.TrySetPropertyValue("DefaultFormationGroup", co.DefaultFormationGroup);


            specialHero.ChangeState(Hero.CharacterStates.Active);
            AddCompanionAction.Apply(Clan.PlayerClan, specialHero);
            AddHeroToPartyAction.Apply(specialHero, MobileParty.MainParty, true);
            CampaignEventDispatcher.Instance.OnHeroCreated(specialHero, false);

            AddTraitVariance(specialHero);
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
                if (MyLittleWarbandLoaded)
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
                baseline_skills[sk] = Math.Min(co.GetSkillValue(sk), 300);
                //specialHero.HeroDeveloper.SetInitialSkillLevel(sk, co.GetSkillValue(sk));
            }
            int curr_sk = 0;
            foreach (SkillObject sk in Skills.All)
            {
                curr_sk = specialHero.GetSkillValue(sk);
                specialHero.HeroDeveloper.ChangeSkillLevel(sk, baseline_skills[sk] - curr_sk);
            }

            int skp_to_assign = base_additional_skill_points + 50 * Hero.MainHero.GetSkillValue(DefaultSkills.Leadership) / leadership_points_per_50_extra_skill_points;
            if (kills > 0)
            {
                if (co.IsMounted)
                {
                    skp_to_assign += PromotionManager.__instance.skp_per_excess_kill * (kills - PromotionManager.__instance.cav_kill_threshold);
                }
                else if (co.IsRanged)
                {
                    skp_to_assign += PromotionManager.__instance.skp_per_excess_kill * (kills - PromotionManager.__instance.ran_kill_threshold);
                }
                else
                {
                    skp_to_assign += PromotionManager.__instance.skp_per_excess_kill * (kills - PromotionManager.__instance.inf_kill_threshold);
                }
            }


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
                foreach (CharacterAttribute ca in shuffled_attrs)
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
            specialHero.HeroDeveloper.UnspentAttributePoints = 0;


        }

        //Eighth and Ninth Util functions -- Assign skills to the nascent hero
        //either through player selection, or randomly
        //For player selection, we create inquiry elements for each "soft" skill,
        //and allow the player to choose several to give a skill bump to
        //Randomly, we replace player choice with a switch statement
        //
        //We also cap out at 300 to avoid... Problems...
        public void AssignSkills(Hero specialHero, int skill_points_to_assign, int num_skills_to_select, string title_prefix, string prev = " basic soldier")
        {
            //Add options only if points can be added to them in a valid way
            List<InquiryElement> iqes = new List<InquiryElement>();
            if (specialHero.GetSkillValue(DefaultSkills.Scouting) < 300)
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
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Scouting); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Scouting, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "smithing_bonus":
                            try
                            {
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Crafting); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Crafting, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "athletics_bonus":
                            try
                            {
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Athletics); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Athletics, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "riding_bonus":
                            try
                            {
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Riding); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Riding, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "tactics_bonus":
                            try
                            {
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Tactics); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Tactics, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "roguery_bonus":
                            try
                            {
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Roguery); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Roguery, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "charm_bonus":
                            try
                            {
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Charm); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Charm, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "leadership_bonus":
                            try
                            {
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Leadership); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Leadership, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "trade_bonus":
                            try
                            {
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Trade); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Trade, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "steward_bonus":
                            try
                            {
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Steward); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Steward, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "medicine_bonus":
                            try
                            {
                                diff = 300 - specialHero.GetSkillValue(DefaultSkills.Medicine); //cap out at 300
                                specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Medicine, Math.Min(skill_points_to_assign, diff));
                            }
                            catch { }
                            break;
                        case "engineering_bonus":
                            try
                            {
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
            for (int i = 0; i < num_skills_to_select; i++)
            {
                switch ((int)(MBRandom.RandomFloat * 12) + 1)
                {
                    case 1:
                        try
                        {
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Scouting, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 2:
                        try
                        {
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Crafting, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 3:
                        try
                        {
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Athletics, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 4:
                        try
                        {
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Riding, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 5:
                        try
                        {
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Tactics, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 6:
                        try
                        {
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Roguery, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 7:
                        try
                        {
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Charm, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 8:
                        try
                        {
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Leadership, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 9:
                        try
                        {
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Trade, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 10:
                        try
                        {
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Steward, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 11:
                        try
                        {
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Medicine, skill_points_to_assign);
                        }
                        catch { }
                        break;
                    case 12:
                        try
                        {
                            specialHero.HeroDeveloper.ChangeSkillLevel(DefaultSkills.Engineering, skill_points_to_assign);
                        }
                        catch { }
                        break;
                }
            }
            int diff = 0;
            foreach (SkillObject sk in Skills.All)
            {
                diff = 300 - specialHero.GetSkillValue(sk);
                if (diff < 0)
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

        //Tenth util function -- shuffle a list of any type randomly
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

        //Setting specific functions, added as event triggers

        //Recruit to hero -- if you recruit a qualified unit, turn them into a hero immediately
        public void recruit_to_hero(CharacterObject troop, int amount)
        {
            if (!IsSoldierQualified(troop) || !MobileParty.MainParty.MemberRoster.Contains(troop))
                return;
            for (int i = 0; i < amount; i++)
            {
                if (this.respect_companion_limit && Clan.PlayerClan.Companions.Count >= Clan.PlayerClan.CompanionLimit)
                { //stop giving companions if over companion limit and respecting it
                    MobileParty.MainParty.MemberRoster.RemoveTroop(troop, i);
                    return;
                }
                PromotionManager.__instance.PromoteUnit(troop);
            }
            MobileParty.MainParty.MemberRoster.RemoveTroop(troop, amount);
        }
        //And finally, for upgraded units
        public void upgrade_to_hero(CharacterObject upgradeFromTroop, CharacterObject upgradeToTroop, int number)
        {
            if (!IsSoldierQualified(upgradeToTroop))
                return;
            for (int i = 0; i < number; i++)
            {
                if (this.respect_companion_limit && Clan.PlayerClan.Companions.Count >= Clan.PlayerClan.CompanionLimit)
                { //stop giving companions if over companion limit and respecting it
                    MobileParty.MainParty.MemberRoster.RemoveTroop(upgradeToTroop, i);
                    return;
                }
                PromotionManager.__instance.PromoteUnit(upgradeToTroop);
            }
            MobileParty.MainParty.MemberRoster.RemoveTroop(upgradeToTroop, number);
        }


        //Console commands to both test out functionality, and allow players to set up

        //their own playthrough as they want:
        [CommandLineFunctionality.CommandLineArgumentFunction("uplift_soldier", "dservice")]
        public static string NewGuyCheat(List<string> strings)
        {
            int tierthresh = -1;
            if (!CampaignCheats.CheckParameters(strings, 1) || CampaignCheats.CheckHelp(strings))
                return "Usage: uplift_soldier [tier threshold = 0]";
            if (!int.TryParse(strings[0], out tierthresh))
                tierthresh = 0;
            List<CharacterObject> cos = new List<CharacterObject>();
            List<int> faux_kills = new List<int>();
            foreach (CharacterObject co in MobileParty.MainParty.MemberRoster.ToFlattenedRoster().Troops)
            {
                if (co.IsHero || co.Tier < tierthresh)
                    continue;
                cos.Add(co);
                faux_kills.Add(1337);
            }

            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Console Command", "Pick a soldier to uplift!", PromotionManager.__instance.GenInquiryelements(cos, faux_kills), true, 1, "DONE", "RANDOM", new Action<List<InquiryElement>>(PromotionManager.__instance.OnNomineeSelect), (Action<List<InquiryElement>>)null, ""), true);

            return "Dialog Generated";
        }


        //Finally, option for AI promotions -- add them to a party

        //First, we scan concluded map events. If they were a battle,
        //we spoof a determination by rolling a random number between 0-1
        //If it hits, we shove one of their qualifying troops through the
        //hero generation system
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
                            if (!(_co_ == null) && !_co_.IsHero && _co_.IsSoldier && PromotionManager.IsSoldierQualified(_co_))
                            {
                                qualified.Add(_co_);

                            }
                        }
                        if (num_comps_in_party < max_ai_comp_per_party && qualified.Count > 0)
                        {
                            this.PromoteToParty(qualified[0], p.Party.MobileParty);
                        }
                    }
                }
            }
            catch {}
        }
        public void PromoteToParty(CharacterObject co, MobileParty party)
        {
            if (co == null || party == null)
            {
                return;
            }
            Hero party_leader = party?.LeaderHero;
            if (party_leader == null)
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
            specialHero.SetName(new TextObject(specialHero.FirstName.ToString() + GetNameSuffix(co)), specialHero.FirstName);
            specialHero.Culture = co.Culture;


            specialHero.ChangeState(Hero.CharacterStates.Active);
            AddHeroToPartyAction.Apply(specialHero, party, true);
            CampaignEventDispatcher.Instance.OnHeroCreated(specialHero, false);

            AddTraitVariance(specialHero);
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

        }


        /*
         * Second half of this file concerns interactions with companions
         * that I felt the game was lacking.
         * These include options to change their name, move companions between parties, etc
         */

        //Add the dialog options to the game
        public static void AddDialogs(CampaignGameStarter campaignGameStarter)
        {
            //name change
            campaignGameStarter.AddPlayerLine("companion_change_name_start", "hero_main_options", "companion_change_name_confirm", "I want you to be known by another name...", new ConversationSentence.OnConditionDelegate(GetNamechangecondition), new ConversationSentence.OnConsequenceDelegate(GetNamechanceconsequence), 100, (ConversationSentence.OnClickableConditionDelegate)null, (ConversationSentence.OnPersuasionOptionDelegate)null);
            campaignGameStarter.AddDialogLine("companion_change_name_confirm", "companion_change_name_confirm", "hero_main_options", "That is all.", (ConversationSentence.OnConditionDelegate)null, (ConversationSentence.OnConsequenceDelegate)null, 100, (ConversationSentence.OnClickableConditionDelegate)null);

            //give companion to party
            campaignGameStarter.AddPlayerLine("companion_transfer_start", "hero_main_options", "companion_transfer_confirm", "Take these companions into your party...", new ConversationSentence.OnConditionDelegate(GetGiveCompToClanPartyCondition), new ConversationSentence.OnConsequenceDelegate(GetGiveCompToClanPartyConsequence), 100, (ConversationSentence.OnClickableConditionDelegate)null, (ConversationSentence.OnPersuasionOptionDelegate)null);
            campaignGameStarter.AddDialogLine("companion_transfer_confirm", "companion_transfer_confirm", "hero_main_options", "That is all.", (ConversationSentence.OnConditionDelegate)null, (ConversationSentence.OnConsequenceDelegate)null, 100, (ConversationSentence.OnClickableConditionDelegate)null);

            //take companion back from party
            campaignGameStarter.AddPlayerLine("companion_takeback_start", "hero_main_options", "companion_takeback_confirm", "I wish to reassign heroes in your party...", new ConversationSentence.OnConditionDelegate(GetTakeCompFromClanPartyCondition), new ConversationSentence.OnConsequenceDelegate(GetTakeCompFromClanPartyConsequence), 100, (ConversationSentence.OnClickableConditionDelegate)null, (ConversationSentence.OnPersuasionOptionDelegate)null);
            campaignGameStarter.AddDialogLine("companion_takeback_confirm", "companion_takeback_confirm", "hero_main_options", "That is all.", (ConversationSentence.OnConditionDelegate)null, (ConversationSentence.OnConsequenceDelegate)null, 100, (ConversationSentence.OnClickableConditionDelegate)null);

            //poach a companion from a defeated party
            campaignGameStarter.AddPlayerLine("enemy_comp_recruit_1", "defeated_lord_answer", "companion_poach_confirm", "After this defeat, are you sure you wouldn't rather work for me?", new ConversationSentence.OnConditionDelegate(PromotionManager.GetCapturedAIWandererCondition), (ConversationSentence.OnConsequenceDelegate)null, 100, (ConversationSentence.OnClickableConditionDelegate)null, (ConversationSentence.OnPersuasionOptionDelegate)null); //new ConversationSentence.OnClickableConditionDelegate(CanConvertWanderer)
            campaignGameStarter.AddDialogLine("enemy_comp_recruit_2", "companion_poach_confirm", "close_window", "{RECRUIT_RESPONSE}", (ConversationSentence.OnConditionDelegate)null, (ConversationSentence.OnConsequenceDelegate)null, 100, (ConversationSentence.OnClickableConditionDelegate)null);

        }

        //Name change logic --
        //A condition for whether the option will appear
        //A consequence that prompts the player for a new name
        //And a result that sets the companion's new name
        private static bool GetNamechangecondition()
        {
            return Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.Clan == Clan.PlayerClan && Hero.OneToOneConversationHero.IsPlayerCompanion;
        }
        private static void GetNamechanceconsequence()
        {
            InformationManager.ShowTextInquiry(new TextInquiryData("Create a new name: ", string.Empty, true, false, GameTexts.FindText("str_done", (string)null).ToString(), (string)null, new Action<string>(PromotionManager.ChangeHeroName), (Action)null, false), false);

        }
        private static void ChangeHeroName(string s)
        {
            Hero.OneToOneConversationHero.SetName(new TextObject(s), new TextObject(s));
        }


        //Companion transferrence logic --
        //Can you ask a companion to take other companions into their party?
        //Select who goes
        //Explicitly move them to the new party
        private static bool GetGiveCompToClanPartyCondition()
        {
            return Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.Clan == Clan.PlayerClan && (Hero.OneToOneConversationHero.IsPartyLeader);
        }
        private static void GetGiveCompToClanPartyConsequence()
        {
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Transfer Heroes", "You can select who to assign to " + Hero.OneToOneConversationHero.Name + "'s party.", PromotionManager.GenTransferList(PromotionManager.GetPlayerPartyHeroCOs()), true, PartyBase.MainParty.MemberRoster.Count, "DONE", "NOBODY", new Action<List<InquiryElement>>(PromotionManager.TransferCompsToConversationParty), (Action<List<InquiryElement>>)null, ""), true);
        }
        private static void TransferCompsToConversationParty(List<InquiryElement> ies)
        {
            MobileParty conv = Hero.OneToOneConversationHero.PartyBelongedTo;
            foreach (InquiryElement ie in ies)
            {
                CharacterObject _co_ = (CharacterObject)(ie.Identifier);
                Hero _h_ = _co_.HeroObject;

                AddHeroToPartyAction.Apply(_h_, conv, true);
            }
        }
        //Util function to create a list of heros in the player's party
        private static List<CharacterObject> GetPlayerPartyHeroCOs()
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

        //Take heros from your clan's party logic --
        //Is this a party leader of your clan you're talking to
        //Select who to steal from their party
        //Explicitly transfer
        private static bool GetTakeCompFromClanPartyCondition()
        {
            return Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.Clan == Clan.PlayerClan && (Hero.OneToOneConversationHero.IsPartyLeader);
        }
        private static void GetTakeCompFromClanPartyConsequence()
        {
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Transfer Heroes", "You can select who to take from " + Hero.OneToOneConversationHero.Name + "'s party.", PromotionManager.GenTransferList(PromotionManager.GetConversationPartyHeros(), false), true, PartyBase.MainParty.MemberRoster.Count, "DONE", "NOBODY", new Action<List<InquiryElement>>(PromotionManager.TransferCompsFromConversationParty), (Action<List<InquiryElement>>)null, ""), true);
        }
        private static void TransferCompsFromConversationParty(List<InquiryElement> ies)
        {
            foreach (InquiryElement ie in ies)
            {
                CharacterObject _co_ = (CharacterObject)(ie.Identifier);
                Hero _h_ = _co_.HeroObject;
                AddHeroToPartyAction.Apply(_h_, MobileParty.MainParty, true);
            }
        }
        //Util function -- Generates a list of InquiryElements from a list of CharacterObjects 
        public static List<InquiryElement> GenTransferList(List<CharacterObject> _cos, bool require_mainparty = true)
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
        //Util function -- Gets list of heros in the party of the hero you are conversing with
        private static List<CharacterObject> GetConversationPartyHeros()
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

        //Condition for whether a captured enemy wanderer will consider switching sides if
        //your "values" align more closely to theirs
        private static bool GetCapturedAIWandererCondition()
        {
            if (Hero.OneToOneConversationHero != null && Hero.OneToOneConversationHero.Clan != Clan.PlayerClan && Hero.OneToOneConversationHero.Occupation == Occupation.Wanderer)
            {
                //get potential relation with player
                float points = 0.0f;
                float max_contrib = 0; //track highest contribution to points
                float min_contrib = 0; //track lowest, to set rejection text
                int max_one = 0;
                int min_one = 0;
                float temp = 0;
                //calculating will like player for being better, not-calculating just vibesss
                temp = MathF.Max(0.25f * MBRandom.RandomFloatRanged(0.1f, 0.5f) * Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Calculating), 0.0f);
                if (temp > max_contrib)
                {
                    max_one = 1;
                }
                else if (temp < min_contrib)
                {
                    min_one = 1;
                }
                //impulsiveness
                temp = MathF.Max(-0.25f * MBRandom.RandomFloatRanged(0.1f, 0.5f) * Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Calculating), 0.0f);
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
                if (-temp > max_contrib)
                {
                    max_one = 2;
                }
                else if (temp < min_contrib)
                {
                    min_one = 2;
                }
                points += temp;
                //Risk-taking AI will want to join
                //risk-averse won't
                temp = 0.5f * MBRandom.RandomFloatRanged(0.1f, 0.5f) * Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Gambler);
                if (temp > max_contrib)
                {
                    max_one = 3;
                }
                else if (temp < min_contrib)
                {
                    min_one = 3;
                }
                points += temp;
                //Valourous AI will think about how glorious it could be
                temp = 0.25f * MBRandom.RandomFloatRanged(0.1f, 0.5f) * Hero.OneToOneConversationHero.GetTraitLevel(DefaultTraits.Valor);
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

    }
}
