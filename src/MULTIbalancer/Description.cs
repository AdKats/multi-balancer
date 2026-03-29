/* MULTIbalancer — Procon Plugin
   Copyright 2013, by PapaCharlie9

   Permission is hereby granted, free of charge, to any person or organization
   obtaining a copy of the software and accompanying documentation covered by
   this license (the "Software") to use, reproduce, display, distribute,
   execute, and transmit the Software, and to prepare derivative works of the
   Software, and to permit third-parties to whom the Software is furnished to
   do so, without restriction.

   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
   FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. IN NO EVENT
   SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING THE SOFTWARE BE LIABLE
   FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE,
   ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
   DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using PRoCon.Core;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;

namespace PRoConEvents
{

    static class MULTIbalancerUtils
    {
        public static Boolean IsEqual(MULTIbalancer lhs, MULTIbalancer.PresetItems preset)
        {
            MULTIbalancer rhs = new MULTIbalancer(preset);
            return (lhs.CheckForEquality(rhs));
        }

        public static void UpdateSettingsForPreset(MULTIbalancer lhs, MULTIbalancer.PresetItems preset)
        {
            try
            {
                MULTIbalancer rhs = new MULTIbalancer(preset);

                lhs.DebugWrite("UpdateSettingsForPreset to " + preset, 6);

                lhs.OnWhitelist = rhs.OnWhitelist;
                lhs.OnFriendsList = rhs.OnFriendsList;
                lhs.ApplyFriendsListToTeam = rhs.ApplyFriendsListToTeam;
                lhs.TopScorers = rhs.TopScorers;
                lhs.SameClanTagsInSquad = rhs.SameClanTagsInSquad;
                lhs.SameClanTagsInTeam = rhs.SameClanTagsInTeam;
                lhs.SameClanTagsForRankDispersal = rhs.SameClanTagsForRankDispersal;
                lhs.LenientRankDispersal = rhs.LenientRankDispersal;
                lhs.MinutesAfterJoining = rhs.MinutesAfterJoining;
                lhs.JoinedEarlyPhase = rhs.JoinedEarlyPhase;
                lhs.JoinedMidPhase = rhs.JoinedMidPhase;
                lhs.JoinedLatePhase = rhs.JoinedLatePhase;

                lhs.EarlyPhaseTicketPercentageToUnstack = rhs.EarlyPhaseTicketPercentageToUnstack;
                lhs.MidPhaseTicketPercentageToUnstack = rhs.MidPhaseTicketPercentageToUnstack;
                lhs.LatePhaseTicketPercentageToUnstack = rhs.LatePhaseTicketPercentageToUnstack;

                lhs.EarlyPhaseBalanceSpeed = rhs.EarlyPhaseBalanceSpeed;
                lhs.MidPhaseBalanceSpeed = rhs.MidPhaseBalanceSpeed;
                lhs.LatePhaseBalanceSpeed = rhs.LatePhaseBalanceSpeed;

                lhs.ForbidSwitchingAfterAutobalance = rhs.ForbidSwitchingAfterAutobalance;
                lhs.ForbidSwitchingToWinningTeam = rhs.ForbidSwitchingToWinningTeam;
                lhs.ForbidSwitchingToBiggestTeam = rhs.ForbidSwitchingToBiggestTeam;
                lhs.ForbidSwitchingAfterDispersal = rhs.ForbidSwitchingAfterDispersal;
                lhs.EnableImmediateUnswitch = rhs.EnableImmediateUnswitch;

            }
            catch (Exception) { }
        }

        public static Boolean EqualArrays(Double[] lhs, Double[] rhs)
        {
            if (lhs == null && rhs == null) return true;
            if (lhs == null || rhs == null) return false;
            if (lhs.Length != rhs.Length) return false;

            for (Int32 i = 0; i < lhs.Length; ++i)
            {
                if (lhs[i] != rhs[i]) return false;
            }
            return true;
        }

        public static Boolean EqualArrays(MULTIbalancer.Speed[] lhs, MULTIbalancer.Speed[] rhs)
        {
            if (lhs == null && rhs == null) return true;
            if (lhs == null || rhs == null) return false;
            if (lhs.Length != rhs.Length) return false;

            for (Int32 i = 0; i < lhs.Length; ++i)
            {
                if (lhs[i] != rhs[i]) return false;
            }
            return true;
        }

        public static String ArrayToString(Double[] a)
        {
            String ret = String.Empty;
            Boolean first = true;
            if (a == null || a.Length == 0) return ret;
            for (Int32 i = 0; i < a.Length; ++i)
            {
                if (first)
                {
                    ret = a[i].ToString("F0");
                    first = false;
                }
                else
                {
                    ret = ret + ", " + a[i].ToString("F0");
                }
            }
            return ret;
        }

        public static String ArrayToString(MULTIbalancer.Speed[] a)
        {
            String ret = String.Empty;
            Boolean first = true;
            if (a == null || a.Length == 0) return ret;
            for (Int32 i = 0; i < a.Length; ++i)
            {
                if (first)
                {
                    ret = Enum.GetName(typeof(MULTIbalancer.Speed), a[i]);
                    first = false;
                }
                else
                {
                    ret = ret + ", " + Enum.GetName(typeof(MULTIbalancer.Speed), a[i]);
                }
            }
            return ret;
        }

        public static Double[] ParseNumArray(String s)
        {
            Double[] nums = new Double[3] { -1, -1, -1 }; // -1 indicates a syntax error
            if (String.IsNullOrEmpty(s)) return nums;
            if (!s.Contains(",")) return nums;
            String[] strs = s.Split(new Char[] { ',' });
            if (strs.Length != 3) return nums;
            for (Int32 i = 0; i < nums.Length; ++i)
            {
                Boolean parsedOk = Double.TryParse(strs[i], out nums[i]);
                if (!parsedOk)
                {
                    nums[i] = -1;
                    return nums;
                }
            }
            return nums;
        }

        public static MULTIbalancer.Speed[] ParseSpeedArray(MULTIbalancer plugin, String s)
        {
            MULTIbalancer.Speed[] speeds = new MULTIbalancer.Speed[3] {
            MULTIbalancer.Speed.Adaptive,
            MULTIbalancer.Speed.Adaptive,
            MULTIbalancer.Speed.Adaptive
        };
            if (String.IsNullOrEmpty(s) || !s.Contains(","))
            {
                if (s == null) s = "(null)";
                plugin.ConsoleWarn("Bad balance speed setting: " + s);
                return speeds;
            }
            String[] strs = s.Split(new Char[] { ',' });
            if (strs.Length != 3)
            {
                plugin.ConsoleWarn("Wrong number of speeds, should be 3, separated by commas: " + s);
                return speeds;
            }
            for (Int32 i = 0; i < speeds.Length; ++i)
            {
                try
                {
                    speeds[i] = (MULTIbalancer.Speed)Enum.Parse(typeof(MULTIbalancer.Speed), strs[i]);
                }
                catch (Exception)
                {
                    plugin.ConsoleWarn("Bad balance speed value: " + strs[i]);
                    speeds[i] = MULTIbalancer.Speed.Adaptive;
                }
            }
            return speeds;
        }

        public static String ConvertHTMLToVBCode(String html)
        {
            if (String.IsNullOrEmpty(html)) return String.Empty;

            /* Normalization */

            // make all markup be lowercase
            String norm = Regex.Replace(html, @"<[^>=]+[>=]", delegate (Match match)
            {
                return match.Value.ToLower();
            });
            // make all entity refs be lowercase
            norm = Regex.Replace(norm, @"&[^;]+;", delegate (Match match)
            {
                return match.Value.ToLower();
            });

            StringBuilder tmp = new StringBuilder(norm);
            //tmp.Replace("\r", String.Empty);

            /* Markup deletions */

            tmp.Replace("<p>", String.Empty);
            tmp.Replace("</p>", String.Empty);

            /* Markup replacements */

            tmp.Replace("<h1>", "[SIZE=5]");
            tmp.Replace("</h1>", "[/SIZE]\n[HR][/HR]");
            tmp.Replace("<h2>", "[SIZE=4][B][COLOR=#0000FF]");
            tmp.Replace("</h2>", "[/COLOR][/B][/SIZE]\n[HR][/HR]");
            tmp.Replace("<h3>", "[SIZE=3][B]");
            tmp.Replace("</h3>", "[/B][/SIZE]");
            tmp.Replace("<h4>", "[B]");
            tmp.Replace("</h4>", "[/B]");

            tmp.Replace("<small>", "[INDENT][SIZE=2][FONT=Arial Narrow]");
            tmp.Replace("</small>", "[/FONT][/SIZE][/INDENT]");
            tmp.Replace("<font color", "[COLOR"); // TODO - be smarter about font tag
            tmp.Replace("</font>", "[/COLOR]"); // TODO - be smarter about font tag

            tmp.Replace("<ul>", "[LIST]");
            tmp.Replace("</ul>", "[/LIST]");
            tmp.Replace("<li>", "[*]");
            tmp.Replace("</li>", String.Empty);

            tmp.Replace("<table>", "[TABLE=\"class: grid\"]"); // TODO - be smarter about table tag
            tmp.Replace("<table border='0'>", "[TABLE=\"class: grid\"]");
            tmp.Replace("</table>", "[/TABLE]");
            tmp.Replace("<tr>", "[TR]\n");
            tmp.Replace("</tr>", "[/TR]");
            tmp.Replace("<td>", "[TD]");
            tmp.Replace("</td>", "[/TD]\n");

            tmp.Replace("<a href=", "[U][URL="); // TODO - be smarter about anchors
            tmp.Replace("</a>", "[/URL][/U]"); // TODO - be smarter about anchors

            tmp.Replace("<pre>", "[CODE]");
            tmp.Replace("</pre>", "[/CODE]");

            tmp.Replace("<i>", "[I]");
            tmp.Replace("</i>", "[/I]");
            tmp.Replace("<b>", "[B]");
            tmp.Replace("</b>", "[/B]");
            tmp.Replace("<hr>", "[HR]");
            tmp.Replace("</hr>", "[/HR]");
            tmp.Replace("<br>", String.Empty);
            tmp.Replace("</br>", "\n");

            // Must do this before entity ref replacement
            tmp.Replace("<", "[");
            tmp.Replace(">", "]");

            /* Entity ref replacements */

            tmp.Replace("&amp;", "&");
            tmp.Replace("&nbsp;", " ");
            tmp.Replace("&quot;", "\"");
            tmp.Replace("&apos;", "'");
            tmp.Replace("&lt;", "<");
            tmp.Replace("&gt;", ">");

            /* Done */

            return tmp.ToString();
        }

        #region HTML_DOC
        public const String HTML_DOC = @"
<h1>Multi-Balancer &amp; Unstacker, including SQDM</h1>
<p>For BF3, BF4 and BFHL, this plugin does live round team balancing and unstacking for all game modes, including Squad Deathmatch (SQDM).</p>

<h3>Acknowledgments</h3>
<p>This plugin would not have been possible without the help and support of these individuals and communities:<br></br>
<small>myrcon.com staff, [C2C]Blitz, [FTB]guapoloko, [Xtra]HexaCanon, [11]EBassie, Firejack, [IAF]SDS, dyn, Jaythegreat1, ADKGamers, AgentHawk, TreeSaint, Taxez, PatPgtips, Hutchew, LumpyNutz, popbndr, tarreltje, 24Flat, [Oaks]kcuestag ... and many others</small></p>

<h3>BF4 Update</h3>
<p>The following features do not yet work for BF4:
<ul>
<li><b>Official mode</b>: this plugin <b>WILL NOT WORK</b> on Official mode servers -- due to admin.movePlayer being disabled on Official mode.</li>
<li><b>Battlelog Cache</b>: needs to be updated to BF4.</li>
</ul></p>

<h2>NOTICE</h2>
<p>This plugin is free to use, forever. Support is provided on a voluntary basic, when time is available, by the author and the user community. Use at your own risk, no guarantees are made or implied (complete notice text is in the source code). Some of the code in this plugin (Battlelog and BattlelogCache code, plugin framework, other odds &amp; ends) was directly derived from Insane Limits by micovery. Inspiration for the plugin settings came from TrueBalancer by Panther and all of the members of the design discussion group, some of whom are listed above in the acknowledgments.</p>

<p><b>Section 7 of settings is intentionally not defined.</b></p>

<h2>Description</h2>
<p>This plugin performs several automated operations:
<ul>
<li>Team balancing for all modes</li>
<li>Unstacking a stacked team</li>
<li>Unswitching players who team switch</li>
</ul></p>

<p>This plugin only moves players when they die. No players are killed by admin to be moved, with the single exception of players who attempt to switch teams when team switching is not allowed -- those players may be admin killed before being moved back to their original team. This plugin also monitors new player joins and if the game server would assign a new player to the wrong team (a team with 30 players when another team only has 27 players), the plugin will <i>reassign</i> the player to the team that needs players for balance. This all happens before a player spawns, so they will not be aware that they were reassigned.</p>

<h3>Quick Start</h3>
<p>Don't want to spend a lot of time learning all of the settings for this plugin? Just follow these quick start steps:

<p>1) Select a <b>Preset</b> at the top of the plugin settings (<b>NOTE</b>: In all of the following presets, references to 'unstack teams' depend on the <b>Enable Unstacking</b> setting -- leave that set to False unless you are absolutely sure you want to use unstacking):
<table>
<tr><td><b>Standard</b></td><td>Autobalance and unstack teams, good for most server configurations</td></tr>
<tr><td><b>Aggressive</b></td><td>Autobalance and unstack teams quickly, moving lots of players in a short amount of time</td></tr>
<tr><td><b>Passive</b></td><td>Autobalance and unstack teams slowly, moving few players over a long period of time</td></tr>
<tr><td><b>Intensify</b></td><td>Focus on keeping teams evenly matched for a level playing field and an intense game</td></tr>
<tr><td><b>Retain</b></td><td>Focus on reducing rage quitting by keeping teams balanced, but refrain from too many player moves</td></tr>
<tr><td><b>BalanceOnly</b></td><td>Disable team unstacking, only move for autobalance</td></tr>
<tr><td><b>UnstackOnly</b></td><td>Disable autobalancing, only move to unstack teams</td></tr>
<tr><td><b>None</b></td><td>Custom plugin settings (this is automatically selected if you change settings controlled by <b>Presets</b>)</td></tr>
</table>
<b><font color=#FF0000>Standard, Retain, and BalanceOnly are recommended to admins new to this plugin.</font></b> Aggressive and Intensify are <b>not </b>recommended for admins new to this plugin.</p>

<p>2) Review plugin section <b>5. Messages</b> and change any messages you don't like.</p>

<p>3) Find your game mode in Section 8 and review the settings. Adjust the <b>Max Players</b> and <b>Definition Of ...</b> settings as needed. Or, <b>Enable Settings Wizard</b> in Section 0, <i>fill in the form that is displayed</i>, and then change <b>Apply Settings Changes</b> to True, to have the plugin set up your per-mode settings automatically.</p>

<p>4) That's it! You are good to go.</p>

<h3>FAQ</h3>

<p><a href='https://forum.myrcon.com/showthread.php?6054'>Go here for Frequently Asked Questions and more in-depth descriptions of settings and how to use them</a>. The descriptions below are intended as quick reference material, to remind you about things you already understand. For more in-depth understanding of what they mean and how they work, see the FAQ or ask questions in this thread.</p>

<h2>Concepts</h2>
<p>This plugin recognizes that a game round has a natural pattern and flow that depends on several factors. Play during the very beginning of a round is different from the very end. Play when the server is nearly empty is different from when the server is nearly full. The natural flow of a round of Conquest is very different from the flow of a game of Rush. Strong (good) players are not interchangeable with weak (bad) players. So with all these differences, how can one set of settings cover all of those different situations? They can't. So this plugin allows you to configure different settings for each combination of factors. The primary factors and concepts are described in the sections that follow.</p>

<h3>Round Phase</h3>
<p>To configure the factor of time, each round is divided into three time phases: <b>Early</b>, <b>Mid</b> (for Mid-phase), and <b>Late</b>. You define the phase based on ticket counts (or in the case of CTF or Carrier Assault, time in minutes) from the start of the round and the end of the round. You may define different settings for different modes, e.g., for <i>Conquest Large</i> you might define the early phase to be the first 200 tickets after the round starts, but for <i>Team Deathmatch</i> you might set early phase to be after the first 25 kills.</p>

<h3>Population</h3>
<p>To configure the factor of number of players, each round is divivded into three population levels: <b>Low</b>, <b>Medium</b>, and <b>High</b>. You define the population level based on total number of players in the server.</p>

<h3>Game Mode</h3>
<p>To configure the factor of game mode, each game mode is grouped into similar per-mode settings. For example, Conquest Large and Conquest Assault Large are grouped together as <b>Conquest Large</b>.</p>

<h3>Exclusions</h3>
<p>There are certain types of players that should never be moved for autobalance. You define those players with exclusions. For example, you can arrange for everyone on your reserved slots lists to be whitelisted so that they are ignored by this plugin.</p>

<h3>Balance Speed</h3>
<p>The aggressiveness with which the balancer selects players to move is controled by the speed names:
<table border='0'>
<tr><td>Stop</td><td>No balancing, no players are selected to move</td></tr>
<tr><td>Slow</td><td>Few players are selected to move, all exclusions are applied, whether they are enabled by you or not</td></tr>
<tr><td>Fast</td><td>Many players are selected to move, no exclusions are applied, whether they are enabled by you or not</td></tr>
<tr><td>Adaptive</td><td>Starts out slow; if teams remain unbalanced, gradually selects more players to move; if teams are still unbalanced after <b>Seconds Until Adaptive Speed Becomes Fast</b>, many players are selected, etc.</td></tr>
<tr><td>Unstack</td><td>Do unstacking only, no balancing. May swap players when teams are not in balance.</td></tr>
</table></p>

<h3>Definition of Strong</h3>
<p>To configure the selection of strong players and weak players, you choose a definition for strong determined from:
<table border='0'>
<tr><td>Round Score</td><td> </td></tr>
<tr><td>Round SPM</td><td>Battlelog SPM</td></tr>
<tr><td>Round Kills</td><td> </td></tr>
<tr><td>Round KDR</td><td>Battlelog KDR</td></tr>
<tr><td>Player Rank</td><td> </td></tr>
<tr><td>Round KPM</td><td>Battlelog KPM</td></tr>
</table></p>

<h3>Ticket Percentage (Ratio)</h3>
<p>The ticket percentage ratio is calculated by taking the tickets of the winning team and dividing them by the tickets of the losing team, expressed as a percentage. For example, if the winning team has 550 tickets and the losing team has 500 tickets, the ticket percentage ratio is 110. This ratio is used to determine when teams are stacked. If the ticket percentage ratio exceeds the level that you set, unstacking swaps will begin.</p>

<h3>Unstacking</h3>
<p>Stacking refers to one team having more strong players than the other team. The result of stacked teams is lopsided wins and usually rage quitting from the losing team or attempts to switch to the winning team. If unstacking is enabled and the <b>Ticket Percentage (Ratio)</b> is exceeded, the plugin will attempt to unstack teams. To unstack teams, a strong player is selected from the winning team and is moved to the losing team. Then, a weak player is selected from the losing team and moved to the winning team. This is repeated until the round ends, or teams become unbalanced, or <b>Max&nbsp;Unstacking&nbsp;Swaps&nbsp;Per&nbsp;Round</b> is reached, whichever comes first.</p>

<h3>Merge Files</h3>
<p>A merge file is an external file that you can use to specify a list setting, such as <b>Whitelist</b>. An external file is convenient if you have long lists or if you share the same list across multiple game servers. The file is specified as <b>&lt;</b><i>filename.ext</i> on the first line of the list, with no whitespace. The contents of the file should be UTF-8 text, using the same contents and syntax as the list it will be merged with. The file should be stored in the procon/Configs folder. You can store as many differently named files there as you want, but each list can only use one merge file at a time.</p>

<h2>Settings</h2>
<p>Each setting is defined below. Settings are grouped into sections.</p>

<h3>0 - Presets</h3>
<p>See the <b>Quick Start</b> section above.</p>

<p><b>Enable Unstacking</b>: True or False, default False. Enables the per-mode unstacking features described in sections 3 and 8. Setting to False will not reset individual unstacking settings, it just disables the unstacking-related settings from operating and hides per-mode settings that are relevant only to unstacking. Setting to True enables all of your untacking-related settings.</p>

<p><b>Enable Settings Wizard</b>: True or False, default False. If set to True, the plugin will automatically change your per-mode settings based on some basic information that you provide. Several additional settings are displayed. The first is <b>Which Mode</b>. Select the mode you want to apply changes to. Fill in the rest of the settings below <b>Which Mode</b>; they are self-explanatory. When you are done, change the <b>Apply Settings Changes</b> from False to True. The changes will be applied, information for review will be displayed in the plugin.log window, and the wizard will set itself to False and hide itself again.</p>

<h3>1 - Settings</h3>
<p>These are general settings.</p>

<p><b>Debug Level</b>: Number from 0 to 9, default 2. Sets the amount of debug messages sent to plugin.log. Status messages for the state of the plugin may be seen at level 4 or higher. Complete details for operation of the plugin may be seen at level 7 or higher. When a problem with the plugin needs to be diagnosed, level 7 will often be required. Setting the level to 0 turns off all logging messages.</p>

<p><b>Maximum Server Size</b>: Number from 8 to 70, default 64. Maximum number of slots on your game server, regardless of game mode.</p>

<p><b>Enable Battlelog Requests</b>: True or False, default True. Enables making requests to Battlelog and uses BattlelogCache if available. Used to obtain clan tag for players and optionally, overview stats SPM, KDR, and KPM.</p>

<p><b>Which Battlelog Stats</b>: ClanTagOnly, AllTime, or Reset. Selects the type of Battlelog stats you want to use, clan tag only, All-Time or Reset stats.</p>

<p><b>Maximum Request Rate</b>: Number from 1 to 15, default 10. If <b>Enable Battlelog Requests</b> is set to True, defines the maximum number of Battlelog requests that are sent every 20 seconds.</p>

<p><b>Wait Timeout</b>: Number from 15 to 90, default 30. If <b>Enable Battlelog Requests</b> is set to True, defines the maximum number of seconds to wait for a reply from Battlelog or BattlelogCache before giving up.</p>

<p><b>Unlimited Team Switching During First Minutes Of Round</b>: Number greater than or equal to 0, default 5. Starting from the beginning of the round, this is the number of minutes that players are allowed to switch teams without restriction. After this time is expired, the plugin will prevent team switching that unbalances or stacks teams. The idea is to enable friends who were split up during the previous round due to autobalancing or unstacking to regroup so that they can play together this round. However, players who switch teams during this period are not excluded from being moved for autobalance or unstacking later in the round, unless some other exclusion applies them.</p>

<p><b>Seconds Until Adaptive Speed Becomes Fast</b>: Number of seconds greater than or equal to 30, default 180. If the autobalance speed is Adaptive and the autobalancer has been active for more than the specified number of seconds, the speed will be forced to Fast. This insures that teams don't remain unbalanced too long if Adaptive speed is not sufficient to move players.</p>

<p><b>Reassign New Players</b>: True or False, default True. This is a trade-off setting, each choice has something good and something bad associated with it. If set to True, new players joining the server are reassigned to the team that needs help before the player's first spawn -- they will not be aware that they were moved, but this may cancel a Battlelog Join on Friend that the player wanted. If set to False, Join on Friend will be respected, but your server may have unbalanced teams for a longer period of time.</p>

<p><b>Enable Admin Kill For Fast Balance</b>: True or False, default False. Enables forced moves using admin kills when teams are grossly unbalanced. All exclusions are ignored except for <b>On Whitelist</b> and <b>Minutes After Being Moved</b>. If the setting is True and teams are 4 or more players apart (3 if population is Low) and the speed is not Stop, live players will be selected and admin killed and then moved. The selection of who is forced to move is controlled by <b>Select Fast Balance By</b>.</p>

<p><b>Select Fast Balance By</b>: Newest, Weakest or Random; default Newest. Only visible if <b>Enable Admin Kill For Fast Balance</b> is True. Determines which live player is force moved for Fast balance. <i>Newest</i> is the player that has been in the server the least amount of time. <i>Weakest</i> is the player with the lowest value as defined by per-mode <b>Determine Strong Players By</b>, e.g., for RoundScore the player with the lowest point score is selected. <i>Random</i> is a player selected at random.</p>

<p><b>Enable In-Game Commands</b>: True or False, default True. Enable <b>@mb</b> in-game commands. Most commands allow admins to change settings in the plugin without needing to leave the game. See the plugin thread for details or type <b>@mb help</b> in-game.</p>

<!--
<p><b>Enable Ticket Loss Rate Logging</b>: True or False, default False. If set to True and the current game mode is one of the Conquest types, including Scavenger and Domination, a comma separated value (CSV) log file will be created for each map/mode/round. Look for files that end with <b>tlr.csv</b> in your procon/Logs/<i>ip_port</i> folder. The log will be updated approximately every 5 seconds with ticket loss information and unstacking moves. You must disable the security sandbox for Plugins if you set the <b>Enable Ticket Loss Rate Logging</b> feature to True.</p>
-->

<p><b>Enable Whitelisting Of Reserved Slots List</b>: True or False, default True. Treats the reserved slots list as if it were added to the specified <b>Whitelist</b>.</p>

<p><b>Whitelist</b>: List of player names (without clan tags), clan tags (by themselves), or EA GUIDs, one per line, in any combination. The first item may also specify a file to merge into the list, e.g., <i>&lt;whitelist.txt</i>. See <b>Merge Files</b> above. If <b>On&nbsp;Whitelist</b> is enabled or the balance speed is <i>Slow</i>, any players on the whitelist are completely excluded from being moved by the plugin (except for between-round scrambling).</p>

<p>Each line of the Whitelist may include one more more option codes that control which exclusions are applied. The codes <b>MUST</b> come after the name/tag/guid and must be separated by spaces. No codes means all options are applied. Codes can only be specified directly in the plugin setting or in a merge file, they cannot be specified in the reserved slots list if <b>Enable Whitelisting Of Reserved Slots List</b> is True. The codes are described in the following table:
<table border='0'>
<tr><td>B</td><td>Exclude from balancing moves</td></tr>
<tr><td>U</td><td>Exclude from unstacking moves</td></tr>
<tr><td>S</td><td>Exclude from unswitching (allow to switch teams freely)</td></tr>
<tr><td>D</td><td>Exclude from <b>Disperse Evenly List</b> or <b>Disperse Evenly By Clan Players</b> moves</td></tr>
<tr><td>R</td><td>Exclude from <b>Disperse Evenly By Rank &gt;=</b> moves</td></tr>
</table></p>

<p>Example list with the name of one player, tag of a clan, and GUID of another player. The player name has the code for exclusion from unstacking and the code for exclusion from rank dispersal, and the clan tag has the code for exclusion from unswitching. The GUID has no codes, which means all exclusions apply:
<pre>
  PapaCharlie9 U R
  LGN S
  EA_20D5B089E734F589B1517C8069A37E28
</pre></p>

<p><b>Friends List</b>: List of player names (without clan tags), clan tags (by themselves), or EA GUIDs, <b>two or more per line</b> separated by spaces, in any combination. The first item may also specify a file to merge into the list, e.g., <i>&lt;friends.txt</i>. See <b>Merge Files</b> above. Players that are friends with each other are specified by a friends sub-list. A sub-list is a single line of the Friends List with two or more names, tags or guids. No literal item may be duplicated anywhere in the list, but a player's clan tag may be on one sub-list and his name on another and his guid on a third. See <b>On&nbsp;Friends&nbsp;List</b> and <b>Keep&nbsp;Friends&nbsp;In&nbsp;Same&nbsp;Team</b>. Example of two separate friends sub-lists:
<pre>
  PapaCharlie9 FTB C2C
  Tom Dick Harry EA_20D5B089E734F589B1517C8069A37E28 
</pre></p>

<p><b>Disperse Evenly List</b>: List of player names (without clan tags), clan tags (by themselves), or EA GUIDs, one per line (except for groups, see below) separated by spaces, in any combination. Players found on this list will be split up and moved so that they are evenly dispersed across teams. The first item may also specify a file to merge into the list, e.g., <i>&lt;disperse.txt</i>. See <b>Merge Files</b> above. Groups of players, tags and guids may be specified to insure that they are always balanced to the opposite team from other specified groups. For example, if clan tag ABC is in group 1 and clan tag XYZ in is group 2, all players with clan tag ABC will eventually be balanced to one team and all players with clan tag XYZ will eventually be balanced to the other team. Groups 3 and 4 are used only for SQDM mode. A group is specified by starting an item in the list with a single digit, from 1 to 4, followed by a space, followed by a space separated list of names, tags or guids. Individual items and groups may be specified in any combination and any order in the list, though duplicating any item is an error. Here is an example list with individual players 'Joe' and 'Mary' and groups 1 and 2:
<pre>
  1 ABC LGN PapaCharlie9
  Joe
  2 XYZ EA_20D5B089E734F589B1517C8069A37E28
  Mary
</pre></p>

<h3>2 - Exclusions</h3>
<p>These settings define which players should be excluded from being moved for balance or unstacking. Changing a preset may overwrite the value of one or more of these settings. Changing one of these settings may change the value of the Preset, usually to None, to indicate a custom setting.</p>

<p><b>On Whitelist</b>: True or False, default True. If True, the <b>Whitelist</b> is used to exclude players. If False, the Whitelist is ignored.</p>

<p><b>On Friends List</b>: True or False, default False. If True, the Friends List is used to exclude players. If False, the <b>Friends&nbsp;List</b> is ignored.</p>

<p><b>Apply Friends List To Team</b>: True or False, default False. Only visible if <b>On Friends List</b> is True. If True, if 5 or more friends are on the same team, they will not be moved, regardless of which squads they are in. If False, if 2 or more friends are in the same squad, they will not be moved.</p>

<p><b>Top Scorers</b>: True or False, default True. If True, the top 1, 2, or 3 players (depending on server population and mode) on each team are excluded from moves for balancing or unstacking. This is to reduce the whining and QQing when a team loses their top players to autobalancing.</p>

<p><b>Same Clan Tags In Squad</b>: True or False, default True. If True, a player will be excluded from being moved if they are a member of a squad (or team, in the case of SQDM) that has at least one other player in it with the same clan tag.</p>

<p><b>Same Clan Tags In Team</b>: True or False, default False. If True, a player will be excluded from being moved for balancing or unstacking if they are a member of a team that has 5 or more players with the same clan tag, regardless of which squad they are in. If False, no special treatment for teams is applied, but <b>Same Clan Tags In Squad</b> may apply.</p>

<p><b>Same Clan Tags For Rank Dispersal</b>: True or False, default False. If True, dispersal by per-mode <b>Disperse Evenly By Rank &gt;=</b> will not be applied if the player has a clan tag that at least one other player on the same team has. This option is a special case of <b>Lenient Rank Dispersal</b>, enabling just one specific exclusion to be applied leniently.</p>

<p><b>Lenient Rank Dispersal</b>: True or False, default False. If False, dispersal by per-mode <b>Disperse Evenly By Rank &gt;=</b> only will by applied strictly, ignoring all exclusions except whitelisting. Teams may get unbalanced, but ranked players will be evenly dispersed. If True, dispersal by per-mode setting of ranked players will respect most exclusions, including <b>Minutes After Being Moved</b> and <b>Rout Percentage</b>. Teams will be kept in balance, but ranked players may not be dispersed evenly.</p>

<p><b>Minutes After Joining</b>: Number greater than or equal to 0, default 5. After joining the server, a player is excluded from being moved for balance or unstacking for this number of minutes. The player is also allowed to switch teams freely during this time. Set to 0 to disable. Keep in mind that most joining players were already assigned to the team with the least players. They have already 'paid their dues'.</p>

<p><b>Minutes After Being Moved</b>: Number greater than or equal to 0, default 90. After being moved for balance or unstacking, a player is excluded from being moved again for the specified number of minutes. Set to 0 to disable.</p>

<h3>3 - Round Phase and Population Settings</h3>
<p>These settings control balancing and unstacking, depending on the round phase and server population.
For each phase, there are three unstacking settings for server population: Low, Medium and High, by number of players. Each number is the ticket percentage ratio that triggers unstacking for each combination of phase and population. Setting the value to 0 disables team unstacking for that combination. If the number is not 0, if the ratio of the winning team's tickets to the losing teams tickets is equal to or greater than the ticket percentage ratio specified, unstacking will be activated.</p>

<p><i>Example</i>: for the <b>Ticket Percentage To Unstack</b> setting, there are three phases, Early, Mid and Late. For each phase, the value is a list of 3 number, either 0 or greater than 100 and less than 5000, one for each of the population levels of Low, Medium, and High, respectively:
<pre>
    Early Phase: Ticket Percentage To Unstack        0, 120, 120
    Mid Phase: Ticket Percentage To Unstack          0, 120, 120
    Late Phase: Ticket Percentage To Unstack         0, 0, 0
</pre></p>

<p>This means that in the Early or Mid phases when the population is Low, there will be no unstacking (0 always means disable). Also, in the Late Phase for any population level, there will be no unstacking. For any other combination, such as Mid Phase with High population, teams will be unstacked when the ratio of winning tickets to losing tickets is 120% or more.</p>

<p>For each phase, there are also three balance speed names for server population: Low, Medium and High, by number of players. These speeds control how aggressively players are selected for moving by the autobalancer. Enter them as speed names separated by commas.</p>

<p><i>Example</i>: for the <b>Balance Speed</b> setting, there are three phases, Early, Mid and Late. For each phase, the value is a list of 3 speed names, one for each of the population levels of Low, Medium, and High, respectively: 
<pre>
    Early Phase: Balance Speed        Slow, Adaptive, Adaptive
    Mid Phase: Balance Speed          Slow, Adaptive, Adaptive
    Late Phase: Balance speed         Stop, Stop, Stop
</pre></p>

<p>This means that in the Early or Mid phases when the population is Low, the balance speed will be Slow. In the Late Phase for any population level, balancing will be disabled. For any other combination, such as Mid Phase with High population, balancing will use an Adaptive speed.</p>

<p>If you forget the names of the balance speeds, click on the <b>Spelling Of Speed Names Reminder</b> setting. This will display all of the balance speed names for you.</p>

<h3>4 - Scrambler</h3>
<p>These settings define options for between-round scrambling of teams. The setting <b>Enable Scrambler</b> is a per-mode setting, which allows you to decide on a mode-by-mode basis whether to use scrambling between rounds or not. See the per-mode settings in Section 8 below for more details. Note that whitelisted players are <b>not</b> excluded from scrambling and that scrambling is not possible with SQDM.</p>

<p><b>Only By Command</b>: True or False, default False. If True, <b>Only On New Maps</b> and <b>Only On Final Ticket Percentage &gt;=</b> settings are ignored/hidden and scrambles will happen only after an admin types the <b>mb scramble on</b> command into chat.</p>

<p><b>Only On New Maps</b>: True or False, default True. If True, scrambles will happen only after the last round of a map. For example, if a map has 2 rounds, there will be no scramble after round 1, only after round 2. If False, scrambling will be attempted at the end of every round.</p>

<p><b>Only On Final Ticket Percentage &gt;=</b>: Number greater than 100 or equal to 0, default 120. This is the ratio between the winning and losing teams final ticket counts at the end of the round. In count-down modes like Conquest, this is the ratio of the difference between the maximum starting tickets and the final ticket count. For example, on a 1000 ticket server, if the final ticket counts are 0/250, the ratio is (1000-0)/(1000-250)=1000/750=133%. Since that is greater than 120, scrambling would be done. For count-up modes like TDM, the ratio is final ticket values. If this value is set to 0, scrambling will occur regardless of final ticket counts.</p>

<p><b>Scramble By</b>: One of the values defined in <b>Definition Of Strong</b> above. Determines how strong vs. weak players are chosen for scrambling.</p>

<p><b>Keep Squads Together</b>: True or False, default True. If True, during scrambling, an attempt is made to keep players in a squad together so that they are moved as a squad. This is not always possible and sometimes squads may be split up even when this setting is True. The squad ID may change, e.g., if the players were originally in Alpha, they may end up in Echo on the other team.</p>

<p><b>Keep Clan Tags In Same Team</b>: True or False, default True. Only visible if <b>Keep Squads Together</b> is set to False. If True, players with the same clan tags will be scrambled to the same team. Players in a squad with other players with the same clan tag will be kept together, if possible. Players in the same squad that do not have the same tag may get moved to another squad. The squad ID may change, e.g., if the players were originally in Hotel, they may end up in Charlie on the other team.</p>

<p><b>Keep Friends In Same Team</b>: True or False, default True. Only visible if <b>Keep Squads Together</b> is set to False and if <b>Keep&nbsp;Clan&nbsp;Tags&nbsp;In&nbsp;Same&nbsp;Team</b> is set to True. If True, players in the same friends sub-list in the <b>Friends&nbsp;List</b> will be scrambled to the same team. Players in a squad with other friends will be kept together, if possible. Players in the same squad that are not friends may get moved to another squad. The squad ID may change, e.g., if the players were originally in Hotel, they may end up in Charlie on the other team.</p>

<p><b>Divide By</b>: None, ClanTag, or DispersalGroup. Specifies how players should be divided into teams during scrambling. ClanTag divides all players evenly between the two teams if they have the clan tag specified in <b>Clan Tag To Divide By</b>. Only one tag may be specified. DispersalGroup divides players to their assigned dispersal group, if they are in one of the two groups defined in the <b>Disperse Evenly List</b>, if any.

<p><b>Delay Seconds</b>: Number of seconds greater than or equal to 0 and less than or equal to 70, default 30. Number of seconds to wait after the round ends before doing the scramble. If done too soon, many players may leave after the scramble, resulting in wildly unequal teams. If done too late, the next level may load and the game server will swap players to opposite teams, interfering with the scramble in progress, which may result in wildly unequal teams.</p>

<h3>5 - Messages</h3>
<p>These settings define all of the chat and yell messages that are sent to players when various actions are taken by the plugin. All of the messages are in pairs, one for chat, one for yell. If both the chat and the yell messages are defined and <b>Quiet&nbsp;Mode</b> is not set to True, both will be sent at the same time. The message setting descriptions apply to both chat and yell. To disable a chat message for a specific actcion, delete the message and leave it empty. To disable theyell message for a specific action, delete the message and leave it empty.</p>

<p>Several substitution macros are defined. You may use them in either chat or yell messages:
<table border='0'>
<tr><td>%name%</td><td>player name</td></tr>
<tr><td>%tag%</td><td>player clan tag</td></tr>
<tr><td>%fromTeam%</td><td>team the player is currently on, as 'US' or 'RU', or 'Alpha', 'Bravo', 'Charlie', or 'Delta' for SQDM, or 'T1:US/RU' or 'T2:CN/RU' for BF4.</td></tr>
<tr><td>%toTeam%</td><td>team the plugin will move the player to, same team name substitutions as for %fromTeam%</td></tr>
<tr><td>%reason%</td><td>ONLY APPLIES TO BAD TEAM SWITCH: reason for switching the player back, may contain other replacements</td></tr>
<tr><td>%technicalDetails%</td><td>THIS IS PROVIDED BY THE PLUGIN: Details about how the autobalancer is preparing to balance or why it is taking so long</td></tr>
</table></p>

<p><b>Quiet Mode</b>: True or False, default False. If False, chat messages are sent to all players and yells are sent to the player being moved. If True, chat and yell messages are only sent to the player being moved.</p>

<p><b>Yell Duration Seconds</b>: A number greater than 0 and less than or equal to 20, or 0. If set to 0, all yells are disabled, even if they have non-empty messages. All yells have the same duration. This duration also controls the delay between when a player is warned and when the are unswitched (see Section 6).</p>

<p><b>Moved For Balance</b>: Message sent after a player is moved for balance.</p>

<p><b>Moved To Unstack</b>: Message sent after a player is moved to unstack teams.</p>

<p><b>Detected Bad Team Switch</b>: Message sent after a player tries to make a forbidden team switch if <b>Enable Immediate Unswitch</b> is set to False (see Section 6 below) or mode is Squad Deathmatch. The message is sent before the player is admin killed and sent back to his original team.</p>

<p><b>Bad Because: Moved By Balancer</b>: Replacement for %reason% if the player tried to move to a different team from the one the plugin have moved them to for balance or unstacking.</p>

<p><b>Bad Because: Winning Team</b>: Replacement for %reason% if the player tried to move to the winning team.</p>

<p><b>Bad Because: Biggest Team</b>: Replacement for %reason% if the player tried to move to the biggest team.</p>

<p><b>Bad Because: Rank</b>: Replacement for %reason% if the player has Rank greater than or equal to the per-mode <b>Disperse Evenly By Rank</b> setting.</p>

<p><b>Bad Because: Clan</b>: Replacement for %reason% if the player has the same clan tag as other players for <b>Disperse Evenly By Clan Players</b> setting.</p>

<p><b>Bad Because: Dispersal List</b>: Replacement for %reason% if the player is a member of the <b>Disperse Evenly List</b>.</p>

<p><b>Detected Good Team Switch</b>: Message sent after a player switches from the winning team to the losing team, or from the biggest team to the smallest team. There is no follow-up message, this is the only one sent.</p>

<p><b>After Unswitching</b>: Message sent after a player is killed by admin and moved back to the team he was assigned to. This message is sent after the <b>Detected Bad Team Switch</b> message.</p>

<p><b>Teams Will Be Scrambled</b>: <font color=#FF0000>BF4 only, chat only.</font> Message sent after the round ends if scrambling is enabled and teams require scrambling for the next round.</p>

<p><b>Autobalancing</b>: Message sent when teams are out of balance and the balancer is waiting for the right conditions to move a player. The %technicalDetails% give further details about what the balancer is doing, like waiting for a new player to join or waiting for a player to die.</p>

<h3>6 - Unswitcher</h3>
<p>This section controls the unswitcher. Every time a player tries to switch to a different team, the unswitcher checks if the switch is allowed or forbidden. If forbidden, he will be moved back by the plugin (see <b>Enable Immediate Unswitch</b> for details about how). The possible values are <i>Always</i>, which means do not allow (always forbid) this type of team switching, <i>Never</i>, which means allow team switching of this type, and <i>LatePhaseOnly</i>, which means allow team switching of this type until Late Phase, then no longer allow it (forbid it). Note that setting any of the <b>Forbid ...</b> settings to <i>Never</i> will reduce the effectiveness of the balancer and unstacker.</p>

<p><b>Forbid Switching After Autobalance</b>: Always, Never, or LatePhaseOnly, default Always. Controls team switching after being moved to a different team for balance or unstacking. This setting forbids moved players from moving back to their original team.</p>

<p><b>Forbid Switching To Winning Team</b>: Always, Never, or LatePhaseOnly, default Always. Controls switching to the winning team.</p>

<p><b>Forbid Switch To Biggest Team</b>: Always, Never, or LatePhaseOnly, default Always. Contorls switching to the biggest team.</p>

<p><b>Forbid Switch After Dispersal</b>: Always, Never, or LatePhaseOnly, default Always. Controls team switching after being moved to a different team due to <b>Disperse Evenly By Rank</b>, <b>Disperse Evenly By Clan Players</b> or the <b>Disperse Evenly List</b>. This setting forbids them from moving back to their original team.</p>

<p><b>Enable Immediate Unswitch</b>: True or False, default True. If True, if a player tries to make a forbidden team switch, the plugin will immediately move them back without any warning. They will only see the <b>After Unswitching</b> message(s). If False, the plugin will wait until the player spawns, it will then post the <b>Detected Bad Team Switch</b> message(s), it will wait <b>Yell Duration Seconds</b> seconds, then it will admin kill the player and move him back. <b>NOTE: Does not apply to SQDM. SQDM is always treated as this were set to False.</b></p>

<h3>7 - TBD</h3>
<p>There is no section 7. This section is reserved for future use.</p>

<h3>8 - Settings for ... (each game mode)</h3>
<p>These are the per-mode settings, used to define population and phase levels for a round and other settings specific to a game mode. Some modes have settings that no other modes have, other modes have fewer settings than most other modes. Each section is structured similarly. One common section is described in detail below and applies to several modes. Modes that have unique settings are then listed separately. The game modes are grouped as follows:
<table border='0'>
<tr><td>Conq Small, Dom, Scav</td><td>BF3: Conquest Small, Conquest Assault Small #1 and #2, Conquest Domination, and Scavenger</td></tr>
<tr><td>Conquest Large</td><td>Conquest Large and BF3:Conquest Assault Large</td></tr>
<tr><td>Conquest Small</td><td>BF4: same as BF3 Conq Small, Dom, Scav</td></tr>
<tr><td>CTF</td><td>Capture The Flag, uses minutes to define phase instead of tickets</td></tr>
<tr><td>Defuse</td><td>BF4: standard settings</td></tr>
<tr><td>Domination</td><td>BF4: same as BF3 Conq Small, Dom, Scav</td></tr>
<tr><td>DT Chain Link</td><td>BF4: Similar to Domination settings</td></tr>
<tr><td>Gun Master</td><td>BF3: Only has a few settings</td></tr>
<tr><td>NS Carrier Large</td><td>Carrier Assault Large, uses minutes to define phase and score to define ratio difference instead of tickets</td></tr>
<tr><td>NS Carrier Small</td><td>Carrier Assault Small, uses minutes to define phase and score to define ratio difference instead of tickets</td></tr>
<tr><td>Obliteration</td><td>BF4: TBD</td></tr>
<tr><td>Rush</td><td>Has unique settings shared with Squad Rush and no other modes</td></tr>
<tr><td>Squad Deathmatch</td><td>Standard settings, similar to Conquest, except that unstacking is disabled (default 0)</td></tr>
<tr><td>Squad Obliteration</td><td>BF4: TBD</td></tr>
<tr><td>Squad Rush</td><td>BF3: Has unique settings shared with Rush and no other modes</td></tr>
<tr><td>Superiority</td><td>Air and Tank Superiority</td></tr>
<tr><td>Team Deathmatch</td><td>TDM and TDM Close Quarters, standard settings, similar to Conquest</td></tr>
<tr><td>Unknown or New Mode</td><td>Generic settings for any new mode that gets introduced before this plugin gets updated</td></tr>
</table></p>

<p>These are the settings that are common to most modes:</p>

<p><b>Max Players</b>: Number greater than or equal to 8 and less than or equal to <b>Maximum Server Size</b>. Some modes might be set up in UMM or Adaptive Server Size or other plugins with a lower maximum than the server maximum. If you set a lower value in your server settings or in a plugin, set the same setting here. This is important for calculating population size correctly.</p>

<p><b>Rout Percentage</b>: Number greater than or equal to 101 and less than or equal to 100000, or 0, default is 0. When one team is so far behind another team (called a 'rout'), it is unfair to move strong or dispersal players in either direction. Use this setting to define when to stop moving strong or dispersal players. For example, if set to 200 for Conquest, the losing team is routed when the winner has at least twice as many tickets as the loser, e.g., 301 vs 150. Movement of strong players for balance or unstacking will be suspended. In the case of dispersal, the suspension applies to both strong and weak players and <b>Enable Strict Dispersal</b> must be False, or if generally strict except for rank dispersal, <b>Lenient Rank Dispersal</b> must be True.</p>

<p><b>Check Team Stacking After First Minutes</b>: Number greater than or equal to 0. From the start of the round, this setting is the number of minutes to wait before activating unstacking. If set to 0, no unstacking will occur for this mode.</p>

<p><b>Max Unstacking Swaps Per Round</b>: Number greater than or equal to 0. To prevent the plugin from swapping every player on every team for unstacking, a maximum per round is set here. If set to 0, no unstacking will occur for this mode.</p>

<p><b>Number Of Swaps Per Group</b>: Number greater than or equal to 0 and less than or equal to <b>Max Unstacking Swaps Per Round</b>, ideally an integral factor, e.g., if <b>Max Unstacking Swaps Per Round</b> is 12, <b>Number Of Swaps Per Group</b> may be 1, 2, 3, 4, 6 or 12. During unstacking, swaps are done as quickly as possible, up to this number. Once this number of swaps is reached, the <b>Delay Seconds Between Swap Groups</b> delay is applied before further swaps are attempted.</p>

<p><b>Delay Seconds Between Swap Groups</b>: Number greater than or equal to 60. After a group of unstacking swaps, wait this number of seconds before doing another group of unstacking swaps.</p>

<p><b>Max Unstacking Ticket Difference</b>: Number greater than or equal to 0. If the difference in tickets is greater than the number specified, unstacking will be disabled. Set to 0 to allow any difference for unstacking.</p>

<p><b>Enable Unstacking By Player Stats</b>: True or False, default False. If set to True, the ratio of average player stats across each team is used instead of the ticket ratio for determining if unstacking is needed. You choose the stat to use with <b>Determine Strong Players By</b>. For example, if <b>Determine Strong Players By</b> is <i>RoundKills</i> and the average of team 1 kills per player is 13 and the average for team 2 is 10, the ratio of 13/10 is 130%. If the unstacking ratio is 120%, teams will be unstacked.</p>

<p><b>Determine Strong Players By</b>: Choice based on method. The setting defines how strong players are determined. Any player that is not a strong player is a weak player. See the <b>Definition of Strong</b> section above for the list of settings. All players in a single team are sorted by the specified definition. Any player above the median position after sorting is considered strong. For example, suppose there are 31 players on a team and this setting is set to <i>RoundScore</i> and after sorting, the median is position #16. If this player is position #7, he is considered strong. If his position is #16 or #17, he is considered weak.</p>

<p><b>Percent Of Top Of Team Is Strong</b>: Number greater than or equal to 5 and less than or equal to 50, or 0. After sorting a team with the <b>Determine Strong Players By</b> choice, this percentage determines the portion of the top players to define as strong. Default is 50 so that any player above the median counts as strong. CAUTION: This setting is changed when the <b>Preset</b> is changed, previous values are overwritten for all modes.</p>

<p><b>Only Move Weak Players</b>: True or False, default True. If set to True, only weak players will be moved for balancing.</p>

<p><b>Disperse Evenly By Rank &gt;=</b>: Number greater than or equal to 0 and less than or equal to 145, default 0. Any players with this absolute rank (Colonel 100 is 145) or higher will be dispersed evenly across teams. This is useful to insure that Colonel 100 ranked players don't all stack on one team. Set to 0 to disable.</p>

<p><b>Disperse Evenly By Clan Players &gt;=</b>: Number greater than or equal to 4 and less than or equal to 40, default 0. If the number of players with the same clan tag is greater than or equal to this number, the players with this same clan tag will be dispersed evenly across teams. This setting overrides <b>Same Clan Tag ...</b> exclusions. Set to 0 to disable.</p>

<p><b>Enable Disperse Evenly List</b>: True or False, default False. If set to true, the players are matched against the <b>Disperse Evenly List</b> and any that match will be dispersed evenly across teams. This is useful to insure that certain clans or groups of players don't always dominate whatever team they are not on.</p>

<p><b>Enable Strict Dispersal</b>: True or False, default True. Only visible if <b>Disperse Evenly By Clan Players</b> or <b>Enable Disperse Evenly List</b> is set to True. If set to True, players will be moved for dispersal, ignoring all exclusions except whitelisting. This may result in wildly unbalanced teams, but absolutely guarantees that players are dispersed. If set to False, players will be moved for dispersal, but many exclusions will apply, such as <b>Same Clan Tags In Squad</b>,  <b>Minutes After Being Moved</b> and <b>Rout Percentage</b>. The teams will be kept in balance, but players may not be dispersed evenly.</p>

<p><b>Enable Low Population Adjustments</b>: True or False, default False. If set to True, when the population of a server is low, all <b>Forbid ...</b> settings in the Unswitcher section are treated as <i>Never</i> (meaning, team switching is allowed in all circumstances), all disperse evenly settings, such as <b>Disperse Evenly By Rank &gt;=</b> are ignored, the maximum difference between team counts that is considered balanced is reduced to 1, and the minimum number of players required for balancing is reduced to 4, until the population rises above your <b>Definition Of Low Population For Players &lt;=</b> setting.</p>

<p><b>Definition Of High Population For Players &gt;=</b>: Number greater than or equal to 0 and less than or equal to <b>Max&nbsp;Players</b>. This is where you define the High population level. If the total number of players in the server is greater than or equal to this number, population is High.</p>

<p><b>Definition Of Low Population For Players &lt;=</b>: Number greater than or equal to 0 and less than or equal to <b>Max&nbsp;Players</b>. This is where you define the Low population level. If the total number of players in the server is less than or equal to this number, population is Low. If the total number is between the definition of High and Low, it is Medium.</p>

<p><b>Definition Of Early Phase As Tickets From Start</b>: Number greater than or equal to 0. This is where you define the Early phase, as tickets from the start of the round. For example, if your round starts with 1500 tickets and you set this to 300, as long as the ticket level for all teams is greater than or equal to 1500-300=1200, the phase is Early. Set to 0 to disable Early phase.</p>

<p><b>Definition Of Late Phase As Tickets From End</b>: Number greater than or equal to 0. This is where you define the Late phase, as tickets from the end of the round. For example, if you set this to 300 and at least one team in Conquest has less than 300 tickets less, the phase is Late. If the ticket level of both teams is between the Early and Late settings, the phase is Mid. Set to 0 to disable Late phase.</p>

<p><b>Enable Scrambler</b>: True or False, default False, not visible for SQDM. If set to True, between-round scrambling of teams will be attempted for rounds played in this mode, depending on the settings in Section 5.</p>

<!--
<p><b>Enable Ticket Loss Ratio</b>: True or False, default False, only visible for Conquest-type modes. If set to True, unstacking will be based on ticket loss ratio percentage instead of ticket ratio percentage in Section 3. <font color=#FF0000><b>IMPORTANT</b>: you <b>must</b> adjust your Section 3 <b>Ticket Percentage To Unstack</b> settings if you <b>Enable Ticket Loss Ratio</b>.</font> The percentages for ticket loss ratios are much larger than for tickets ratios. If you don't adjust your values upwards, you will be constantly unstacking teams. See discussion in the forums for details.</p>

<p><b>Ticket Loss Sample Count</b>: Number greater than or equal to 15 and less than or equal to 1200, default 180. This setting determines how many ticket loss samples are included in the average. Each sample is the average ticket loss per second. The higher this number is, the longer it will take to detect a significant change in loss rate; however, the lower the number is, the more susceptible unstacking will be to false detections (temporary spikes). The average is a moving average, so as new samples are added, old samples are dropped.</p>
-->

<p>These settings are unique to Conquest.</p>

<p><b>Enable Metro Adjustments</b>: True or False, default False. This setting should be set to True when Metro is one of several maps in a Conquest Large or Conquest Small rotation. This setting insures that no players are moved to the losing team, which is usually futile. The actual effect is that when the map is Metro, during Early and Late phase, the Balance Speed is forced to be Stop and the Unstack Percentage Ratio is forced to be 0%. During Mid Phase, the Balance Speed is forced to be Slow. The Unstack Percentage Ratio is left unchanged for Mid Phase. If Metro is the only Conquest map in the rotation or if Metro is not in the rotation at all, set this setting to False. See also <b>Metro Adjusted Definition Of Late Phase</b>.</p>

<p><b>Metro Adjusted Definition Of Late Phase</b>: Number greater than or equal to 0. This setting is visible only when <b>Enable Metro Adjustments</b> is set to True. When the map is Metro, the value specified here is used instead of <b>Definition Of Late Phase As Tickets From End</b>. This allows you to specify a much longer Late phase than for the other Conquest maps in your rotation. You generally want Metro Late phase to be the second half of your tickets, for example, if you have 1000 tickets, set this setting to 500.</p>

<p>These settings are unique to CTF and Carrier Assault.</p>

<p><b>Definition Of Early Phase As Minutes From Start</b>: Number greater than or equal to 0. This is where you define the Early phase, as minutes from the start of the round. For example, if your round starts with 20 minutes on the clock and you set this to 5, the phase is Early until 20-5=15 minutes are left on the clock.</p>

<p><b>Definition Of Late Phase As Minutes From End</b>: Number greater than or equal to 0. This is where you define the Late phase, as minutes from the end of the round. For example, if your round starts with 20 minutes on the clock and you set this to 8, the phase is Late for when there 8 minutes or less left on the clock.</p>

<p>These settings are unique to Rush and Squad Rush.</p>

<p>Rush and Squad Rush require adjustments to the ticket percentage to unstack values specified in section 3 above. For example, if you have a mixed mode server with TDM and Rush, you may set ticket percentage to unstack to 120 for certain combinations of phase and population. This works great for TDM with 200 tickets. It does not work well for Rush with 150 tickets. The ticket ratio may easily exceed 120% without the teams being stacked. It's just the nature of the stages. Rather than have completely different settings for Rush and Squad Rush for section 3, instead, the per-mode settings define adjustments to the section 3 settings. For example, if you specify 30 for <b>Stage 1 Ticket Percentage To Unstack Adjustment</b>, 30 is added to 120 to yield 150% as the ratio for stage 1. You may also use negative numbers to reduce the value, for example, if the normal setting is 120 and you want stage 4 to have no unstacking, you may set the adjustment to -120. If the adjustment results in a value less than or equal to 100, it is set to 0. If you use 0 for the adjustment value, no change is made. <b>If the normal value is 0, no adjustment is applied.</b> Otherwise, the adjustment is applied to all phase and population combinations for that stage. Rush maps range from 3 to 5 stages. Most are 4. To account for maps with up to 5 stages, there is one setting for stage 4 and stage 5. Treat this setting as the 'last' stage.</p>

<p><b>Stage 1 Ticket Percentage To Unstack Adjustment</b>: Any positive or negative number whose absolute value is 0 or less than or equal to the corresponding <b>Ticket Percentage To Unstack</b> value. If the defending team is stacked, the game will be unlikely to get past stage 1, so ratios in the range 125 to 150 after adjustment are good for stage 1. For example, if your normal ratio is 120, set the adjustment to 5 to get 125 for Rush.</p>

<p><b>Stage 2 Ticket Percentage To Unstack Adjustment</b>: Any positive or negative number whose absolute value is 0 or less than or equal to the corresponding <b>Ticket Percentage To Unstack</b> value. If the attacking team is stacked, the game will get to stage 2 quickly, so ratios in the range 125 to 150 are good for stage 2. For example, if  your normal ratio is 120, set the adjustment to 30 to get 150 for Rush</p>

<p><b>Stage 3 Ticket Percentage To Unstack Adjustment</b>: Any positive or negative number whose absolute value is 0 or less than or equal to the corresponding <b>Ticket Percentage To Unstack</b> value. Evenly matched teams will often get to stage 3, so set the ratio high to catch unsual situations only, ratios in the range 200 or more are good for stage 3. For example, if your normal ratio is 120, set the adjustment to 80 to get 200 for Rush.</p>

<p><b>Stage 4 And 5 Ticket Percentage To Unstack Adjustment</b>: Any positive or negative number whose absolute value is 0 or less than or equal to the corresponding <b>Ticket Percentage To Unstack</b> value. This is tricky, since a team that is stacked for attackers or evenly matched teams will both get to the last stage. To give the benefit of the doubt, aim for a ratio of 0. For example, if your normal ratio is 120, set the adjustment to -120 to get 0 for Rush.</p>

<p><b>Seconds To Check For New Stage</b>: Number greater than or equal to 5 and less than or equal to 30, default is 10. Number of seconds between each check to see if a new stage has started. The check is a guess since BF3 does not report stage changes, so it is possible for the plugin to guess incorrectly.</p>

<p><b>Enable Advanced Rush Unstacking</b>: True or False, default False. If set to True, an advanced method of determining unstacking is used for Rush. Do not use this unless you know what you are doing. See forum post for details.</p>

<h3>9 - Debugging</h3>
<p>These settings are used for debugging problems with the plugin.</p>

<p><b>Show Command In Log</b>: Special commands may be typed in this text area to display information in plugin.log. Type <i>help</i> into the text field and press Enter (type a return). A list of commands will be written to plugin.log.</p>

<p><b>Log Chat</b>: True or False, default True. If set to True, all chat messages sent by the plugin will be logged in chat.log.</p>

<p><b>Enable Logging Only Mode</b>: True or False, default False. If set to True, the plugin will only log messages. No move, chat or yell commands will be sent to the game server. If set to False, the plugin will operate normally.</p>

<p><b>Enable External Logging</b>: True or False, default False. If set to True, plugin.log messages will also be sent to an external log file in Procon's Log folder, by game server connection. See <b>External Log Suffix</b>. </p>

<p><b>External Log Suffix</b>: Suffix for file name used for the external log file, default is <i>_mb.log</i>. The path to procon/Logs/<i>ip_port</i> is used to write a log file with the current date in YYYYMMDD format prepended to the suffix you supply, for example, 20130515_mb.log.</p>

<h2>Development</h2>
<p>This plugin is an open source project hosted on GitHub.com. The repo is located at
<a href='https://github.com/PapaCharlie9/multi-balancer'>https://github.com/PapaCharlie9/multi-balancer</a> and the master branch is used for public distributions. See the <a href='https://github.com/PapaCharlie9/multi-balancer/tags'>Tags</a> tab for the latest ZIP distribution. If you would like to offer bug fixes or new features, feel free to fork the repo and submit pull requests. Post questions and problem reports in the forum Plugin thread.</p>
";

        /*
        Deleted:
        <h3>Details</h3>
        <p>This plugin provides a rich set of features for a wide variety of team management styles. Some (but not all) of the styles this plugin is designed for are listed below, and you can mix and max these styles depending on the game mode, number of players on the server and whether it is early or late in the round:</p>

        <h4>Fair play</h4>
        <p>This style aims for each round to be as evenly balanced in skills as possible. Every round should end as a &quot;nail-biter&quot;. If you want to see Conquest rounds end with ticket differences less than 20 or Team Deathmatch or Squad Deathmatch rounds end with kill differences less than 5 or Rush matches that get down to 1 ticket before the last MCOM is blown, the settings provided by this plugin give you the best chance to have that experience on your server.</p>

        <h4>Cutthroat</h4>
        <p>This is pretty much the exact opposite of Fair Play. Every player for himself and damn the consequences. If one team gets stacked with good players, that's just too bad for the other team. The newest players to join are the ones moved to keep teams balanced. This plugin supports cutthroat style by turning most of the features off, except new player reassignment and new player autobalancing.</p>

        <h4>Retain players</h4>
        <p>This style aims to retain players on your server. Players are left alone to do what they want, but aspects of team balance and team switching that cause players to leave, like too much autobalancing, team stacking, too many Colonel 100's on one team, too many players from one clan on one team, etc., are dealt with. Only things that are related to team balance are managed, however. This plugin doesn't do anything about, for example, base raping.</p>

        <h4>Keep friends together</h4>
        <p>This style recognizes that friends like to play together. To the extent that friends wear the same clan tag or are specified in a friend's list, the balancer and unstacker can be configured to keep friends together.</p>

        <h4>Split problem clans apart</h4>
        <p>This style recognizes that some &quot;pro&quot; clans can spoil everyone's fun if they play together, so the balancer and unstacker can be configured to split players with the same clan tag apart and spread them out evenly between teams.</p>
        */
        #endregion

    } // end MULTIbalancerUtils

} // end namespace PRoConEvents
