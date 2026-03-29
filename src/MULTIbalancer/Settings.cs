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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using System.Web;
using System.Windows.Forms;
using System.Xml;

using PRoCon.Core;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;

namespace PRoConEvents
{

    public partial class MULTIbalancer
    {
        /* ======================== SETTINGS ============================= */









        public List<CPluginVariable> GetDisplayPluginVariables()
        {


            List<CPluginVariable> lstReturn = new List<CPluginVariable>();

            try
            {
                List<String> simpleModes = GetSimplifiedModes();

                /* ===== SECTION 0 - Presets ===== */

                UpdatePresetValue();

                String var_name = "0 - Presets|Use Round Phase, Population and Exclusions preset ";
                String var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(PresetItems))) + ")";

                lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(PresetItems), Preset)));

                lstReturn.Add(new CPluginVariable("0 - Presets|Enable Unstacking", EnableUnstacking.GetType(), EnableUnstacking));

                lstReturn.Add(new CPluginVariable("0 - Presets|Enable Settings Wizard", EnableSettingsWizard.GetType(), EnableSettingsWizard));

                if (EnableSettingsWizard)
                {
                    List<String> enumModes = new List<String>();
                    enumModes.Add("Conq Small or Dom or Scav");
                    foreach (String sm in simpleModes)
                    {
                        if (!sm.Contains("Conq Small"))
                        {
                            enumModes.Add(sm);
                        }
                    }
                    var_name = "0 - Presets|Which Mode";
                    var_type = "enum." + var_name + "(" + String.Join("|", enumModes.ToArray()) + ")";

                    lstReturn.Add(new CPluginVariable(var_name, var_type, WhichMode));

                    lstReturn.Add(new CPluginVariable("0 - Presets|Metro Is In Map Rotation", MetroIsInMapRotation.GetType(), MetroIsInMapRotation));

                    lstReturn.Add(new CPluginVariable("0 - Presets|Maximum Players For Mode", MaximumPlayersForMode.GetType(), MaximumPlayersForMode));

                    lstReturn.Add(new CPluginVariable("0 - Presets|Lowest Maximum Tickets For Mode", LowestMaximumTicketsForMode.GetType(), LowestMaximumTicketsForMode));

                    lstReturn.Add(new CPluginVariable("0 - Presets|Highest Maximum Tickets For Mode", HighestMaximumTicketsForMode.GetType(), HighestMaximumTicketsForMode));

                    var_name = "0 - Presets|Preferred Style Of Balancing";
                    var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(PresetItems))) + ")";

                    lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(PresetItems), PreferredStyleOfBalancing)));

                    lstReturn.Add(new CPluginVariable("0 - Presets|Apply Settings Changes", ApplySettingsChanges.GetType(), ApplySettingsChanges));
                }

                /* ===== SECTION 1 - Settings ===== */

                lstReturn.Add(new CPluginVariable("1 - Settings|Debug Level", DebugLevel.GetType(), DebugLevel));

                lstReturn.Add(new CPluginVariable("1 - Settings|Maximum Server Size", MaximumServerSize.GetType(), MaximumServerSize));

                lstReturn.Add(new CPluginVariable("1 - Settings|Enable Battlelog Requests", EnableBattlelogRequests.GetType(), EnableBattlelogRequests));

                if (EnableBattlelogRequests || fRevealSettings)
                {
                    var_name = "1 - Settings|Which Battlelog Stats";
                    var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(BattlelogStats))) + ")";

                    lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(BattlelogStats), WhichBattlelogStats)));

                    lstReturn.Add(new CPluginVariable("1 - Settings|Maximum Request Rate", MaximumRequestRate.GetType(), MaximumRequestRate));

                    lstReturn.Add(new CPluginVariable("1 - Settings|Wait Timeout", WaitTimeout.GetType(), WaitTimeout));
                }

                /*
                        lstReturn.Add(new CPluginVariable("1 - Settings|Max Team Switches By Strong Players", MaxTeamSwitchesByStrongPlayers.GetType(), MaxTeamSwitchesByStrongPlayers));

                        lstReturn.Add(new CPluginVariable("1 - Settings|Max Team Switches By Weak Players", MaxTeamSwitchesByWeakPlayers.GetType(), MaxTeamSwitchesByWeakPlayers));
                */

                lstReturn.Add(new CPluginVariable("1 - Settings|Unlimited Team Switching During First Minutes Of Round", UnlimitedTeamSwitchingDuringFirstMinutesOfRound.GetType(), UnlimitedTeamSwitchingDuringFirstMinutesOfRound));

                /*
                        lstReturn.Add(new CPluginVariable("1 - Settings|Enable 2 Slot Reserve", Enable2SlotReserve.GetType(), Enable2SlotReserve));

                        lstReturn.Add(new CPluginVariable("1 - Settings|Enable @#!recruit Command", EnablerecruitCommand.GetType(), EnablerecruitCommand));
                */

                lstReturn.Add(new CPluginVariable("1 - Settings|Seconds Until Adaptive Speed Becomes Fast", SecondsUntilAdaptiveSpeedBecomesFast.GetType(), SecondsUntilAdaptiveSpeedBecomesFast));

                lstReturn.Add(new CPluginVariable("1 - Settings|Reassign New Players", ReassignNewPlayers.GetType(), ReassignNewPlayers));

                lstReturn.Add(new CPluginVariable("1 - Settings|Enable Admin Kill For Fast Balance", EnableAdminKillForFastBalance.GetType(), EnableAdminKillForFastBalance));

                if (EnableAdminKillForFastBalance || fRevealSettings)
                {
                    var_name = "1 - Settings|Select Fast Balance By";
                    var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(ForceMove))) + ")";

                    lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(ForceMove), SelectFastBalanceBy)));
                }

                lstReturn.Add(new CPluginVariable("1 - Settings|Enable In-Game Commands", EnableInGameCommands.GetType(), EnableInGameCommands));

                if (EnableRiskyFeatures || fRevealSettings)
                {
                    lstReturn.Add(new CPluginVariable("1 - Settings|Enable Ticket Loss Rate Logging", EnableTicketLossRateLogging.GetType(), EnableTicketLossRateLogging));
                }

                lstReturn.Add(new CPluginVariable("1 - Settings|Enable Whitelisting Of Reserved Slots List", EnableWhitelistingOfReservedSlotsList.GetType(), EnableWhitelistingOfReservedSlotsList));

                lstReturn.Add(new CPluginVariable("1 - Settings|Whitelist", Whitelist.GetType(), Whitelist));

                lstReturn.Add(new CPluginVariable("1 - Settings|Friends List", FriendsList.GetType(), FriendsList));

                lstReturn.Add(new CPluginVariable("1 - Settings|Disperse Evenly List", DisperseEvenlyList.GetType(), DisperseEvenlyList));

                /* ===== SECTION 2 - Exclusions ===== */

                lstReturn.Add(new CPluginVariable("2 - Exclusions|On Whitelist", OnWhitelist.GetType(), OnWhitelist));

                lstReturn.Add(new CPluginVariable("2 - Exclusions|On Friends List", OnFriendsList.GetType(), OnFriendsList));

                if (OnFriendsList || fRevealSettings)
                {
                    lstReturn.Add(new CPluginVariable("2 - Exclusions|Apply Friends List To Team", ApplyFriendsListToTeam.GetType(), ApplyFriendsListToTeam));
                }

                lstReturn.Add(new CPluginVariable("2 - Exclusions|Top Scorers", TopScorers.GetType(), TopScorers));

                lstReturn.Add(new CPluginVariable("2 - Exclusions|Same Clan Tags In Squad", SameClanTagsInSquad.GetType(), SameClanTagsInSquad));

                lstReturn.Add(new CPluginVariable("2 - Exclusions|Same Clan Tags In Team", SameClanTagsInTeam.GetType(), SameClanTagsInTeam));

                lstReturn.Add(new CPluginVariable("2 - Exclusions|Same Clan Tags For Rank Dispersal", SameClanTagsForRankDispersal.GetType(), SameClanTagsForRankDispersal));

                lstReturn.Add(new CPluginVariable("2 - Exclusions|Lenient Rank Dispersal", LenientRankDispersal.GetType(), LenientRankDispersal));

                lstReturn.Add(new CPluginVariable("2 - Exclusions|Minutes After Joining", MinutesAfterJoining.GetType(), MinutesAfterJoining));

                lstReturn.Add(new CPluginVariable("2 - Exclusions|Minutes After Being Moved", MinutesAfterBeingMoved.GetType(), MinutesAfterBeingMoved));

                /*
                lstReturn.Add(new CPluginVariable("2 - Exclusions|Joined Early Phase", JoinedEarlyPhase.GetType(), JoinedEarlyPhase));

                lstReturn.Add(new CPluginVariable("2 - Exclusions|Joined Mid Phase", JoinedMidPhase.GetType(), JoinedMidPhase));

                lstReturn.Add(new CPluginVariable("2 - Exclusions|Joined Late Phase", JoinedLatePhase.GetType(), JoinedLatePhase));
                */

                /* ===== SECTION 3 - Round Phase & Population Setttings ===== */

                if (EnableUnstacking)
                {
                    lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Early Phase: Ticket Percentage To Unstack (Low, Med, High population)", typeof(String), MULTIbalancerUtils.ArrayToString(EarlyPhaseTicketPercentageToUnstack)));

                    lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Mid Phase: Ticket Percentage To Unstack (Low, Med, High population)", typeof(String), MULTIbalancerUtils.ArrayToString(MidPhaseTicketPercentageToUnstack)));

                    lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Late Phase: Ticket Percentage To Unstack (Low, Med, High population)", typeof(String), MULTIbalancerUtils.ArrayToString(LatePhaseTicketPercentageToUnstack)));
                }

                var_name = "3 - Round Phase and Population Settings|Spelling Of Speed Names Reminder";
                var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(Speed))) + ")";

                lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(Speed), SpellingOfSpeedNamesReminder)));

                lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Early Phase: Balance Speed (Low, Med, High population)", typeof(String), MULTIbalancerUtils.ArrayToString(EarlyPhaseBalanceSpeed)));

                lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Mid Phase: Balance Speed (Low, Med, High population)", typeof(String), MULTIbalancerUtils.ArrayToString(MidPhaseBalanceSpeed)));

                lstReturn.Add(new CPluginVariable("3 - Round Phase and Population Settings|Late Phase: Balance Speed (Low, Med, High population)", typeof(String), MULTIbalancerUtils.ArrayToString(LatePhaseBalanceSpeed)));

                /* ===== SECTION 4 - Scrambler ===== */

                lstReturn.Add(new CPluginVariable("4 - Scrambler|Only By Command", OnlyByCommand.GetType(), OnlyByCommand));

                if (!OnlyByCommand)
                {
                    lstReturn.Add(new CPluginVariable("4 - Scrambler|Only On New Maps", OnlyOnNewMaps.GetType(), OnlyOnNewMaps));

                    lstReturn.Add(new CPluginVariable("4 - Scrambler|Only On Final Ticket Percentage >=", OnlyOnFinalTicketPercentage.GetType(), OnlyOnFinalTicketPercentage));
                }

                var_name = "4 - Scrambler|Scramble By";
                var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(DefineStrong))) + ")";

                lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(DefineStrong), ScrambleBy)));

                lstReturn.Add(new CPluginVariable("4 - Scrambler|Keep Squads Together", KeepSquadsTogether.GetType(), KeepSquadsTogether));

                if (!KeepSquadsTogether || fRevealSettings)
                {
                    lstReturn.Add(new CPluginVariable("4 - Scrambler|Keep Clan Tags In Same Team", KeepClanTagsInSameTeam.GetType(), KeepClanTagsInSameTeam));
                }

                if ((!KeepSquadsTogether && KeepClanTagsInSameTeam) || fRevealSettings)
                {
                    lstReturn.Add(new CPluginVariable("4 - Scrambler|Keep Friends In Same Team", KeepFriendsInSameTeam.GetType(), KeepFriendsInSameTeam));
                }

                var_name = "4 - Scrambler|Divide By";
                var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(DivideByChoices))) + ")";

                lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(DivideByChoices), DivideBy)));

                if (DivideBy == DivideByChoices.ClanTag || fRevealSettings)
                {
                    lstReturn.Add(new CPluginVariable("4 - Scrambler|Clan Tag To Divide By", ClanTagToDivideBy.GetType(), ClanTagToDivideBy));
                }

                lstReturn.Add(new CPluginVariable("4 - Scrambler|Delay Seconds", DelaySeconds.GetType(), DelaySeconds));

                /* ===== SECTION 5 - Messages ===== */

                lstReturn.Add(new CPluginVariable("5 - Messages|Quiet Mode", QuietMode.GetType(), QuietMode));

                lstReturn.Add(new CPluginVariable("5 - Messages|Yell Duration Seconds", YellDurationSeconds.GetType(), YellDurationSeconds));

                lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Moved For Balance", ChatMovedForBalance.GetType(), ChatMovedForBalance));

                lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Moved For Balance", YellMovedForBalance.GetType(), YellMovedForBalance));

                if (EnableUnstacking)
                {
                    lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Moved To Unstack", ChatMovedToUnstack.GetType(), ChatMovedToUnstack));

                    lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Moved To Unstack", YellMovedToUnstack.GetType(), YellMovedToUnstack));
                }

                lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Detected Bad Team Switch", ChatDetectedBadTeamSwitch.GetType(), ChatDetectedBadTeamSwitch));

                lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Detected Bad Team Switch", YellDetectedBadTeamSwitch.GetType(), YellDetectedBadTeamSwitch));

                lstReturn.Add(new CPluginVariable("5 - Messages|Bad Because: Moved By Balancer", BadBecauseMovedByBalancer.GetType(), BadBecauseMovedByBalancer));

                lstReturn.Add(new CPluginVariable("5 - Messages|Bad Because: Winning Team", BadBecauseWinningTeam.GetType(), BadBecauseWinningTeam));

                lstReturn.Add(new CPluginVariable("5 - Messages|Bad Because: Biggest Team", BadBecauseBiggestTeam.GetType(), BadBecauseBiggestTeam));

                lstReturn.Add(new CPluginVariable("5 - Messages|Bad Because: Rank", BadBecauseRank.GetType(), BadBecauseRank));

                lstReturn.Add(new CPluginVariable("5 - Messages|Bad Because: Dispersal List", BadBecauseDispersalList.GetType(), BadBecauseDispersalList));

                lstReturn.Add(new CPluginVariable("5 - Messages|Bad Because: Clan", BadBecauseClan.GetType(), BadBecauseClan)); // DCE

                lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Detected Good Team Switch", ChatDetectedGoodTeamSwitch.GetType(), ChatDetectedGoodTeamSwitch));

                lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Detected Good Team Switch", YellDetectedGoodTeamSwitch.GetType(), YellDetectedGoodTeamSwitch));

                lstReturn.Add(new CPluginVariable("5 - Messages|Chat: After Unswitching", ChatAfterUnswitching.GetType(), ChatAfterUnswitching));

                lstReturn.Add(new CPluginVariable("5 - Messages|Yell: After Unswitching", YellAfterUnswitching.GetType(), YellAfterUnswitching));

                lstReturn.Add(new CPluginVariable("5 - Messages|Teams Will Be Scrambled", TeamsWillBeScrambled.GetType(), TeamsWillBeScrambled));

                lstReturn.Add(new CPluginVariable("5 - Messages|Chat: Autobalancing", ChatAutobalancing.GetType(), ChatAutobalancing));

                lstReturn.Add(new CPluginVariable("5 - Messages|Yell: Autobalancing", YellAutobalancing.GetType(), YellAutobalancing));


                /* ===== SECTION 6 - Unswitcher ===== */

                var_name = "6 - Unswitcher|Forbid Switching After Autobalance";
                var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(UnswitchChoice))) + ")";

                lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(UnswitchChoice), ForbidSwitchingAfterAutobalance)));

                var_name = "6 - Unswitcher|Forbid Switching To Winning Team";

                lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(UnswitchChoice), ForbidSwitchingToWinningTeam)));

                var_name = "6 - Unswitcher|Forbid Switching To Biggest Team";

                lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(UnswitchChoice), ForbidSwitchingToBiggestTeam)));

                var_name = "6 - Unswitcher|Forbid Switching After Dispersal";

                lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(UnswitchChoice), ForbidSwitchingAfterDispersal)));

                lstReturn.Add(new CPluginVariable("6 - Unswitcher|Enable Immediate Unswitch", EnableImmediateUnswitch.GetType(), EnableImmediateUnswitch));

                /* ===== SECTION 7 - TBD ===== */

                /* ===== SECTION 8 - Per-Mode Settings ===== */

                foreach (String sm in simpleModes)
                {
                    // No settings for non-balancing modes
                    if (fGameVersion == GameVersion.BFH && Regex.Match(sm, @"(Rescue|Crosshair)", RegexOptions.IgnoreCase).Success)
                    {
                        continue;
                    }

                    // Get settings
                    PerModeSettings oneSet = null;
                    if (!fPerMode.ContainsKey(sm))
                    {
                        oneSet = new PerModeSettings(sm, fGameVersion);
                        fPerMode[sm] = oneSet;
                    }
                    else
                    {
                        oneSet = fPerMode[sm];
                    }

                    bool isCTF = (sm == "CTF");
                    bool isGM = (sm == "Gun Master");
                    bool isRush = (sm.Contains("Rush"));
                    bool isSQDM = (sm == "Squad Deathmatch");
                    bool isConquest = (sm.Contains("Conq"));
                    bool isCarrierAssault = (sm.Contains("Carrier"));
                    bool isObliteration = (sm.Contains("Obliteration"));

                    lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Max Players", oneSet.MaxPlayers.GetType(), oneSet.MaxPlayers));

                    if (!isGM)
                    {

                        lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Rout Percentage", oneSet.RoutPercentage.GetType(), oneSet.RoutPercentage));

                        if (EnableUnstacking)
                        {
                            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Check Team Stacking After First Minutes", oneSet.CheckTeamStackingAfterFirstMinutes.GetType(), oneSet.CheckTeamStackingAfterFirstMinutes));

                            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Max Unstacking Swaps Per Round", oneSet.MaxUnstackingSwapsPerRound.GetType(), oneSet.MaxUnstackingSwapsPerRound));

                            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Number Of Swaps Per Group", oneSet.NumberOfSwapsPerGroup.GetType(), oneSet.NumberOfSwapsPerGroup));

                            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Delay Seconds Between Swap Groups", oneSet.DelaySecondsBetweenSwapGroups.GetType(), oneSet.DelaySecondsBetweenSwapGroups));

                            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Max Unstacking Ticket Difference", oneSet.MaxUnstackingTicketDifference.GetType(), oneSet.MaxUnstackingTicketDifference));

                            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Enable Unstacking By Player Stats", oneSet.EnableUnstackingByPlayerStats.GetType(), oneSet.EnableUnstackingByPlayerStats));
                        }

                        var_name = "8 - Settings for " + sm + "|" + sm + ": " + "Determine Strong Players By";
                        var_type = "enum." + var_name + "(" + String.Join("|", Enum.GetNames(typeof(DefineStrong))) + ")";

                        lstReturn.Add(new CPluginVariable(var_name, var_type, Enum.GetName(typeof(DefineStrong), oneSet.DetermineStrongPlayersBy)));

                        lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Percent Of Top Of Team Is Strong", oneSet.PercentOfTopOfTeamIsStrong.GetType(), oneSet.PercentOfTopOfTeamIsStrong));

                    }

                    lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Only Move Weak Players", oneSet.OnlyMoveWeakPlayers.GetType(), oneSet.OnlyMoveWeakPlayers));

                    lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Disperse Evenly By Rank >=", oneSet.DisperseEvenlyByRank.GetType(), oneSet.DisperseEvenlyByRank));

                    lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Disperse Evenly By Clan Players >=", oneSet.DisperseEvenlyByClanPlayers.GetType(), oneSet.DisperseEvenlyByClanPlayers));

                    lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Enable Disperse Evenly List", oneSet.EnableDisperseEvenlyList.GetType(), oneSet.EnableDisperseEvenlyList));

                    if (oneSet.EnableDisperseEvenlyList || oneSet.DisperseEvenlyByClanPlayers > 1)
                    {
                        lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Enable Strict Dispersal", oneSet.EnableStrictDispersal.GetType(), oneSet.EnableStrictDispersal));
                    }

                    lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Enable Low Population Adjustments", oneSet.EnableLowPopulationAdjustments.GetType(), oneSet.EnableLowPopulationAdjustments));

                    lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of High Population For Players >=", oneSet.DefinitionOfHighPopulationForPlayers.GetType(), oneSet.DefinitionOfHighPopulationForPlayers));

                    lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Low Population For Players <=", oneSet.DefinitionOfLowPopulationForPlayers.GetType(), oneSet.DefinitionOfLowPopulationForPlayers));

                    if (isCTF || isCarrierAssault || isObliteration)
                    {
                        lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Early Phase As Minutes From Start", oneSet.DefinitionOfEarlyPhaseFromStart.GetType(), oneSet.DefinitionOfEarlyPhaseFromStart));

                        lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Late Phase As Minutes From End", oneSet.DefinitionOfLatePhaseFromEnd.GetType(), oneSet.DefinitionOfLatePhaseFromEnd));
                    }
                    else if (!isGM)
                    {
                        lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Early Phase As Tickets From Start", oneSet.DefinitionOfEarlyPhaseFromStart.GetType(), oneSet.DefinitionOfEarlyPhaseFromStart));

                        lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Definition Of Late Phase As Tickets From End", oneSet.DefinitionOfLatePhaseFromEnd.GetType(), oneSet.DefinitionOfLatePhaseFromEnd));
                    }

                    if (!isSQDM)
                    {
                        lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Enable Scrambler", oneSet.EnableScrambler.GetType(), oneSet.EnableScrambler));

                    }

                    if (isRush && EnableUnstacking)
                    {
                        lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Stage 1 Ticket Percentage To Unstack Adjustment", oneSet.Stage1TicketPercentageToUnstackAdjustment.GetType(), oneSet.Stage1TicketPercentageToUnstackAdjustment));

                        lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Stage 2 Ticket Percentage To Unstack Adjustment", oneSet.Stage2TicketPercentageToUnstackAdjustment.GetType(), oneSet.Stage2TicketPercentageToUnstackAdjustment));

                        lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Stage 3 Ticket Percentage To Unstack Adjustment", oneSet.Stage3TicketPercentageToUnstackAdjustment.GetType(), oneSet.Stage3TicketPercentageToUnstackAdjustment));

                        lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Stage 4 And 5 Ticket Percentage To Unstack Adjustment", oneSet.Stage4And5TicketPercentageToUnstackAdjustment.GetType(), oneSet.Stage4And5TicketPercentageToUnstackAdjustment));

                        lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Seconds To Check For New Stage", oneSet.SecondsToCheckForNewStage.GetType(), oneSet.SecondsToCheckForNewStage));

                        lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Enable Advanced Rush Unstacking", oneSet.EnableAdvancedRushUnstacking.GetType(), oneSet.EnableAdvancedRushUnstacking));
                    }

                    if (isConquest)
                    {
                        // lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Enable Ticket Loss Ratio", oneSet.EnableTicketLossRatio.GetType(), oneSet.EnableTicketLossRatio)); // disable for this release

                        if (oneSet.EnableTicketLossRatio && false)
                        {
                            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Ticket Loss Sample Count", oneSet.TicketLossSampleCount.GetType(), oneSet.TicketLossSampleCount));
                        }
                        else
                        {
                            lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Enable Metro Adjustments", oneSet.EnableMetroAdjustments.GetType(), oneSet.EnableMetroAdjustments));

                            if (oneSet.EnableMetroAdjustments)
                            {
                                lstReturn.Add(new CPluginVariable("8 - Settings for " + sm + "|" + sm + ": " + "Metro Adjusted Definition Of Late Phase", oneSet.MetroAdjustedDefinitionOfLatePhase.GetType(), oneSet.MetroAdjustedDefinitionOfLatePhase));
                            }
                        }
                    }

                }

                /* ===== SECTION 9 - Debug Settings ===== */

                lstReturn.Add(new CPluginVariable("9 - Debugging|Show Command In Log", ShowCommandInLog.GetType(), ShowCommandInLog));

                lstReturn.Add(new CPluginVariable("9 - Debugging|Log Chat", LogChat.GetType(), LogChat));

                lstReturn.Add(new CPluginVariable("9 - Debugging|Enable Logging Only Mode", EnableLoggingOnlyMode.GetType(), EnableLoggingOnlyMode));

                lstReturn.Add(new CPluginVariable("9 - Debugging|Enable External Logging", EnableExternalLogging.GetType(), EnableExternalLogging));

                if (EnableExternalLogging || fRevealSettings)
                {

                    lstReturn.Add(new CPluginVariable("9 - Debugging|External Log Suffix", ExternalLogSuffix.GetType(), ExternalLogSuffix));

                }

                if (fShowRiskySettings || fRevealSettings)
                {
                    lstReturn.Add(new CPluginVariable("9 - Debugging|Enable Risky Features", EnableRiskyFeatures.GetType(), EnableRiskyFeatures));
                }


            }
            catch (Exception e)
            {
                ConsoleException(e);
            }

            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            fRevealSettings = true;
            List<CPluginVariable> lstReturn = null;
            try
            {
                lstReturn = GetDisplayPluginVariables();
            }
            catch (Exception)
            {
                if (lstReturn == null) lstReturn = new List<CPluginVariable>();
            }
            fRevealSettings = false;

            // pre-v1 legacy settings
            lstReturn.Add(new CPluginVariable("6 - Unswitcher|Forbid Switch After Autobalance", ForbidSwitchAfterAutobalance.GetType(), ForbidSwitchAfterAutobalance));
            lstReturn.Add(new CPluginVariable("6 - Unswitcher|Forbid Switch To Winning Team", ForbidSwitchToWinningTeam.GetType(), ForbidSwitchToWinningTeam));
            lstReturn.Add(new CPluginVariable("6 - Unswitcher|Forbid Switch To Biggest Team", ForbidSwitchToBiggestTeam.GetType(), ForbidSwitchToBiggestTeam));
            lstReturn.Add(new CPluginVariable("6 - Unswitcher|Forbid Switch After Dispersal", ForbidSwitchAfterDispersal.GetType(), ForbidSwitchAfterDispersal));
            lstReturn.Add(new CPluginVariable("9 - Debugging|Show In Log", ShowInLog.GetType(), ShowInLog));
            // hidden setting
            lstReturn.Add(new CPluginVariable("0 - Presets|Settings Version", SettingsVersion.GetType(), SettingsVersion));
            return lstReturn;
        }

        public void SetPluginVariable(String strVariable, String strValue)
        {
            bool isPresetVar = false;
            bool isReminderVar = false;

            if (fIsEnabled) DebugWrite(strVariable + " <- " + strValue, 6);

            try
            {
                if (strVariable.Contains("Show In Log") && String.IsNullOrEmpty(strValue))
                {
                    DebugWrite("^8Detected pre-v1 settings, upgrading ...", 3);
                    UpgradePreV1Settings();
                    strValue = INVALID_NAME_TAG_GUID; // mark as upgraded
                }
                else if (strVariable.Contains("Settings Version"))
                {
                    DebugWrite("^1Settings Version = " + strValue, 3);
                }
                String tmp = strVariable;
                int pipeIndex = strVariable.IndexOf('|');
                if (pipeIndex >= 0)
                {
                    pipeIndex++;
                    tmp = strVariable.Substring(pipeIndex, strVariable.Length - pipeIndex);
                }
                if (tmp.Contains("(Low, Med, High population)"))
                {
                    tmp = tmp.Replace("(Low, Med, High population)", String.Empty);
                }

                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

                String propertyName = Regex.Replace(tmp, @"[^a-zA-Z_0-9]", String.Empty);

                if (strVariable.Contains("preset")) propertyName = "Preset";

                FieldInfo field = this.GetType().GetField(propertyName, flags);

                Type fieldType = null;


                if (!strVariable.Contains("Settings for") && field != null)
                {
                    fieldType = field.GetValue(this).GetType();
                    if (strVariable.Contains("preset"))
                    {
                        fieldType = typeof(PresetItems);
                        try
                        {
                            Preset = (PresetItems)Enum.Parse(fieldType, strValue);
                            isPresetVar = true;
                        }
                        catch (Exception e)
                        {
                            ConsoleException(e);
                        }
                    }
                    else if (tmp.Contains("Spelling Of Speed Names Reminder"))
                    {
                        fieldType = typeof(Speed);
                        try
                        {
                            field.SetValue(this, (Speed)Enum.Parse(fieldType, strValue));
                            isReminderVar = true;
                        }
                        catch (Exception e)
                        {
                            ConsoleException(e);
                        }
                    }
                    else if (tmp.Contains("Balance Speed"))
                    {
                        fieldType = typeof(Speed[]);
                        try
                        {
                            // Parse the list into an array of enum vals
                            Speed[] items = MULTIbalancerUtils.ParseSpeedArray(this, strValue); // also validates
                            field.SetValue(this, items);
                        }
                        catch (Exception e)
                        {
                            ConsoleException(e);
                        }
                    }
                    else if (tmp.Contains("Ticket Percentage To Unstack"))
                    {
                        fieldType = typeof(double[]);
                        try
                        {
                            // Parse the list into an array of numbers
                            double[] nums = MULTIbalancerUtils.ParseNumArray(strValue); // also validates
                            field.SetValue(this, nums);
                        }
                        catch (Exception e)
                        {
                            ConsoleException(e);
                        }
                    }
                    else if (tmp.Contains("Scramble By"))
                    {
                        fieldType = typeof(DefineStrong);
                        try
                        {
                            field.SetValue(this, (DefineStrong)Enum.Parse(fieldType, strValue));
                        }
                        catch (Exception e)
                        {
                            ConsoleException(e);
                        }
                    }
                    else if (tmp == "Divide By")
                    {
                        fieldType = typeof(DivideByChoices);
                        try
                        {
                            field.SetValue(this, (DivideByChoices)Enum.Parse(fieldType, strValue));
                        }
                        catch (Exception e)
                        {
                            ConsoleException(e);
                        }
                    }
                    else if (tmp.Contains("Preferred Style Of Balancing"))
                    {
                        fieldType = typeof(PresetItems);
                        try
                        {
                            field.SetValue(this, (PresetItems)Enum.Parse(fieldType, strValue));
                        }
                        catch (Exception e)
                        {
                            ConsoleException(e);
                        }
                    }
                    else if (tmp.Contains("Forbid Switching"))
                    {
                        fieldType = typeof(UnswitchChoice);
                        try
                        {
                            field.SetValue(this, (UnswitchChoice)Enum.Parse(fieldType, strValue));
                        }
                        catch (Exception e)
                        {
                            ConsoleException(e);
                        }
                    }
                    else if (tmp.Contains("Which Battlelog Stats"))
                    {
                        fieldType = typeof(BattlelogStats);
                        try
                        {
                            field.SetValue(this, (BattlelogStats)Enum.Parse(fieldType, strValue));
                        }
                        catch (Exception e)
                        {
                            ConsoleException(e);
                        }
                    }
                    else if (tmp.Contains("Select Fast Balance By"))
                    {
                        fieldType = typeof(ForceMove);
                        try
                        {
                            field.SetValue(this, (ForceMove)Enum.Parse(fieldType, strValue));
                        }
                        catch (Exception e)
                        {
                            ConsoleException(e);
                        }
                    }
                    else if (fEasyTypeDict.ContainsValue(fieldType))
                    {
                        field.SetValue(this, TypeDescriptor.GetConverter(fieldType).ConvertFromString(strValue));
                    }
                    else if (fListStrDict.ContainsValue(fieldType))
                    {
                        if (DebugLevel >= 8) ConsoleDebug("String array " + propertyName + " <- " + strValue);
                        field.SetValue(this, CPluginVariable.DecodeStringArray(strValue));
                        if (propertyName == "Whitelist")
                        {
                            UpdateWhitelistModel();
                            /*
                            MergeWithFile(Whitelist, fSettingWhitelist);
                            if (EnableWhitelistingOfReservedSlotsList) MergeWhitelistWithReservedSlots();
                            UpdateAllFromWhitelist();
                            if (DebugLevel >= 8) {
                                String l = "Whitelist: ";
                                l = l + String.Join(", ", fSettingWhitelist.ToArray());
                                ConsoleDebug(l);
                            }
                            */
                        }
                        else if (propertyName == "DisperseEvenlyList")
                        {
                            MergeWithFile(DisperseEvenlyList, fSettingDisperseEvenlyList); // clears fSettingDispersEvenlyList
                            SetDispersalListGroups();
                            AssignGroups();
                        }
                        else if (propertyName == "FriendsList")
                        {
                            MergeWithFile(FriendsList, fSettingFriendsList); // clears fSettingFriendsList
                            SetFriends();
                        }
                    }
                    else if (fBoolDict.ContainsValue(fieldType))
                    {
                        if (fIsEnabled) DebugWrite(propertyName + " strValue = " + strValue, 6);
                        if (Regex.Match(strValue, "true", RegexOptions.IgnoreCase).Success)
                        {
                            field.SetValue(this, true);
                        }
                        else
                        {
                            field.SetValue(this, false);
                        }
                    }
                    else
                    {
                        if (DebugLevel >= 8) ConsoleDebug("Unknown var " + propertyName + " with type " + fieldType);
                    }
                }
                else
                {
                    Match m = Regex.Match(tmp, @"([^:]+):\s([^:]+)$");

                    if (m.Success)
                    {
                        String mode = m.Groups[1].Value;
                        String fieldPart = m.Groups[2].Value.Replace(" ", "");
                        String perModeSetting = Regex.Replace(fieldPart, @"[^a-zA-Z_0-9]", String.Empty);

                        perModeSetting = Regex.Replace(perModeSetting, @"(?:AsTickets|AsMinutes)", String.Empty);

                        if (!fPerMode.ContainsKey(mode))
                        {
                            fPerMode[mode] = new PerModeSettings(mode, fGameVersion);
                        }
                        PerModeSettings pms = fPerMode[mode];

                        field = pms.GetType().GetField(perModeSetting, flags);

                        if (fIsEnabled) DebugWrite("Mode: " + mode + ", Field: " + perModeSetting + ", Value: " + strValue, 6);

                        if (field != null)
                        {
                            fieldType = field.GetValue(pms).GetType();
                            if (fEasyTypeDict.ContainsValue(fieldType))
                            {
                                field.SetValue(pms, TypeDescriptor.GetConverter(fieldType).ConvertFromString(strValue));
                            }
                            else if (fListStrDict.ContainsValue(fieldType))
                            {
                                field.SetValue(pms, new List<String>(CPluginVariable.DecodeStringArray(strValue)));
                            }
                            else if (fBoolDict.ContainsValue(fieldType))
                            {
                                if (Regex.Match(strValue, "true", RegexOptions.IgnoreCase).Success)
                                {
                                    field.SetValue(pms, true);
                                }
                                else
                                {
                                    field.SetValue(pms, false);
                                }
                            }
                            else if (strVariable.Contains("Determine Strong"))
                            {
                                fieldType = typeof(DefineStrong);
                                try
                                {
                                    field.SetValue(pms, (DefineStrong)Enum.Parse(fieldType, strValue));
                                }
                                catch (Exception e)
                                {
                                    ConsoleException(e);
                                }
                            }
                        }
                        else
                        {
                            if (fIsEnabled) DebugWrite("field is null", 6);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                ConsoleException(e);
            }
            finally
            {

                if (!isReminderVar)
                {
                    // Reset to show hint
                    SpellingOfSpeedNamesReminder = Speed.Click_Here_For_Speed_Names;
                }

                if (isPresetVar)
                {
                    // Update other settings based on new preset value
                    MULTIbalancerUtils.UpdateSettingsForPreset(this, Preset);
                }
                else
                {
                    // Update Preset value based on current settings
                    UpdatePresetValue();
                }

                if (strVariable.Contains("Apply Settings Changes") && ApplySettingsChanges)
                {
                    ApplySettingsChanges = false;
                    EnableSettingsWizard = false;
                    ApplyWizardSettings();
                }

                // Validate all values and correct if needed
                ValidateSettings(strVariable, strValue);

                // Handle show in log commands
                if (!String.IsNullOrEmpty(ShowCommandInLog))
                {
                    CommandToLog(ShowCommandInLog);
                    ShowCommandInLog = String.Empty;
                }

                // Handle risky settings
                if (!EnableRiskyFeatures)
                {
                    if (EnableTicketLossRateLogging)
                    {
                        ConsoleWarn("^8Setting ^bEnable Ticket Loss Rate Logging^n to False. This is an experimental setting and you have not enabled risky settings.");
                        EnableTicketLossRateLogging = false;
                    }
                }
            }
        }



        /*
        procon.protected.plugins.setVariable "MULTIbalancer" "1 - Settings|Whitelist" "Able B|Baker B U|Charlie B U S|None|Delta B U S R"
        procon.protected.plugins.setVariable "MULTIbalancer" "1 - Settings|Friends List" "AAA BBB CCC|XXX YYY ZZZ|Able Baker|Charlie Delta"
        procon.protected.plugins.setVariable "MULTIbalancer" "1 - Settings|Disperse Evenly List" "1 AAA BBB CCC|2 XXX YYY ZZZ|Able|Baker|Charlie|Delta"

        Command: 	procon.protected.plugins.setVariable <string: classname> <string: variablename> <string: value>
        Effect: 	Sets <classname> plugin’s <variablename> to <value>

        */

        private void ForceSetPluginVariable(String strVariable, String[] values)
        {
            try
            {
                ForceSetPluginVariable(strVariable, String.Join("|", values));
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }


        private void ForceSetPluginVariable(String strVariable, String strValue)
        {
            try
            {
                if (DebugLevel >= 7) ConsoleDebug("procon.protected.plugins.setVariable" + ", " + GetPluginName() + ", " + strVariable + ", " + strValue);
                this.ExecuteCommand("procon.protected.plugins.setVariable", GetPluginName(), strVariable, strValue);
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }



        private bool ValidateSettings(String strVariable, String strValue)
        {
            try
            {

                /* ===== SECTION 1 - Settings ===== */

                if (strVariable.Contains("Debug Level")) { ValidateIntRange(ref DebugLevel, "Debug Level", 0, 9, 2, false); }
                else if (strVariable.Contains("Maximum Server Size")) { ValidateIntRange(ref MaximumServerSize, "Maximum Server Size", 8, 70, 64, false); }
                else if (strVariable.Contains("Maximum Request Rate")) { ValidateIntRange(ref MaximumRequestRate, "Maximum Request Rate", 1, 15, 10, true); } // in 20 seconds
                else if (strVariable.Contains("Wait Timeout")) { ValidateDoubleRange(ref WaitTimeout, "Wait Timeout", 15, 90, 30, false); }
                else if (strVariable.Contains("Unlimited Team Switching During First Minutes Of Round")) { ValidateDouble(ref UnlimitedTeamSwitchingDuringFirstMinutesOfRound, "Unlimited Team Switching During First Minutes Of Round", 5.0); }
                else if (strVariable.Contains("Seconds Until Adaptive Speed Becomes Fast")) { ValidateDoubleRange(ref SecondsUntilAdaptiveSpeedBecomesFast, "Seconds Until Adaptive Speed Becomes Fast", MIN_ADAPT_FAST, 999999, 3 * 60, true); } // 3 minutes default

                /* ===== SECTION 2 - Exclusions ===== */

                else if (strVariable.Contains("Minutes After Joining")) { ValidateDouble(ref MinutesAfterJoining, "Minutes After Joining", 5); }
                else if (strVariable.Contains("Minutes After Being Moved")) { ValidateDouble(ref MinutesAfterBeingMoved, "Minutes After Being Moved", 5); }

                /* ===== SECTION 3 - Round Phase & Population Settings ===== */

                for (int i = 0; i < EarlyPhaseTicketPercentageToUnstack.Length; ++i)
                {
                    if (strVariable.Contains("Early Phase: Ticket Percentage To Unstack")) ValidateDoubleRange(ref EarlyPhaseTicketPercentageToUnstack[i], "Early Phase Ticket Percentage To Unstack", 100.0, 5000.0, 120.0, true);
                }
                for (int i = 0; i < MidPhaseTicketPercentageToUnstack.Length; ++i)
                {
                    if (strVariable.Contains("Mid Phase: Ticket Percentage To Unstack")) ValidateDoubleRange(ref MidPhaseTicketPercentageToUnstack[i], "Mid Phase Ticket Percentage To Unstack", 100.0, 5000.0, 120.0, true);
                }
                for (int i = 0; i < LatePhaseTicketPercentageToUnstack.Length; ++i)
                {
                    if (strVariable.Contains("Late Phase: Ticket Percentage To Unstack")) ValidateDoubleRange(ref LatePhaseTicketPercentageToUnstack[i], "Late Phase Ticket Percentage To Unstack", 100.0, 5000.0, 120.0, true);
                }

                /* ===== SECTION 4 - Scrambler ===== */

                if (strVariable.Contains("Only On Final Ticket Percentage")) { ValidateDoubleRange(ref OnlyOnFinalTicketPercentage, "Only On Final Ticket Percentage", 100.0, 1000.0, 120.0, true); }

                else if (strVariable.Contains("Delay Seconds")) { ValidateDoubleRange(ref DelaySeconds, "Delay Seconds", 0, 70, 30, false); }

                /* ===== SECTION 5 - Messages ===== */

                else if (strVariable.Contains("Yell Duration Seconds")) { ValidateDoubleRange(ref YellDurationSeconds, "Yell Duration Seconds", 1, 20, 10, true); }

                else if (strVariable.Contains("Chat: Moved For Balance") && ChatMovedForBalance.Contains("%reason%"))
                {
                    ConsoleWarn(strVariable + ": contains %reason%, which is only recognized in ^bDetected Bad Team Switch^n");
                }
                else if (strVariable.Contains("Yell: Moved For Balance") && YellMovedForBalance.Contains("%reason%"))
                {
                    ConsoleWarn(strVariable + ": contains %reason%, which is only recognized in ^bDetected Bad Team Switch^n");
                }
                else if (strVariable.Contains("Chat: Moved To Unstack") && ChatMovedToUnstack.Contains("%reason%"))
                {
                    ConsoleWarn(strVariable + ": contains %reason%, which is only recognized in ^bDetected Bad Team Switch^n");
                }
                else if (strVariable.Contains("Yell: Moved To Unstack") && YellMovedToUnstack.Contains("%reason%"))
                {
                    ConsoleWarn(strVariable + ": contains %reason%, which is only recognized in ^bDetected Bad Team Switch^n");
                }
                else if (strVariable.Contains("Chat: Detected Good Team Switch") && ChatDetectedGoodTeamSwitch.Contains("%reason%"))
                {
                    ConsoleWarn(strVariable + ": contains %reason%, which is only recognized in ^bDetected Bad Team Switch^n");
                }
                else if (strVariable.Contains("Yell: Detected Good Team Switch") && YellDetectedGoodTeamSwitch.Contains("%reason%"))
                {
                    ConsoleWarn(strVariable + ": contains %reason%, which is only recognized in ^bDetected Bad Team Switch^n");
                }

                /* ===== SECTION 6 ===== */

                /* ===== SECTION 7 - TBD ===== */

                /* ===== SECTION 8 - Per-Mode Settings ===== */

                foreach (String mode in fPerMode.Keys)
                {
                    PerModeSettings perMode = fPerMode[mode];
                    PerModeSettings def = new PerModeSettings(mode, fGameVersion); // defaults for this mode

                    def.MaxPlayers = Math.Min(def.MaxPlayers, MaximumServerSize);
                    def.NumberOfSwapsPerGroup = Math.Min(def.NumberOfSwapsPerGroup, perMode.MaxUnstackingSwapsPerRound);
                    def.DefinitionOfHighPopulationForPlayers = Math.Min(def.DefinitionOfHighPopulationForPlayers, perMode.MaxPlayers);
                    def.DefinitionOfLowPopulationForPlayers = Math.Min(def.DefinitionOfLowPopulationForPlayers, perMode.MaxPlayers);
                    if (strVariable.Contains("Max Players")) ValidateIntRange(ref perMode.MaxPlayers, mode + ":" + "Max Players", 8, MaximumServerSize, def.MaxPlayers, false);
                    else if (strVariable.Contains("Rout Percentage")) ValidateDoubleRange(ref perMode.RoutPercentage, mode + ":" + "Rout Percentage", 101, 10000, def.RoutPercentage, true);
                    else if (strVariable.Contains("Check Team Stacking After First Minutes")) ValidateDouble(ref perMode.CheckTeamStackingAfterFirstMinutes, mode + ":" + "Check Team Stacking After First Minutes", def.CheckTeamStackingAfterFirstMinutes);
                    else if (strVariable.Contains("Max Unstacking Swaps Per Round")) ValidateInt(ref perMode.MaxUnstackingSwapsPerRound, mode + ":" + "Max Unstacking Swaps Per Round", def.MaxUnstackingSwapsPerRound);
                    else if (strVariable.Contains("Number Of Swaps Per Group")) ValidateIntRange(ref perMode.NumberOfSwapsPerGroup, mode + ":" + "Number Of Swaps Per Group", 0, perMode.MaxUnstackingSwapsPerRound, def.NumberOfSwapsPerGroup, false);
                    else if (strVariable.Contains("Delay Seconds Between Swap Groups")) ValidateDoubleRange(ref perMode.DelaySecondsBetweenSwapGroups, mode + ":" + "Delay Seconds Between Swap Groups", 60, 24 * 60 * 60, def.DelaySecondsBetweenSwapGroups, false);
                    else if (strVariable.Contains("Max Unstacking Ticket Difference")) ValidateInt(ref perMode.MaxUnstackingTicketDifference, mode + ":" + "Max Unstacking Ticket Difference", def.MaxUnstackingTicketDifference);
                    else if (strVariable.Contains("Percent Of Top Of Team Is Strong")) ValidateDoubleRange(ref perMode.PercentOfTopOfTeamIsStrong, mode + ":" + "Percent Of Top Of Team Is Strong", 5, 50, def.PercentOfTopOfTeamIsStrong, false);
                    else if (strVariable.Contains("Disperse Evenly By Rank")) ValidateIntRange(ref perMode.DisperseEvenlyByRank, mode + ":" + "Disperse Evenly By Rank", 0, 145, def.DisperseEvenlyByRank, true);
                    else if (strVariable.Contains("Disperse Evenly By Clan Players")) ValidateIntRange(ref perMode.DisperseEvenlyByClanPlayers, mode + ":" + "Disperse Evenly By Clan Players", 4, 40, def.DisperseEvenlyByRank, true);
                    else if (strVariable.Contains("Definition Of High Population For Players")) ValidateIntRange(ref perMode.DefinitionOfHighPopulationForPlayers, mode + ":" + "Definition Of High Population For Players", 0, perMode.MaxPlayers, def.DefinitionOfHighPopulationForPlayers, false);
                    else if (strVariable.Contains("Definition Of Low Population For Players")) ValidateIntRange(ref perMode.DefinitionOfLowPopulationForPlayers, mode + ":" + "Definition Of Low Population For Players", 0, perMode.MaxPlayers, def.DefinitionOfLowPopulationForPlayers, false);
                    else if (strVariable.Contains("Ticket Loss Sample Count")) ValidateIntRange(ref perMode.TicketLossSampleCount, mode + ":" + "Ticket Loss Sample Count", MIN_SAMPLE_COUNT, 1200, def.TicketLossSampleCount, false);
                    else if (strVariable.Contains("Definition Of Early Phase")) ValidateInt(ref perMode.DefinitionOfEarlyPhaseFromStart, mode + ":" + "Definition Of Early Phase From Start", def.DefinitionOfEarlyPhaseFromStart);
                    else if (strVariable.Contains("Metro Adjusted Definition Of Late Phase")) ValidateInt(ref perMode.MetroAdjustedDefinitionOfLatePhase, mode + ":" + "Metro Adjusted Definition Of Late Phase", def.MetroAdjustedDefinitionOfLatePhase);
                    else if (strVariable.Contains("Definition Of Late Phase")) ValidateInt(ref perMode.DefinitionOfLatePhaseFromEnd, mode + ":" + "Definition Of Late Phase From End", def.DefinitionOfLatePhaseFromEnd);
                    if (mode == "CTF" || mode.Contains("Carrier"))
                    {
                        int maxMinutes = (mode == "CTF") ? 60 : 90; // TBD, might need to factor in gameModeCounter
                        if (strVariable.Contains("Definition Of Late Phase") && perMode.DefinitionOfLatePhaseFromEnd > maxMinutes)
                        {
                            ConsoleError("^b" + "Definition Of Late Phase" + "^n must be less than or equal to " + maxMinutes + " minutes, corrected to " + maxMinutes);
                            perMode.DefinitionOfEarlyPhaseFromStart = 0;
                        }
                        else if (strVariable.Contains("Definition Of Early Phase") && perMode.DefinitionOfEarlyPhaseFromStart > (maxMinutes - perMode.DefinitionOfLatePhaseFromEnd))
                        {
                            ConsoleError("^b" + "Definition Of Early Phase" + "^n must be less than or equal to " + (maxMinutes - perMode.DefinitionOfLatePhaseFromEnd) + " minutes, corrected to " + (maxMinutes - perMode.DefinitionOfLatePhaseFromEnd));
                            perMode.DefinitionOfEarlyPhaseFromStart = maxMinutes - perMode.DefinitionOfLatePhaseFromEnd;
                        }
                    }
                    else if (mode == "Rush" || mode == "Squad Rush")
                    {
                        if (strVariable.Contains("Seconds To Check For New Stage")) ValidateDoubleRange(ref perMode.SecondsToCheckForNewStage, mode + ":" + "Seconds To Check For New Stage", 5, 30, def.SecondsToCheckForNewStage, false);
                    }
                }

                /* ===== SECTION 9 - Debug Settings ===== */


            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
            return true;
        }

        private void ResetSettings()
        {
            MULTIbalancer rhs = new MULTIbalancer();

            /* ===== SECTION 0 - Presets ===== */

            Preset = rhs.Preset;
            // EnableUnstacking = rhs.EnableUnstacking; // don't reset EnableUnstacking
            EnableAdminKillForFastBalance = rhs.EnableAdminKillForFastBalance;
            SelectFastBalanceBy = rhs.SelectFastBalanceBy;

            /* ===== SECTION 1 - Settings ===== */

            DebugLevel = rhs.DebugLevel;
            MaximumServerSize = rhs.MaximumServerSize;
            EnableBattlelogRequests = rhs.EnableBattlelogRequests;
            MaximumRequestRate = rhs.MaximumRequestRate;
            WaitTimeout = rhs.WaitTimeout;
            WhichBattlelogStats = rhs.WhichBattlelogStats;
            MaxTeamSwitchesByStrongPlayers = rhs.MaxTeamSwitchesByStrongPlayers;
            MaxTeamSwitchesByWeakPlayers = rhs.MaxTeamSwitchesByWeakPlayers;
            UnlimitedTeamSwitchingDuringFirstMinutesOfRound = rhs.UnlimitedTeamSwitchingDuringFirstMinutesOfRound;
            Enable2SlotReserve = rhs.Enable2SlotReserve;
            EnablerecruitCommand = rhs.EnablerecruitCommand;
            EnableWhitelistingOfReservedSlotsList = rhs.EnableWhitelistingOfReservedSlotsList;
            SecondsUntilAdaptiveSpeedBecomesFast = rhs.SecondsUntilAdaptiveSpeedBecomesFast;
            EnableInGameCommands = rhs.EnableInGameCommands;
            ReassignNewPlayers = rhs.ReassignNewPlayers;
            // Whitelist = rhs.Whitelist; // don't reset the whitelist
            // DisperseEvenlyList = rhs.DisperseEvenlyList; // don't reset the dispersal list
            EnableTicketLossRateLogging = rhs.EnableTicketLossRateLogging;

            /* ===== SECTION 2 - Exclusions ===== */

            OnWhitelist = rhs.OnWhitelist;
            OnFriendsList = rhs.OnFriendsList;
            ApplyFriendsListToTeam = rhs.ApplyFriendsListToTeam;
            TopScorers = rhs.TopScorers;
            SameClanTagsInSquad = rhs.SameClanTagsInSquad;
            SameClanTagsInTeam = rhs.SameClanTagsInTeam;
            SameClanTagsForRankDispersal = rhs.SameClanTagsForRankDispersal;
            LenientRankDispersal = rhs.LenientRankDispersal;
            MinutesAfterJoining = rhs.MinutesAfterJoining;
            MinutesAfterBeingMoved = rhs.MinutesAfterBeingMoved;
            JoinedEarlyPhase = rhs.JoinedEarlyPhase;
            JoinedMidPhase = rhs.JoinedMidPhase;
            JoinedLatePhase = rhs.JoinedLatePhase;

            /* ===== SECTION 3 - Round Phase & Population Settings ===== */

            EarlyPhaseTicketPercentageToUnstack = rhs.EarlyPhaseTicketPercentageToUnstack;
            MidPhaseTicketPercentageToUnstack = rhs.MidPhaseTicketPercentageToUnstack;
            LatePhaseTicketPercentageToUnstack = rhs.LatePhaseTicketPercentageToUnstack;

            SpellingOfSpeedNamesReminder = rhs.SpellingOfSpeedNamesReminder;

            EarlyPhaseBalanceSpeed = rhs.EarlyPhaseBalanceSpeed;
            MidPhaseBalanceSpeed = rhs.MidPhaseBalanceSpeed;
            LatePhaseBalanceSpeed = rhs.LatePhaseBalanceSpeed;

            /* ===== SECTION 4 - Scrambler ===== */

            OnlyByCommand = rhs.OnlyByCommand;
            OnlyOnNewMaps = rhs.OnlyOnNewMaps;
            OnlyOnFinalTicketPercentage = rhs.OnlyOnFinalTicketPercentage;
            ScrambleBy = rhs.ScrambleBy;
            KeepClanTagsInSameTeam = rhs.KeepClanTagsInSameTeam;
            KeepFriendsInSameTeam = rhs.KeepFriendsInSameTeam;
            DivideBy = rhs.DivideBy;
            ClanTagToDivideBy = rhs.ClanTagToDivideBy;
            DelaySeconds = rhs.DelaySeconds;

            /* ===== SECTION 5 - Messages ===== */

            QuietMode = rhs.QuietMode;
            YellDurationSeconds = rhs.YellDurationSeconds;
            BadBecauseMovedByBalancer = rhs.BadBecauseMovedByBalancer;
            BadBecauseWinningTeam = rhs.BadBecauseWinningTeam;
            BadBecauseBiggestTeam = rhs.BadBecauseBiggestTeam;
            BadBecauseRank = rhs.BadBecauseRank;
            BadBecauseDispersalList = rhs.BadBecauseDispersalList;
            BadBecauseClan = rhs.BadBecauseClan; // DCE
            ChatMovedForBalance = rhs.ChatMovedForBalance;
            YellMovedForBalance = rhs.YellMovedForBalance;
            ChatMovedToUnstack = rhs.ChatMovedToUnstack;
            YellMovedToUnstack = rhs.YellMovedToUnstack;
            ChatDetectedBadTeamSwitch = rhs.ChatDetectedBadTeamSwitch;
            YellDetectedBadTeamSwitch = rhs.YellDetectedBadTeamSwitch;
            ChatDetectedGoodTeamSwitch = rhs.ChatDetectedGoodTeamSwitch;
            YellDetectedGoodTeamSwitch = rhs.YellDetectedGoodTeamSwitch;
            ChatAfterUnswitching = rhs.ChatAfterUnswitching;
            YellAfterUnswitching = rhs.YellAfterUnswitching;
            TeamsWillBeScrambled = rhs.TeamsWillBeScrambled;
            ChatAutobalancing = rhs.ChatAutobalancing;
            YellAutobalancing = rhs.YellAutobalancing;

            /* ===== SECTION 6 - Unswitcher ===== */

            ForbidSwitchingAfterAutobalance = rhs.ForbidSwitchingAfterAutobalance;
            ForbidSwitchingToWinningTeam = rhs.ForbidSwitchingToWinningTeam;
            ForbidSwitchingToBiggestTeam = rhs.ForbidSwitchingToBiggestTeam;
            ForbidSwitchingAfterDispersal = rhs.ForbidSwitchingAfterDispersal;
            EnableImmediateUnswitch = rhs.EnableImmediateUnswitch;

            /* ===== SECTION 7 - TBD ===== */

            /* ===== SECTION 8 - Per-Mode Settings ===== */

            List<String> simpleModes = GetSimplifiedModes();

            fPerMode.Clear();

            foreach (String sm in simpleModes)
            {
                PerModeSettings oneSet = null;
                if (!fPerMode.ContainsKey(sm))
                {
                    oneSet = new PerModeSettings(sm, fGameVersion);
                    fPerMode[sm] = oneSet;
                }
            }

            /* ===== SECTION 9 - Debug Settings ===== */

            ShowCommandInLog = rhs.ShowCommandInLog;
            LogChat = rhs.LogChat;
            EnableLoggingOnlyMode = rhs.EnableLoggingOnlyMode;
            EnableRiskyFeatures = rhs.EnableRiskyFeatures;
        }

        private void CommandToLog(string cmd)
        {
            try
            {
                Match m = null;
                String msg = String.Empty;
                ConsoleDump("Command: " + cmd);

                if (Regex.Match(cmd, @"^bad\s+tags?", RegexOptions.IgnoreCase).Success)
                {
                    List<String> failures = new List<String>();
                    lock (fKnownPlayers)
                    {
                        foreach (String name in fKnownPlayers.Keys)
                        {
                            PlayerModel p = fKnownPlayers[name];
                            if (p.Role != ROLE_PLAYER)
                                continue;

                            double joinedMinutesAgo = GetPlayerJoinedTimeSpan(p).TotalMinutes;
                            double enabledForMinutes = DateTime.Now.Subtract(fEnabledTimestamp).TotalMinutes;
                            if ((enabledForMinutes > MinutesAfterJoining)
                            && (joinedMinutesAgo > MinutesAfterJoining)
                            && (!p.TagVerified || p.TagFetchStatus.State == FetchState.Failed || p.TagFetchStatus.State == FetchState.Aborted))
                            {
                                failures.Add(name);
                            }
                        }
                    }
                    if (failures.Count == 0)
                    {
                        ConsoleDump("^bNo clan tag fetch failures to report");
                    }
                    else
                    {
                        String tmp = String.Join(", ", failures.ToArray());
                        // Limit string to less than 1000
                        if (tmp.Length > 1000)
                        {
                            tmp = tmp.Substring(0, 1000) + " ...";
                        }
                        tmp = tmp + " (" + failures.Count + " total)";
                        ConsoleDump("^bUnable to fetch clan tags for: " + tmp);
                        int aborted = 0;
                        int failed = 0;
                        foreach (String pn in failures)
                        {
                            PlayerModel p = GetPlayer(pn);
                            if (p == null) continue;
                            if (p.TagFetchStatus.State == FetchState.Aborted) ++aborted;
                            if (p.TagFetchStatus.State == FetchState.Failed) ++failed;
                        }
                        ConsoleDump("^bClan tag fetches aborted: " + aborted);
                        ConsoleDump("^bClan tag fetches failed: " + failed);
                    }
                    return;
                }

                if (Regex.Match(cmd, @"^bad\s+stats?", RegexOptions.IgnoreCase).Success)
                {
                    List<String> failures = new List<String>();
                    lock (fKnownPlayers)
                    {
                        foreach (String name in fKnownPlayers.Keys)
                        {
                            PlayerModel p = fKnownPlayers[name];
                            if (p.Role != ROLE_PLAYER)
                                continue;

                            double joinedMinutesAgo = GetPlayerJoinedTimeSpan(p).TotalMinutes;
                            double enabledForMinutes = DateTime.Now.Subtract(fEnabledTimestamp).TotalMinutes;
                            if ((enabledForMinutes > MinutesAfterJoining)
                            && (joinedMinutesAgo > MinutesAfterJoining)
                            && !p.StatsVerified
                            && (p.StatsFetchStatus.State == FetchState.Failed || p.StatsFetchStatus.State == FetchState.Requesting))
                            {
                                failures.Add(name);
                            }
                        }
                    }
                    if (failures.Count == 0)
                    {
                        ConsoleDump("^bNo stats fetch failures to report");
                    }
                    else
                    {
                        String tmp = String.Join(", ", failures.ToArray());
                        // Limit string to less than 1000
                        if (tmp.Length > 1000)
                        {
                            tmp = tmp.Substring(0, 1000) + " ...";
                        }
                        tmp = tmp + " (" + failures.Count + " total)";
                        ConsoleDump("^bUnable to fetch stats for: " + tmp);
                        int aborted = 0;
                        int failed = 0;
                        foreach (String pn in failures)
                        {
                            PlayerModel p = GetPlayer(pn);
                            if (p == null) continue;
                            if (p.TagFetchStatus.State == FetchState.Aborted) ++aborted;
                            if (p.TagFetchStatus.State == FetchState.Failed) ++failed;
                        }
                        ConsoleDump("^bClan tag fetches aborted: " + aborted);
                        ConsoleDump("^bClan tag fetches failed: " + failed);
                    }
                    return;
                }



                if (Regex.Match(cmd, @"^delay", RegexOptions.IgnoreCase).Success)
                {
                    if (fTotalRoundEndingRounds < 1)
                    {
                        ConsoleDump("Not enough rounds timed to make a recommendation yet");
                        return;
                    }
                    double total = (fTotalRoundEndingSeconds / fTotalRoundEndingRounds); // total amount of time between rounds
                    double backoff = (TotalPlayerCount() / 15) * 5; // scrambler needs about 5 seconds per 15 players
                    backoff = Math.Max(5, backoff);
                    double advice = total - backoff;
                    advice = Math.Max(((fGameVersion == GameVersion.BFH) ? 10 : 50), advice); // never less than 50 seconds (10 for BFH)
                    ConsoleDump("Recommended scrambler delay, based on " + fTotalRoundEndingRounds + " rounds, is " + advice.ToString("F0") + " seconds");
                    return;
                }

                m = Regex.Match(cmd, @"^gen\s+((?:cs|cl|ctf|gm|r|sqdm|sr|s|tdm|u|dom|ob|sob|def|crl|crs|bm|hs|hot|bh)|[1234569])", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    String what = m.Groups[1].Value;
                    int section = 8;
                    if (!Int32.TryParse(what, out section)) section = 8;

                    List<CPluginVariable> vars = GetDisplayPluginVariables();

                    String sm = section.ToString() + " -";
                    if (section == 8)
                    {
                        switch (what)
                        {
                            case "cs":
                                if (fGameVersion != GameVersion.BF3)
                                    sm = "for Conquest Small";
                                else
                                    sm = "for Conq Small, Dom, Scav";
                                break;
                            case "cl": sm = "for Conquest Large"; break;
                            case "ctf": sm = "for CTF"; break;
                            case "gm": sm = "for Gun Master"; break;
                            case "r": sm = "for Rush"; break;
                            case "sqdm": sm = "for Squad Deathmatch"; break;
                            case "sr": sm = "for Squad Rush"; break;
                            case "s": sm = "for Superiority"; break;
                            case "tdm": sm = "for Team Deathmatch"; break;
                            case "u": sm = "for Unknown or New Mode"; break;
                            case "def": sm = "for Defuse"; break; //bf4
                            case "dom": sm = "for Domination"; break; // bf4
                            case "ob": sm = "for Obliteration"; break; // bf4
                            case "sob": sm = "for Squad Obliteration"; break; // bf4
                            case "crl": sm = "for NS Carrier Large"; break; // bf4
                            case "crs": sm = "for NS Carrier Small"; break; // bf4
                            case "bm": sm = "for Blood Money"; break; // bfh
                            case "hs": sm = "for Heist"; break; // bfh
                            case "hot": sm = "for Hotwire"; break; //bfh
                            case "bh": sm = "for Bounty Hunter"; break; //bfh
                            default: ConsoleDump("Unknown mode: " + what); return;
                        }
                    }

                    foreach (CPluginVariable var in vars)
                    {
                        if (section == 8)
                        {
                            if (var.Name.Contains(sm))
                            {
                                ConsoleDump(var.Name + ": " + var.Value);
                            }
                        }
                        else
                        {
                            if (var.Name.Contains(sm))
                            {
                                ConsoleDump(var.Name + ": " + var.Value);
                            }
                        }
                    }
                    return;
                }

                if (Regex.Match(cmd, @"^histogram", RegexOptions.IgnoreCase).Success)
                {
                    if (fTicketLossHistogram.Total < 1) return;
                    List<String> graph = fTicketLossHistogram.Log(60);
                    foreach (String line in graph)
                    {
                        ConsoleDump(line);
                    }
                    return;
                }

                if (Regex.Match(cmd, @"^lists", RegexOptions.IgnoreCase).Success)
                {
                    ConsoleDump("Whitelist(" + fSettingWhitelist.Count + "):");
                    foreach (String item in fSettingWhitelist)
                    {
                        ConsoleDump(item);
                    }
                    ConsoleDump(" ");
                    ConsoleDump("Friends List(" + fFriends.Keys.Count + "):");
                    foreach (int k in fFriends.Keys)
                    {
                        ConsoleDump(k.ToString() + ": " + String.Join(", ", fFriends[k].ToArray()));
                    }
                    ConsoleDump(" ");
                    ConsoleDump("Disperse Evenly List(" + fSettingDisperseEvenlyList.Count + "):");
                    foreach (String item in fSettingDisperseEvenlyList)
                    {
                        ConsoleDump(item);
                    }
                    ConsoleDump(" ");
                    for (int i = 1; i <= 4; ++i)
                    { // 1 to 4 teams
                        if (fDispersalGroups[i].Count > 0)
                        {
                            msg = "Dispersal Group " + i + " (" + fDispersalGroups[i].Count + "): " + String.Join(", ", fDispersalGroups[i].ToArray());
                            ConsoleDump(msg);
                        }
                    }
                    ConsoleDump(" ");
                    msg = "Group assignments: ";
                    for (int i = 1; i <= 4; ++i)
                    { // 1 to 4 teams
                        msg = msg + fGroupAssignments[i];
                        if (i < 4) msg = msg + "/";
                    }
                    ConsoleDump(msg);
                    return;
                }

                if (Regex.Match(cmd, @"^modes", RegexOptions.IgnoreCase).Success)
                {
                    List<String> modeList = GetSimplifiedModes();
                    ConsoleDump("modes(" + modeList.Count + "):");
                    foreach (String mode in modeList)
                    {
                        ConsoleDump(mode);
                    }
                    return;
                }

                if (Regex.Match(cmd, @"^moved", RegexOptions.IgnoreCase).Success)
                {
                    lock (fKnownPlayers)
                    {
                        ConsoleDump("^bMoved by " + GetPluginName() + ":");
                        foreach (String name in fKnownPlayers.Keys)
                        {
                            PlayerModel p = fKnownPlayers[name];
                            if (p.Role != ROLE_PLAYER)
                                continue;
                            if ((p.MovesByMBTotal + p.MovesByMBRound) < 1) continue;
                            String minsAgo = "(reset)";
                            String interval = "(never)";
                            if (p.MovedByMBTimestamp != DateTime.MinValue)
                            {
                                minsAgo = DateTime.Now.Subtract(p.MovedByMBTimestamp).TotalMinutes.ToString("F0");
                            }
                            lock (p.MovedByMBHistory)
                            {
                                if (p.MovedByMBHistory.Count > 0)
                                {
                                    if (p.MovedByMBHistory.Count == 1)
                                    {
                                        interval = "(first)";
                                    }
                                    else
                                    {
                                        int last = p.MovedByMBHistory.Count - 1;
                                        interval = p.MovedByMBHistory[last].Subtract(p.MovedByMBHistory[last - 1]).TotalMinutes.ToString("F0") + " minutes apart";
                                    }
                                }
                            }
                            ConsoleDump("^b" + p.FullName + "^n was moved " + p.MovesByMBRound + " times this round, " + (p.MovesByMBTotal + p.MovesByMBRound) + " total, the last was " + interval + " and " + minsAgo + " minutes ago");
                        }
                        ConsoleDump(" ");
                        ConsoleDump("^bMoved by someone or something else:");
                        foreach (String name in fKnownPlayers.Keys)
                        {
                            PlayerModel p = fKnownPlayers[name];
                            if (p.Role != ROLE_PLAYER)
                                continue;
                            if (p.MovesRound > 0)
                            {
                                ConsoleDump("^b" + p.FullName + "^n was moved " + p.MovesRound + " times this round, " + (p.MovesTotal + p.MovesRound) + " total");
                            }
                        }
                        ConsoleDump(" ");
                    }
                    return;
                }

                if (Regex.Match(cmd, @"^rage", RegexOptions.IgnoreCase).Success)
                {
                    ConsoleDump("Rage stats: " + fGrandRageQuits + " rage of " + fGrandTotalQuits + " total, this round " + fRageQuits + " rage of " + fTotalQuits + " total");
                    return;
                }

                if (Regex.Match(cmd, @"^refetch", RegexOptions.IgnoreCase).Success)
                {
                    List<String> fetch = new List<String>();
                    lock (fAllPlayers)
                    {
                        foreach (String name in fAllPlayers)
                        {
                            PlayerModel p = GetPlayer(name);
                            if (p == null) continue;
                            /*
                            if (!p.TagVerified) {
                                fetch.Add(name);
                                continue;
                            }
                            */
                            if ((p.TagFetchStatus.State == FetchState.InQueue || p.TagFetchStatus.State == FetchState.Requesting)
                                && (p.StatsFetchStatus.State == FetchState.InQueue || p.StatsFetchStatus.State == FetchState.Requesting)) continue;
                            fetch.Add(name);
                        }
                    }

                    if (fetch.Count == 0)
                    {
                        ConsoleDump("No active players need info, nothing to refetch!");
                        return;
                    }

                    ConsoleDump("^bRefetching Battlelog info for " + fetch.Count + " players");

                    foreach (String name in fetch)
                    {
                        PlayerModel p = GetPlayer(name);
                        p.TagFetchStatus.State = FetchState.New;
                        p.StatsFetchStatus.State = FetchState.New;
                        p.TagVerified = false;
                        AddPlayerFetch(name);
                    }
                    return;
                }

                if (Regex.Match(cmd, @"^refresh", RegexOptions.IgnoreCase).Success)
                {
                    fRefreshCommand = true;
                    ConsoleDump("Player models will be revalidated on next listPlayers event");
                    ScheduleListPlayers(1);
                    return;
                }

                if (Regex.Match(cmd, @"^reset settings", RegexOptions.IgnoreCase).Success)
                {
                    ConsoleDump("^8^bRESETTING ALL PLUGIN SETTINGS (except Whitelist and Dispersal list) TO DEFAULT!");
                    ResetSettings();
                    return;
                }

                if (Regex.Match(cmd, @"^scramble[d]?", RegexOptions.IgnoreCase).Success)
                {
                    if (fDebugScramblerBefore[0].Count == 0
                      || fDebugScramblerBefore[1].Count == 0
                      || fDebugScramblerAfter[0].Count == 0
                      || fDebugScramblerAfter[1].Count == 0)
                    {
                        ConsoleDump("No scrambler data available");
                        return;
                    }
                    ConsoleDump("===== BEFORE =====");
                    ListSideBySide(fDebugScramblerBefore[0], fDebugScramblerBefore[1], false, (KeepSquadsTogether || KeepClanTagsInSameTeam));
                    ConsoleDump("===== AFTER =====");
                    ListSideBySide(fDebugScramblerAfter[0], fDebugScramblerAfter[1], false, (KeepSquadsTogether || KeepClanTagsInSameTeam));
                    if (KeepSquadsTogether)
                    {
                        ConsoleDump(" ");
                        // After scramble, compare squads: use both after teams to account for cross-team moves
                        CompareSquads(1, 1, fDebugScramblerBefore[0], fDebugScramblerAfter[0], 2, fDebugScramblerAfter[1], false);
                        CompareSquads(2, 2, fDebugScramblerBefore[1], fDebugScramblerAfter[1], 1, fDebugScramblerAfter[0], false);
                    }
                    if (fDebugScramblerStartRound[0].Count > 0 && fDebugScramblerStartRound[1].Count > 0)
                    {
                        ConsoleDump("===== START OF ROUND =====");
                        ListSideBySide(fDebugScramblerStartRound[0], fDebugScramblerStartRound[1], false, (KeepSquadsTogether || KeepClanTagsInSameTeam));
                        if (KeepSquadsTogether)
                        {
                            ConsoleDump(" ");
                            // After team swaps, compare squads
                            CompareSquads(2, 1, fDebugScramblerAfter[1], fDebugScramblerStartRound[0], 2, fDebugScramblerStartRound[1], true);
                            CompareSquads(1, 2, fDebugScramblerAfter[0], fDebugScramblerStartRound[1], 1, fDebugScramblerStartRound[0], true);
                        }
                    }
                    ConsoleDump("===== END =====");
                    return;
                }

                if (Regex.Match(cmd, @"^size[s]?", RegexOptions.IgnoreCase).Success)
                {
                    int kp = fKnownPlayers.Count;
                    int ap = fAllPlayers.Count;
                    int old = 0;
                    int validTags = 0;
                    int commanders = 0;
                    int spectators = 0;
                    lock (fKnownPlayers)
                    {
                        // count player records more than 12 hours old
                        foreach (String name in fKnownPlayers.Keys)
                        {
                            PlayerModel p = fKnownPlayers[name];
                            if (DateTime.Now.Subtract(p.LastSeenTimestamp).TotalMinutes > 12 * 60)
                            {
                                if (!IsKnownPlayer(name))
                                {
                                    ++old;
                                }
                            }
                            if (p.TagVerified) ++validTags;
                            bool playing = false;
                            lock (fAllPlayers)
                            {
                                playing = fAllPlayers.Contains(name);
                            }
                            if (playing)
                            {
                                if (p.Role == ROLE_SPECTATOR)
                                    ++spectators;
                                else if (p.Role == ROLE_COMMANDER_MOBILE || p.Role == ROLE_COMMANDER_PC)
                                    ++commanders;
                            }
                        }
                    }
                    ConsoleDump("Plugin has been enabled for " + fRoundsEnabled + " rounds");
                    ConsoleDump("fKnownPlayers.Count = " + kp + ", not playing = " + (kp - ap) + ", more than 12 hours old = " + old + ", current commanders = " + commanders + ", current spectators = " + spectators);
                    ConsoleDump("fPriorityFetchQ.Count = " + PriorityQueueCount() + ", verified tags = " + validTags);
                    ConsoleDump("MULTIbalancerUtils.HTML_DOC.Length = " + MULTIbalancerUtils.HTML_DOC.Length);
                    return;
                }

                m = Regex.Match(cmd, @"^sort\s+([1-4])\s+(score|spm|kills|kdr|rank|kpm|bspm|bkdr|bkpm)", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    String teamID = m.Groups[1].Value;
                    String propID = m.Groups[2].Value;

                    int team = 0;
                    if (!Int32.TryParse(teamID, out team) || team < 1 || team > 4)
                    {
                        ConsoleDump("Invalid team: " + teamID);
                        return;
                    }
                    List<PlayerModel> fromList = GetTeam(team);
                    if (fromList == null || fromList.Count < 3)
                    {
                        ConsoleDump("Invalid team or not enough players in team: " + team);
                        return;
                    }
                    switch (propID.ToLower())
                    {
                        case "score":
                            fromList.Sort(DescendingRoundScore);
                            break;
                        case "spm":
                            fromList.Sort(DescendingRoundSPM);
                            break;
                        case "kills":
                            fromList.Sort(DescendingRoundKills);
                            break;
                        case "kdr":
                            fromList.Sort(DescendingRoundKDR);
                            break;
                        case "rank":
                            fromList.Sort(DescendingPlayerRank);
                            break;
                        case "kpm":
                            fromList.Sort(DescendingRoundKPM);
                            break;
                        case "bspm":
                            fromList.Sort(DescendingSPM);
                            break;
                        case "bkdr":
                            fromList.Sort(DescendingKDR);
                            break;
                        case "bkpm":
                            fromList.Sort(DescendingKPM);
                            break;
                        default:
                            fromList.Sort(DescendingRoundScore);
                            break;
                    }
                    int n = 1;
                    foreach (PlayerModel p in fromList)
                    {
                        switch (propID.ToLower())
                        {
                            case "score":
                                ConsoleDump("#" + String.Format("{0,2}", n) + ") Score: " + String.Format("{0,6:F0}", p.ScoreRound) + ", ^b" + p.FullName);
                                break;
                            case "spm":
                                ConsoleDump("#" + String.Format("{0,2}", n) + ") SPM: " + String.Format("{0,6:F0}", p.SPMRound) + ", ^b" + p.FullName);
                                break;
                            case "kills":
                                ConsoleDump("#" + String.Format("{0,2}", n) + ") Kills: " + String.Format("{0,6:F0}", p.KillsRound) + ", ^b" + p.FullName);
                                break;
                            case "kdr":
                                ConsoleDump("#" + String.Format("{0,2}", n) + ") KDR: " + String.Format("{0,6:F1}", p.KDRRound) + ", ^b" + p.FullName);
                                break;
                            case "rank":
                                ConsoleDump("#" + String.Format("{0,2}", n) + ") Rank: " + String.Format("{0,6:F0}", p.Rank) + ", ^b" + p.FullName);
                                break;
                            case "kpm":
                                ConsoleDump("#" + String.Format("{0,2}", n) + ") KPM: " + String.Format("{0,6:F1}", p.KPMRound) + ", ^b" + p.FullName);
                                break;
                            case "bspm":
                                ConsoleDump("#" + String.Format("{0,2}", n) + ") bSPM: " + String.Format("{0,6:F0}", ((p.StatsVerified) ? p.SPM : p.SPMRound)) + ", ^b" + p.FullName);
                                break;
                            case "bkdr":
                                ConsoleDump("#" + String.Format("{0,2}", n) + ") bKDR: " + String.Format("{0,6:F1}", ((p.StatsVerified) ? p.KDR : p.KDRRound)) + ", ^b" + p.FullName);
                                break;
                            case "bkpm":
                                ConsoleDump("#" + String.Format("{0,2}", n) + ") bKPM: " + String.Format("{0,6:F0}", ((p.StatsVerified) ? p.KPM : p.KPMRound)) + ", ^b" + p.FullName);
                                break;
                            default:
                                ConsoleDump("#" + String.Format("{0,2}", n) + ") Score: " + String.Format("{0,6:F0}", p.ScoreRound) + ", ^b" + p.FullName);
                                break;
                        }
                        n = n + 1;
                    }
                    return;
                }

                if (Regex.Match(cmd, @"^status", RegexOptions.IgnoreCase).Success)
                {
                    LogStatus(false, 7);
                    return;
                }

                if (Regex.Match(cmd, @"^subscribed", RegexOptions.IgnoreCase).Success)
                {
                    lock (fAllPlayers)
                    {
                        foreach (String name in fAllPlayers)
                        {
                            PlayerModel p = GetPlayer(name);
                            if (p != null && p.Subscribed)
                            {
                                ConsoleDump("^b" + p.FullName + "^n is subscribed to all balancer messages in chat");
                            }
                        }
                    }
                    return;
                }

                if (Regex.Match(cmd, @"^tags?", RegexOptions.IgnoreCase).Success)
                {
                    Dictionary<String, List<PlayerModel>> byTag = new Dictionary<String, List<PlayerModel>>();

                    lock (fAllPlayers)
                    {
                        foreach (String name in fAllPlayers)
                        {
                            PlayerModel player = GetPlayer(name);
                            if (player == null || player.Team < 1 || player.Team > 2) continue;
                            String tag = ExtractTag(player);
                            if (String.IsNullOrEmpty(tag)) continue;
                            if (!byTag.ContainsKey(tag))
                            {
                                byTag[tag] = new List<PlayerModel>();
                            }
                            byTag[tag].Add(player);
                        }
                    }

                    List<String> tags = new List<String>();
                    foreach (String t in byTag.Keys)
                    {
                        tags.Add(t);
                        byTag[t].Sort(delegate (PlayerModel lhs, PlayerModel rhs)
                        { // ascending by team/squad
                            if (lhs == null && rhs == null) return 0;
                            if (lhs == null) return -1;
                            if (rhs == null) return 1;

                            // by team, then by squad
                            if (lhs.Team < rhs.Team) return -1;
                            if (lhs.Team > rhs.Team) return 1;
                            if (lhs.Team == rhs.Team)
                            {
                                if (lhs.Squad < 1 || rhs.Squad < 1) return 0;
                                if (lhs.Squad < rhs.Squad) return -1;
                                if (lhs.Squad > rhs.Squad) return 1;
                            }
                            return 0;
                        });
                    }
                    tags.Sort();

                    foreach (String t in tags)
                    {
                        ConsoleDump("Tag [" + t + "]:");
                        List<PlayerModel> clan = byTag[t];
                        foreach (PlayerModel p in clan)
                        {
                            ConsoleDump(String.Format("        {0}, {1}, {2}",
                                p.Name,
                                GetTeamName(p.Team),
                                GetSquadName(p.Squad)
                            ));
                        }
                    }
                    ConsoleDump(" === END OF TAGS === ");
                    return;
                }

                if (Regex.Match(cmd, @"^whitelist", RegexOptions.IgnoreCase).Success)
                {
                    ConsoleDump("Whitelist:");
                    String bCodes = String.Empty;
                    String uCodes = String.Empty;
                    String sCodes = String.Empty;
                    String dCodes = String.Empty;
                    String rCodes = String.Empty;
                    String all = String.Empty;

                    List<String> plist = null;
                    lock (fAllPlayers)
                    {
                        plist = new List<String>(fAllPlayers);
                    }

                    foreach (String item in fSettingWhitelist)
                    {
                        List<String> tokens = new List<String>(Regex.Split(item, @"\s+"));
                        if (tokens.Count < 1)
                        {
                            ConsoleError("tokens.Count < 1!");
                            continue;
                        }
                        String line = String.Empty;
                        for (int i = 0; i < tokens.Count; ++i)
                        {
                            line = line + tokens[i] + " ";
                        }
                        ConsoleDump("WL: " + line);
                    }

                    foreach (String name in plist)
                    {
                        try
                        {
                            PlayerModel player = GetPlayer(name);
                            if (player == null) continue;
                            if (CheckWhitelist(player, WL_ALL))
                            {
                                if (String.IsNullOrEmpty(all))
                                {
                                    all = "    All: " + player.Name;
                                }
                                else
                                {
                                    all = all + ", " + player.Name;
                                }
                                continue;
                            }
                            if (CheckWhitelist(player, WL_BALANCE))
                            {
                                if (String.IsNullOrEmpty(bCodes))
                                {
                                    bCodes = "    Balance only: " + player.Name;
                                }
                                else
                                {
                                    bCodes = bCodes + ", " + player.Name;
                                }
                            }
                            if (CheckWhitelist(player, WL_UNSTACK))
                            {
                                if (String.IsNullOrEmpty(uCodes))
                                {
                                    uCodes = "    Unstack only: " + player.Name;
                                }
                                else
                                {
                                    uCodes = uCodes + ", " + player.Name;
                                }
                            }
                            if (CheckWhitelist(player, WL_SWITCH))
                            {
                                if (String.IsNullOrEmpty(sCodes))
                                {
                                    sCodes = "     Switch only: " + player.Name;
                                }
                                else
                                {
                                    sCodes = sCodes + ", " + player.Name;
                                }
                            }
                            if (CheckWhitelist(player, WL_DISPERSE))
                            {
                                if (String.IsNullOrEmpty(dCodes))
                                {
                                    dCodes = "   Disperse only: " + player.Name;
                                }
                                else
                                {
                                    dCodes = dCodes + ", " + player.Name;
                                }
                            }
                            if (CheckWhitelist(player, WL_RANK))
                            {
                                if (String.IsNullOrEmpty(rCodes))
                                {
                                    rCodes = "       Rank only: " + player.Name;
                                }
                                else
                                {
                                    rCodes = rCodes + ", " + player.Name;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            ConsoleException(e);
                        }
                    }

                    if (!String.IsNullOrEmpty(all)) ConsoleDump(all);
                    if (!String.IsNullOrEmpty(bCodes)) ConsoleDump(bCodes);
                    if (!String.IsNullOrEmpty(uCodes)) ConsoleDump(uCodes);
                    if (!String.IsNullOrEmpty(sCodes)) ConsoleDump(sCodes);
                    if (!String.IsNullOrEmpty(dCodes)) ConsoleDump(dCodes);
                    if (!String.IsNullOrEmpty(rCodes)) ConsoleDump(rCodes);
                    return;
                }

                // test BF3 fetch
                Match testF3 = Regex.Match(cmd, @"^test f3 ([^\s]+)", RegexOptions.IgnoreCase);
                if (testF3.Success)
                {
                    int oldLevel = DebugLevel;
                    DebugLevel = 7;
                    try
                    {
                        ConsoleDump("Testing BF3 Clantag fetch:");
                        String tn = testF3.Groups[1].Value;
                        PlayerModel dummy = GetPlayer(tn);
                        if (dummy == null)
                        {
                            ConsoleDump("Player ^b" + tn + "^n seems to have left the server");
                            dummy = new PlayerModel(tn, 1);
                        }
                        else
                        {
                            ConsoleDump("Player ^b" + tn + "^n, TagVerified: " + dummy.TagVerified + ", TagFetchStatus: " + dummy.TagFetchStatus.State + ", PersonaId: " + dummy.PersonaId);
                        }
                        SendBattlelogRequest(dummy.Name, "clanTag", dummy);
                        ConsoleDump("Status = " + dummy.TagFetchStatus.State);
                        dummy.TagVerified = (dummy.TagFetchStatus.State != FetchState.Failed);
                    }
                    catch (Exception e)
                    {
                        ConsoleException(e);
                    }
                    DebugLevel = oldLevel;
                    return;
                }

                // test BF4 fetch
                Match testF4 = Regex.Match(cmd, @"^test f4 ([^\s]+)", RegexOptions.IgnoreCase);
                if (testF4.Success)
                {
                    int oldLevel = DebugLevel;
                    DebugLevel = 7;
                    try
                    {
                        ConsoleDump("Testing BF4 Clantag fetch:");
                        String tn = testF4.Groups[1].Value;
                        PlayerModel dummy = GetPlayer(tn);
                        if (dummy == null)
                        {
                            ConsoleDump("Player ^b" + tn + "^n seems to have left the server");
                            dummy = new PlayerModel(tn, 1);
                        }
                        else
                        {
                            ConsoleDump("Player ^b" + tn + "^n, TagVerified: " + dummy.TagVerified + ", TagFetchStatus: " + dummy.TagFetchStatus.State + ", PersonaId: " + dummy.PersonaId);
                        }
                        SendBattlelogRequestBF4(dummy.Name, "clanTag", dummy);
                        ConsoleDump("Status = " + dummy.TagFetchStatus.State);
                        dummy.TagVerified = (dummy.TagFetchStatus.State != FetchState.Failed);
                    }
                    catch (Exception e)
                    {
                        ConsoleException(e);
                    }
                    DebugLevel = oldLevel;
                    return;
                }

                // test BFH fetch
                Match testFH = Regex.Match(cmd, @"^test fh ([^\s]+)", RegexOptions.IgnoreCase);
                if (testFH.Success)
                {
                    int oldLevel = DebugLevel;
                    DebugLevel = 7;
                    try
                    {
                        ConsoleDump("Testing BFH Clantag fetch:");
                        String tn = testFH.Groups[1].Value;
                        PlayerModel dummy = GetPlayer(tn);
                        if (dummy == null)
                        {
                            ConsoleDump("Player ^b" + tn + "^n seems to have left the server");
                            dummy = new PlayerModel(tn, 1);
                        }
                        else
                        {
                            ConsoleDump("Player ^b" + tn + "^n, TagVerified: " + dummy.TagVerified + ", TagFetchStatus: " + dummy.TagFetchStatus.State + ", PersonaId: " + dummy.PersonaId);
                        }
                        SendBattlelogRequestBFH(dummy.Name, "clanTag", dummy);
                        ConsoleDump("Status = " + dummy.TagFetchStatus.State);
                        dummy.TagVerified = (dummy.TagFetchStatus.State != FetchState.Failed);
                    }
                    catch (Exception e)
                    {
                        ConsoleException(e);
                    }
                    DebugLevel = oldLevel;
                    return;
                }

                // Undocumented command: risky (hide|show)
                Match risky = Regex.Match(cmd, @"^risky (hide|show)", RegexOptions.IgnoreCase);
                if (risky.Success)
                {
                    if (risky.Groups[1].Value == "show")
                    {
                        fShowRiskySettings = true;
                    }
                    else
                    {
                        fShowRiskySettings = false;
                    }
                    if (fShowRiskySettings)
                    {
                        ConsoleDump("Showing risky settings!");
                    }
                    else
                    {
                        ConsoleDump("Hiding risky settings!");
                    }
                    return;
                }

                // Undocumented command: test scrambler
                if (Regex.Match(cmd, @"^test scrambler", RegexOptions.IgnoreCase).Success)
                {
                    ConsoleDump("Testing scrambler:");
                    ScrambleByCommand(1, true); // log only, winner is always team 1
                    return;
                }

                // Undocumented command: test @mb ...
                if (Regex.Match(cmd, @"^test @mb", RegexOptions.IgnoreCase).Success)
                {
                    ConsoleDump("Testing chat command:");
                    String tmp = cmd.Replace("test ", String.Empty);
                    try
                    {
                        fTestMBCommand = true;
                        OnGlobalChat("[Plugin]", tmp);
                    }
                    catch (Exception)
                    {
                        // Do nothing
                    }
                    finally
                    {
                        fTestMBCommand = false;
                    }
                    return;
                }

                // Undocumented command: test fast balance
                if (Regex.Match(cmd, @"^test fast", RegexOptions.IgnoreCase).Success)
                {
                    if (!EnableAdminKillForFastBalance)
                    {
                        ConsoleDump("Enable Admin Kill For Fast Balance must be True to test, skipping");
                        return;
                    }
                    ConsoleDump("Testing fast balance:");
                    if (fTestFastBalance)
                    {
                        fTestFastBalance = false;
                        ConsoleDump("Deactivated fast balance test");
                    }
                    else
                    {
                        fTestFastBalance = true;
                        FastBalance("Test: ");
                    }
                    return;
                }

                // Undocumented command: test clan dispersal
                if (Regex.Match(cmd, @"^test clan", RegexOptions.IgnoreCase).Success)
                {
                    PerModeSettings perMode = GetPerModeSettings();
                    if (perMode.DisperseEvenlyByClanPlayers == 0)
                    {
                        ConsoleDump("per-mode Disperse Evenly By Clan Players must be more than 0 to test, skipping");
                        return;
                    }
                    ConsoleDump("Testing clan dispersal:");
                    if (fTestClanDispersal)
                    {
                        fTestClanDispersal = false;
                        ConsoleDump("Deactivated clan dispersal testing");
                    }
                    else
                    {
                        fTestClanDispersal = true;
                        ConsoleDump("Activated clan dispersal testing");
                    }
                    return;
                }

                // Undocumented command: generate VBCode from HTML
                if (Regex.Match(cmd, @"^vbcode", RegexOptions.IgnoreCase).Success)
                {
                    String vbCode = MULTIbalancerUtils.ConvertHTMLToVBCode(MULTIbalancerUtils.HTML_DOC);
                    ConsoleDump("Converted " + MULTIbalancerUtils.HTML_DOC.Length + " chars of HTML to " + vbCode.Length + " chars of VBCode!");
                    try
                    {
                        String path = Path.Combine(Directory.GetParent(Application.ExecutablePath).FullName, "vbcode.txt");

                        using (FileStream fs = File.Open(path, FileMode.Create))
                        {
                            Byte[] buffer = new UTF8Encoding(true).GetBytes(vbCode);
                            fs.Write(buffer, 0, buffer.Length);
                            ConsoleDump("Successfully wrote " + path);
                        }
                    }
                    catch (Exception e)
                    {
                        ConsoleException(e);
                    }
                    return;
                }

                if (Regex.Match(cmd, @"^\s*help", RegexOptions.IgnoreCase).Success || !String.IsNullOrEmpty(cmd))
                {
                    ConsoleDump("^1^bbad tags^n^0: Examine list of players whose clan tag fetch failed");
                    ConsoleDump("^1^bbad stats^n^0: Examine list of players whose stats fetch failed");
                    ConsoleDump("^1^bdelay^n^0: Examine recommended scrambler delay time");
                    ConsoleDump("^1^bgen^n ^imode^n^0: Generate settings listing for ^imode^n (one of: cs, cl, ctf, gm, r, sqdm, sr, s, tdm, dom, ob, def, crl, crs, bm, hs, hot, bh, u)");
                    ConsoleDump("^1^bgen^n ^isection^n^0: Generate settings listing for ^isection^n (1-6,9)");
                    ConsoleDump("^1^bhistogram^n^0: Examine a histogram graph of ticket loss ratios");
                    ConsoleDump("^1^blists^n^0: Examine all settings that are lists");
                    ConsoleDump("^1^bmodes^n^0: Examine the known game modes");
                    ConsoleDump("^1^bmoved^n^0: Examine which players were moved, how many times total and how long ago");
                    ConsoleDump("^1^brage^n^0: Examine rage quit statistics");
                    ConsoleDump("^1^brefetch^n^0: Refetch Battlelog info for all active players");
                    ConsoleDump("^1^brefresh^n^0: Force refresh of player list");
                    ConsoleDump("^1^breset settings^n^0: Reset all plugin settings to default, except for ^bWhitelist^n and ^bDisperse Evenly List^n");
                    ConsoleDump("^1^bscrambled^n^0: Examine list of players before and after last successful scramble");
                    ConsoleDump("^1^bsizes^n^0: Examine the sizes of various data structures");
                    ConsoleDump("^1^bsort^n ^iteam^n ^itype^n^0: Examine sorted ^iteam^n (1-4) by ^itype^n (one of: score, spm, kills, kdr, rank, kpm, bspm, bkdr, bkpm)");
                    ConsoleDump("^1^bstatus^n^0: Examine full status log, as if Debug Level were 7");
                    ConsoleDump("^1^bsubscribed^n^0: Examine all players who are subscribed to balancer chat messages");
                    ConsoleDump("^1^btags^n^0: Examine list of players sorted by clan tags");
                    ConsoleDump("^1^btest f3^n ^iname^n^0: Test BF3 tag fetch");
                    ConsoleDump("^1^btest f4^n ^iname^n^0: Test BF4 tag fetch");
                    ConsoleDump("^1^bwhitelist^n^0: Examine whitelist combined with reserved slots, by option codes");
                    return;
                }



            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        private void CompareSquads(int beforeTeam, int afterTeam, List<PlayerModel> before, List<PlayerModel> after, int otherTeam, List<PlayerModel> otherAfter, bool finalCheck)
        {
            Dictionary<Int32, List<String>> beforeTable = new Dictionary<Int32, List<String>>();
            Dictionary<Int32, List<String>> afterTable = new Dictionary<Int32, List<String>>();
            Dictionary<Int32, List<String>> otherTable = new Dictionary<Int32, List<String>>();
            // Load the expected squad assignments into a table indexed by squad
            foreach (PlayerModel b in before)
            {
                List<String> s = null;
                if (beforeTable.TryGetValue(b.Squad, out s) && s != null)
                {
                    s.Add(b.Name);
                }
                else
                {
                    s = new List<String>();
                    s.Add(b.Name);
                    beforeTable[b.Squad] = s;
                }
            }
            // Load actual squad assignments into a table indexed by squad
            foreach (PlayerModel a in after)
            {
                List<String> s = null;
                if (afterTable.TryGetValue(a.Squad, out s) && s != null)
                {
                    s.Add(a.Name);
                }
                else
                {
                    s = new List<String>();
                    s.Add(a.Name);
                    afterTable[a.Squad] = s;
                }
            }
            // Check for cross-team moves
            foreach (PlayerModel o in otherAfter)
            {
                List<String> s = null;
                if (otherTable.TryGetValue(o.Squad, out s) && s != null)
                {
                    s.Add(o.Name);
                }
                else
                {
                    s = new List<String>();
                    s.Add(o.Name);
                    otherTable[o.Squad] = s;
                }
            }

            // Compare
            foreach (int expectedSquad in beforeTable.Keys)
            {
                try
                {
                    AnalyzeSquadLists(beforeTeam, expectedSquad, beforeTable[expectedSquad], afterTeam, afterTable, otherTeam, otherTable, finalCheck);
                }
                catch (Exception e)
                {
                    ConsoleException(e);
                }
            }
        }

        private void AnalyzeSquadLists(int beforeTeam, int beforeSquad, List<String> beforeSquadList, int afterTeam, Dictionary<Int32, List<String>> afterTable, int otherTeam, Dictionary<Int32, List<String>> otherTable, bool finalCheck)
        {
            // Analyze the disposition of one squad (beforeSquad)
            if (beforeTeam < 1 || beforeTeam > 2 || beforeSquad < 0 || beforeSquad >= SQUAD_NAMES.Length) return;
            Dictionary<String, Int32> endedUpIn = new Dictionary<String, Int32>();
            String teamName = GetTeamName(beforeTeam);
            String squadName = GetSquadName(beforeSquad);
            String ts = teamName + "/" + squadName;

            // Find which squad each expected player ended up in
            foreach (String x in beforeSquadList)
            {
                // anyone leave?
                if (finalCheck && !IsKnownPlayer(x))
                {
                    ConsoleDump("Player must have left, since " + ts + " is missing ^b" + x);
                    continue;
                }
                // where did player x end up?
                foreach (int afterSquad in afterTable.Keys)
                {
                    if (afterTable[afterSquad].Contains(x))
                    {
                        endedUpIn[x] = (1000 * afterTeam) + afterSquad; // remember combined team+squad this name ended up
                    }
                }
                foreach (int otherSquad in otherTable.Keys)
                {
                    if (otherTable[otherSquad].Contains(x))
                    {
                        endedUpIn[x] = (1000 * otherTeam) + otherSquad; // remember combined team+squad this name ended up
                    }
                }
            }

            // build a table of where every player actually ended up (invert endedUpIn table)
            String split = " ";
            int different = -1;
            Dictionary<Int32, List<String>> movedSquadTable = new Dictionary<Int32, List<String>>(); // key is combined team + squad

            foreach (String name in endedUpIn.Keys)
            {
                int eui = endedUpIn[name];
                int endedUpInTeam = eui / 1000;
                int endedUpInSquad = eui - (1000 * endedUpInTeam);
                if (endedUpInSquad != beforeSquad) different = eui; // only remember the latest
                List<String> endedUpInSquadList = null;
                if (movedSquadTable.TryGetValue(eui, out endedUpInSquadList) && endedUpInSquadList != null)
                {
                    endedUpInSquadList.Add(name);
                }
                else
                {
                    endedUpInSquadList = new List<String>();
                    endedUpInSquadList.Add(name);
                    movedSquadTable[eui] = endedUpInSquadList;
                }
            }

            // A split squad will have more than one entry in the squad id -> player list table
            if (movedSquadTable.Keys.Count > 1)
            {
                // Decide which players are the outliers, in the smallest lists
                int max = -1;
                int big = -1;
                foreach (int si in movedSquadTable.Keys)
                {
                    if (movedSquadTable[si].Count > max)
                    {
                        big = si;
                        max = movedSquadTable[si].Count;
                    }
                }
                // every list except max
                String notice = "Player(s) removed from " + ts + " to balance teams:";
                foreach (int si in movedSquadTable.Keys)
                {
                    if (si == big) continue;
                    int siTeam = si / 1000;
                    int siSquad = si - (1000 * siTeam);
                    if (!finalCheck)
                    {
                        foreach (String outlier in movedSquadTable[si])
                        {
                            split = split + "^b" + outlier + "^n to " + GetSquadName(siSquad) + ", ";
                        }
                        split = split + "end.";
                        ConsoleDump(notice + split);
                        split = " ";
                    }
                    else
                    {
                        foreach (String finalOutlier in movedSquadTable[si])
                        {
                            String fm = null;
                            try
                            {
                                lock (fExtrasLock)
                                {
                                    fDebugScramblerSuspects.TryGetValue(finalOutlier, out fm);
                                }
                                if (fm == null)
                                {
                                    fm = "^4UNEXPECTED: split of " + ts + " due to player ^b{0}^n being found in " + GetSquadName(siSquad);
                                }
                                PlayerModel outp = GetPlayer(finalOutlier);
                                String fullName = (outp == null) ? finalOutlier : outp.FullName;
                                ConsoleDump(String.Format(fm, fullName));
                            }
                            catch (Exception e)
                            {
                                ConsoleException(e);
                            }
                        }
                    }
                }
            }
            else if (different != -1)
            {
                int differentTeam = different / 1000;
                if (differentTeam < 1 || differentTeam > 2) differentTeam = 0;
                int differentSquad = different - (1000 * differentTeam);
                if (differentSquad < 0 || differentSquad >= SQUAD_NAMES.Length) differentSquad = 0;
                ConsoleDump(ts + " is intact and is now a different squad: " + GetTeamName(differentTeam) + "/" + GetSquadName(differentSquad));
            }
            // Dump nothing if everything is as expected
        }










    } // end MULTIbalancer

} // end namespace PRoConEvents
