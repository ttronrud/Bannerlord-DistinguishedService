namespace DistinguishedService
{
    public class Settings
    {
        public int up_front_cost { get; set; }
        public int tier_threshold { get; set; }
        public int base_additional_skill_points { get; set; }
        public int leadership_points_per_50_extra_skill_points { get; set; }
        public int inf_kill_threshold { get; set; }
        public int cav_kill_threshold { get; set; }
        public int ran_kill_threshold { get; set; }
        public float outperform_percentile { get; set; }
        public int max_nominations { get; set; }
        public bool upgrade_to_hero { get; set; }
        public bool fill_in_perks { get; set; }
        public bool respect_companion_limit { get; set; }
        public int bonus_companion_slots_base { get; set; }
        public int bonus_companion_slots_per_clan_tier { get; set; }

        public float companion_extra_lethality { get; set; }
        public float ai_promotion_chance { get; set; }
        public int number_of_skill_bonuses { get; set; }

        public int number_of_skill_rounds { get; set; }

        public bool select_skills_randomly { get; set; }

        public bool remove_tavern_companions { get; set; }

        public bool cull_ai_companions_on_defeat { get; set; }
        public bool disable_caution_text { get; set; }

        public int skillpoints_per_excess_kill { get; set; }
        public int max_ai_companions_per_party { get; set; }

        public bool NAMES_FROM_EXTERNAL_FILE { get; set; }
        public string EXTERNAL_NAME_FILE { get; set; }

        public Settings()
        {
            this.up_front_cost = 0;
            this.tier_threshold = 4;
            this.base_additional_skill_points = 150;
            this.leadership_points_per_50_extra_skill_points = 100;
            this.number_of_skill_bonuses = 3;
            this.inf_kill_threshold = 5;
            this.cav_kill_threshold = 5;
            this.ran_kill_threshold = 5;
            this.outperform_percentile = 0.68f;
            this.max_nominations = 1;
            this.upgrade_to_hero = false;
            this.fill_in_perks = false;
            this.respect_companion_limit = false;
            this.bonus_companion_slots_base = 3;
            this.bonus_companion_slots_per_clan_tier = 2;
            this.companion_extra_lethality = 0;
            this.ai_promotion_chance = 0.001f;
            this.remove_tavern_companions = true;
            this.cull_ai_companions_on_defeat = true;
            this.disable_caution_text = false;
            this.skillpoints_per_excess_kill = 25;
            this.NAMES_FROM_EXTERNAL_FILE = false;
            this.EXTERNAL_NAME_FILE = "external_namelist.txt";
            this.max_ai_companions_per_party = 1;
            this.number_of_skill_rounds = 2;
            this.select_skills_randomly = false;
        }
    }
}
