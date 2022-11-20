//TODO
//
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.DotNet;
using System.Linq;
using TaleWorlds.CampaignSystem.Party;

namespace DistinguishedService
{
    internal class DSBattleLogic : MissionLogic
    {

        //Instantiate by setting instance to provided pm
        public DSBattleLogic()
        {
            if (PromotionManager.__instance == null)
                return;

            PromotionManager.__instance.nominations = new List<CharacterObject>();
            PromotionManager.__instance.killcounts = new List<int>();
        }

        /*public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            //base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, blow);
        }*/

        private int totalKillCount()
        {
            int total_kill_count = 0;
            foreach (Agent ag in Mission.Current.PlayerTeam.ActiveAgents)
            {
                if (!ag.IsHero) //only count mooks
                {
                    total_kill_count += ag.KillCount;
                }
            }
            return total_kill_count;
        }

        //Get a list of all kill counts
        //for percentile calculation
        private List<float> kill_counts()
        {
            List<float> kills = new List<float>();
            foreach (Agent ag in Mission.Current.PlayerTeam.ActiveAgents)
            {
                if (!ag.IsHero) //only count mooks
                {
                    kills.Add((float)ag.KillCount);
                }
            }
            return kills;
        }

        //Calculate the value of a percentile
        public static double Percentile(IEnumerable<float> seq, double percentile)
        {
            var elements = seq.ToArray();
            Array.Sort(elements);
            double realIndex = percentile * (elements.Length - 1);
            int index = (int)realIndex;
            double frac = realIndex - index;
            if (index + 1 < elements.Length)
                return elements[index] * (1 - frac) + elements[index + 1] * frac;
            else
                return elements[index];
        }

        //We override showbattleresults because it fires before any loading screen 
        //has had the chance to pop up... That has previously caused a horrible bug
        //where options would appear *behind* the loading screen,
        //causing infinite hang
        public override void ShowBattleResults()
        {
            if (PromotionManager.__instance == null)
                return;

            //reset nominations and killcount
            PromotionManager.__instance.nominations.Clear();
            PromotionManager.__instance.killcounts.Clear();

            //drop out if the mission is incorrect
            if(Mission.Current.Mode == MissionMode.Conversation || Mission.Current.Mode == MissionMode.StartUp)
                return;

            if (Mission.Current.CombatType == Mission.MissionCombatType.ArenaCombat)// || !Mission.Current.IsFieldBattle)
                return;

            //sum the total kill count in case nothing happened...
            int total_kill_count = totalKillCount();

            //if no chance for glory
            if (total_kill_count <= 0)
                return;

            //Get kill number at the performance percentile
            float perc_kills = (float)Percentile(kill_counts(), PromotionManager.__instance.outperform_percentile);

            int kill_thresh = 0;
            //Now, go through each agent in the player's team, skipping heros
            //and check their performance against the thresholds
            //Technically this can hit false positives in certain situations,
            //but those edge cases are harder to fix and more rare,
            //so we'll just settle for something that works for now...
            foreach (Agent ag in Mission.Current.PlayerTeam.ActiveAgents)
            {
                if (ag.IsHero)
                    continue;

                CharacterObject co = CharacterObject.Find(ag.Character.StringId);
                //Check that the player has one of these in their party
                if (!PartyBase.MainParty.MemberRoster.Contains(co) || !MobileParty.MainParty.MemberRoster.Contains(co))
                    continue;

                if (co.IsRanged)
                {
                    kill_thresh = PromotionManager.__instance.ran_kill_threshold;
                }
                else if (co.IsMounted)
                {
                    kill_thresh = PromotionManager.__instance.cav_kill_threshold;
                }
                else
                {
                    kill_thresh = PromotionManager.__instance.inf_kill_threshold;
                }

                //proceed if we're not considering average kills, OR agent has more than average (rounded up)
                bool proceed = (PromotionManager.__instance.outperform_percentile <= 0 || ag.KillCount > MathF.Ceiling(perc_kills));
                //If they qualify, add them and their kill count to the PM list
                if (proceed && ag.KillCount >= kill_thresh) {
                    if(AddNewGuy.is_soldier_qualified(co))
                    {
                        //if an agent has enough kills and is high enough tier, nominate them
                        PromotionManager.__instance.nominations.Add(co);
                        PromotionManager.__instance.killcounts.Add(ag.KillCount);
                    }
                }
            }
            //finally, tell the PM to handle nominations
            PromotionManager.__instance.OnPCBattleEnded_results();
        }
        
        
    }

    //Class to add behaviour to ongoing battle
    internal class DSBattleBehavior : CampaignBehaviorBase
    {
        public DSBattleBehavior()
        {
        }
        public override void RegisterEvents()
        {
            CampaignEvents.OnMissionStartedEvent.AddNonSerializedListener((object)this, new Action<IMission>(this.FindBattle));
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener((object)this, new Action<CampaignGameStarter>(PromotionManager.addDialogs));
        }

        public override void SyncData(IDataStore dataStore)
        {
        }

        public void FindBattle(IMission misson)
        {
            if (((Mission)misson).CombatType > Mission.MissionCombatType.Combat || !((NativeObject)Mission.Current.Scene != (NativeObject)null))
                return;
            if (Mission.Current.HasMissionBehavior<DSBattleLogic>())
            {
                return; //don't add more
            }
            Mission.Current.AddMissionBehavior(new DSBattleLogic());
        }
    }
}
