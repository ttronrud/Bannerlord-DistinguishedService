/*
 * Author: Thor Tronrud
 * NameList.cs:
 * 
 * A class that holds the various suffices that we can add
 * to a newly-promoted hero's name. Inspirations are... Varied.
 * 
 * A possibility to "vibe" score names to assign based on the 
 * hero's traits is something I want to sketch out/add in 
 * the future, probably in a larger hero-oriented update.
 */

using System.Collections.Generic;

namespace DistinguishedService
{
    public static class NameList
    {
        //name suffixes
        public static List<string> infantry_suff = new List<string>()
        {
            " the Blade", " the Swordsman", " the Brave", " the Marauder", " the Loyal", " the Strong", " the Red", " the Butcher", " the Knife", " the Shark", " the Scythe", " the Anvil", " Scarlet"
        };
        public static List<string> cavalry_suff = new List<string>()
        {
            " the Rider", " the Horseman", " the Bold", " the Vanguard", " the Centaur", " the Stallion", " the Gallant", " the Butcher", " the Hammer", " the Swift", " the Pinder"
        };
        public static List<string> archer_suff = new List<string>()
        {
            " the Ranger", " the Bowman", " the Sharpshooter", " the Sniper", " the Hunter", " the Merry", " the Scarlet", " the Miller's Son", "-a-Dale", " Greenleaf", " of Doncaster"
        };
        //Regional Suffixes
        public static List<string> battanian_suff = new List<string>()
        {
            " an Fhàsach", " an Làidir", " an Gaisgeach"
        };
        public static List<string> imperial_suff = new List<string>()
        {
            " Vulpis", " Fortis", " Callidus"
        };
        public static List<string> sturgian_suff = new List<string>()
        {
            " det Kråke", " det Rev", " av Nord"
        };
        public static List<string> aserai_suff = new List<string>()
        {
            " al janubi", " al sayf", " al sharasa"
        };
        public static List<string> vlandian_suff = new List<string>()
        {
            " the Bold", " the Lion", " the Little"
        };
        public static List<string> khuzait_suff = new List<string>()
        {
            " Ukhaantai", " Khurdan", " Yamaa"
        };
        //criminals
        public static List<string> criminal_suff = new List<string>()
        {
            " the Ragged", " the Shiv", " Slicer", " the Plunderer", " the Mad", " Racoon", " the Overlord", " the Slayer", " the Boss", " of the Filth", " the Bonebreaker", " Skullsmasher", " the Enslaver", " the Captain"
        };
        public static List<string> criminal2_suff = new List<string>()
        {
            " the Smuggler", " the Thief", " Conman", " the Fence", " Sly", "the Contrabander", " the Swindler", " the Pilferer"
        };
        public static List<string> criminal3_suff = new List<string>()
        {
            " the Mastermind", " the Assassin", " the Spy", " the Scoutmaster", " the Tracker", " the Pathfinder", " the Hidden", " the Bounty Hunter", " Fox"
        };
        public static List<string> pirates_suff = new List<string>()
        {
            " the Smuggler", " the Sailor", " of the Waves", " the Peg Leg", " Ale-Drinker", " the Privateer", " the Seafarer", " Blackbeard"
        };
        public static List<string> shiftingsands_suff = new List<string>()
        {
            " the Doc", " Bloodscalpel", " Sawbones", " the Chemist", " the Limbchopper"
        };
    }

    public static class UnitTypeList
    {
        public static List<string> noble_units = new List<string>()
        {
            "aserai_youth", "aserai_tribal_horseman", "aserai_faris", "aserai_veteran_faris", "aserai_vanguard_faris", "battanian_highborn_youth",
            "battanian_highborn_warrior", "battanian_hero", "battanian_fian", "battanian_fian_champion", "imperial_vigla_recruit",
            "imperial_equite", "imperial_heavy_horseman", "imperial_cataphract", "imperial_elite_cataphract", "khuzait_noble_son",
            "khuzait_qanqli", "khuzait_torguud", "khuzait_kheshig", "khuzait_khans_guard", "sturgian_warrior_son", "varyag",
            "varyag_veteran", "druzhinnik", "druzhinnik_champion", "vlandian_squire", "vlandian_gallant", "vlandian_knight",
            "vlandian_champion", "vlandian_banner_knight"
        };
    }
}
