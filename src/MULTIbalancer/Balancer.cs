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
        /* ======================== CORE ENGINE ============================= */









        private void BalanceAndUnstack(String name)
        {

            /* Useful variables */

            PlayerModel player = null;
            String simpleMode = String.Empty;
            PerModeSettings perMode = null;
            bool isStrong = false; // this player
            int winningTeam = 0;
            int losingTeam = 0;
            int biggestTeam = 0;
            int smallestTeam = 0;
            int[] ascendingSize = null;
            int[] descendingTickets = null;
            String strongMsg = String.Empty;
            int diff = 0;
            DateTime now = DateTime.Now;
            bool needsBalancing = false;
            bool loggedStats = false;
            bool isSQDM = IsSQDM();
            bool isNonBalancingMode = IsNonBalancingMode();
            String log = String.Empty;

            /* Sanity checks */

            if (fServerInfo == null)
            {
                return;
            }

            int totalPlayerCount = TotalPlayerCount();

            if (DebugLevel >= 8) DebugBalance("BalanceAndUnstack(^b" + name + "^n), " + totalPlayerCount + " players");

            if (totalPlayerCount >= (MaximumServerSize - 1))
            {
                if (DebugLevel >= 6) DebugBalance("Server is full, no balancing or unstacking will be attempted!");
                IncrementTotal(); // no matching stat, reflect total deaths handled
                CheckDeativateBalancer("Full");
                return;
            }

            if (totalPlayerCount < 4)
            {
                if (DebugLevel >= 6) DebugBalance("Server is in warmup, less than 4 players");
                CheckDeativateBalancer("Warmup");
                return;
            }

            if (totalPlayerCount > 0)
            {
                AnalyzeTeams(out diff, out ascendingSize, out descendingTickets, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);
            }
            else
            {
                CheckDeativateBalancer("Empty");
                return;
            }

            if (EnableAdminKillForFastBalance && !isNonBalancingMode && diff > MaxFastDiff())
            {
                DebugBalance("Fast balance is enabled and active, skipping normal balancing and unstacking");
                CheckDeativateBalancer("Fast balance is active");
                return;
            }


            /* Pre-conditions */

            player = GetPlayer(name);
            if (player == null)
            {
                CheckDeativateBalancer("Unknown player " + name);
                return;
            }

            if (!fModeToSimple.TryGetValue(fServerInfo.GameMode, out simpleMode))
            {
                DebugBalance("Unknown game mode: " + fServerInfo.GameMode);
                simpleMode = fServerInfo.GameMode;
            }
            if (String.IsNullOrEmpty(simpleMode))
            {
                DebugBalance("Simple mode is null: " + fServerInfo.GameMode);
                CheckDeativateBalancer("Unknown mode");
                return;
            }
            if (!fPerMode.TryGetValue(simpleMode, out perMode))
            {
                DebugBalance("No per-mode settings for " + simpleMode + ", using defaults");
                perMode = new PerModeSettings();
            }
            if (perMode == null)
            {
                DebugBalance("Per-mode settings null for " + simpleMode + ", using defaults");
                perMode = new PerModeSettings();
            }

            if (fGameVersion == GameVersion.BFH && isNonBalancingMode)
            {
                DebugWrite("^5(AUTO)^9 Server is in ^b" + simpleMode + "^n mode, which should not be balanced! Deactivating balancer!", 4);
                CheckDeativateBalancer("BFH Competitive Mode");
                return;
            }

            /* Per-mode and player info */

            String extractedTag = ExtractTag(player);
            Speed balanceSpeed = GetBalanceSpeed(perMode);
            double unstackTicketRatio = GetUnstackTicketRatio(perMode);
            int lastMoveFrom = player.LastMoveFrom;

            if (totalPlayerCount >= (perMode.MaxPlayers - 1))
            {
                if (DebugLevel >= 6) DebugBalance("Server is full by per-mode Max Players, no balancing or unstacking will be attempted!");
                IncrementTotal(); // no matching stat, reflect total deaths handled
                CheckDeativateBalancer("Full per-mode");
                return;
            }

            int floorPlayers = (perMode.EnableLowPopulationAdjustments) ? 4 : 6;
            if (totalPlayerCount < floorPlayers)
            {
                if (DebugLevel >= 6) DebugBalance("Not enough players in server, minimum is " + floorPlayers);
                IncrementTotal(); // no matching stat, reflect total deaths handled
                CheckDeativateBalancer("Not enough players");
                return;
            }

            /* Check dispersals */

            bool mustMove = false;
            bool lenient = false;
            int maxDispersalMoves = 2;
            bool isDisperseByRank = IsRankDispersal(player);
            bool isDisperseByList = IsInDispersalList(player, false);
            /* DCE */
            bool isDisperseByClanPop = false;
            if (!isDisperseByList)
            {
                isDisperseByClanPop = IsClanDispersal(player, false);
            }

            if (isDisperseByList)
            {
                lenient = !perMode.EnableStrictDispersal; // the opposite of strict is lenient
                String dispersalMode = (lenient) ? "LENIENT MODE" : "STRICT MODE";
                ConsoleDebug("ON MUST MOVE LIST ^b" + player.FullName + "^n T:" + player.Team + ", disperse evenly enabled, " + dispersalMode);
                mustMove = true;
                maxDispersalMoves = (lenient) ? 1 : 2;
            }
            else if (isDisperseByClanPop)
            {
                lenient = !perMode.EnableStrictDispersal; // the opposite of strict is lenient
                String dispersalMode = (lenient) ? "LENIENT MODE" : "STRICT MODE";
                ConsoleDebug("ON MUST MOVE LIST ^b" + player.FullName + "^n T:" + player.Team + ", disperse clan tags evenly enabled, " + dispersalMode);
                mustMove = true;
                maxDispersalMoves = (lenient) ? 1 : 2;
            }
            else if (isDisperseByRank)
            {
                lenient = LenientRankDispersal || !perMode.EnableStrictDispersal;
                String dispersalMode = (lenient) ? "LENIENT MODE" : "STRICT MODE";
                ConsoleDebug("ON MUST MOVE LIST ^b" + name + "^n T:" + player.Team + ", Rank " + player.Rank + " >= " + perMode.DisperseEvenlyByRank + ", " + dispersalMode);
                mustMove = true;
                maxDispersalMoves = (lenient) ? 1 : 2;
            }

            /* Check if balancing is needed */

            if (diff > MaxDiff())
            {
                needsBalancing = true; // needs balancing set to true, unless speed is Unstack only
                if (balanceSpeed == Speed.Unstack)
                {
                    DebugBalance("Needs balancing, but balance speed is set to Unstack, so no balancing will be done");
                    needsBalancing = false;
                }
            }

            /* Per-mode settings */

            // Adjust for duration of balance active
            if (needsBalancing && fBalanceIsActive && balanceSpeed == Speed.Adaptive && fLastBalancedTimestamp != DateTime.MinValue)
            {
                double secs = now.Subtract(fLastBalancedTimestamp).TotalSeconds;
                if (secs > SecondsUntilAdaptiveSpeedBecomesFast)
                {
                    DebugBalance("^8^bBalancing taking too long (" + secs.ToString("F0") + " secs)!^n^0 Forcing to Fast balance speed.");
                    balanceSpeed = Speed.Fast;
                }
            }

            // Adjust speed to Fast if teams differ by 4 or more
            if (needsBalancing && balanceSpeed != Speed.Fast && balanceSpeed != Speed.Stop && !isSQDM && diff >= 4)
            {
                DebugBalance("^8^bTeam count difference is 4 or more (" + diff + ")!^n^0 Forcing to Fast balance speed.");
                balanceSpeed = Speed.Fast;
            }


            String orSlow = (balanceSpeed == Speed.Slow) ? " or speed is Slow" : String.Empty;

            // Do not disperse mustMove players if speed is Stop or Slow or Phase is Late or Popluation is Low and Enable Low Population Adjustments is True
            if (mustMove && balanceSpeed == Speed.Stop)
            {
                DebugBalance("Removing MUST MOVE status from dispersal player ^b" + player.FullName + "^n T:" + player.Team + ", due to Balance Speed = Stop");
                mustMove = false;
            }
            else if (mustMove && balanceSpeed == Speed.Slow)
            {
                DebugBalance("Removing MUST MOVE status from dispersal player ^b" + player.FullName + "^n T:" + player.Team + ", due to Balance Speed = Slow");
                mustMove = false;
            }
            else if (mustMove && GetPhase(perMode, false) == Phase.Late)
            {
                DebugBalance("Removing MUST MOVE status from dispersal player ^b" + player.FullName + "^n T:" + player.Team + ", due to Phase = Late");
                mustMove = false;
            }
            else if (mustMove && perMode.EnableLowPopulationAdjustments && GetPopulation(perMode, false) == Population.Low)
            {
                DebugBalance("Removing MUST MOVE status from dispersal player ^b" + player.FullName + "^n T:" + player.Team + ", due to Population = Low");
                mustMove = false;
            }

            /* Activation check */

            if (balanceSpeed != Speed.Stop && needsBalancing)
            {
                if (!fBalanceIsActive)
                {
                    DebugBalance("^2^bActivating autobalance!");
                    fLastBalancedTimestamp = now;
                }
                fBalanceIsActive = true;
            }
            else
            {
                CheckDeativateBalancer("Deactiving autobalance");
            }

            // Wait for unassigned
            if (!mustMove && needsBalancing && balanceSpeed != Speed.Fast && (diff > MaxDiff()) && fUnassigned.Count >= (diff - MaxDiff()))
            {
                DebugBalance("Wait for " + fUnassigned.Count + " unassigned players to be assigned before moving active players");
                IncrementTotal(); // no matching stat, reflect total deaths handled
                return;
            }

            /* Early exemptions - avoid doing exclusion computation if unnecessary */

            // Exempt if this player already been moved for balance or unstacking
            if ((!mustMove && GetMovesThisRound(player) >= 1) || (mustMove && GetMovesThisRound(player) >= maxDispersalMoves))
            {
                DebugBalance("Exempting ^b" + name + "^n, already moved this round");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }

            // Exempt if role isn't ordinary player - mustMove always false for this case
            if (player.Role != ROLE_PLAYER)
            {
                String rn = "UNKNOWN";
                if (player.Role >= 0 && player.Role < ROLE_NAMES.Length) rn = ROLE_NAMES[player.Role];
                DebugBalance("Exempting ^b" + name + "^n, role is " + rn + " for team " + GetTeamName(player.Team));
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }

            /* Exclusions */

            // Exclude if on Whitelist or Reserved Slots if enabled
            if (OnWhitelist || (needsBalancing && balanceSpeed == Speed.Slow))
            {
                if (CheckWhitelist(player, WL_BALANCE))
                {
                    DebugBalance("Excluding ^b" + player.FullName + "^n: whitelisted" + orSlow);
                    fExcludedRound = fExcludedRound + 1;
                    IncrementTotal();
                    return;
                }
            }

            // Sort player's team by the strong method
            List<PlayerModel> fromList = GetTeam(player.Team);
            if (fromList == null)
            {
                DebugBalance("Unknown team " + player.Team + " for player ^b" + player.Name);
                return;
            }
            switch (perMode.DetermineStrongPlayersBy)
            {
                case DefineStrong.RoundScore:
                    fromList.Sort(DescendingRoundScore);
                    strongMsg = "Determing strong by: Round Score";
                    break;
                case DefineStrong.RoundSPM:
                    fromList.Sort(DescendingRoundSPM);
                    strongMsg = "Determing strong by: Round SPM";
                    break;
                case DefineStrong.RoundKills:
                    fromList.Sort(DescendingRoundKills);
                    strongMsg = "Determing strong by: Round Kills";
                    break;
                case DefineStrong.RoundKDR:
                    fromList.Sort(DescendingRoundKDR);
                    strongMsg = "Determing strong by: Round KDR";
                    break;
                case DefineStrong.PlayerRank:
                    fromList.Sort(DescendingPlayerRank);
                    strongMsg = "Determing strong by: Player Rank";
                    break;
                case DefineStrong.RoundKPM:
                    fromList.Sort(DescendingRoundKPM);
                    strongMsg = "Determing strong by: Round KPM";
                    break;
                case DefineStrong.BattlelogSPM:
                    fromList.Sort(DescendingSPM);
                    strongMsg = "Determing strong by: Battlelog SPM";
                    break;
                case DefineStrong.BattlelogKDR:
                    fromList.Sort(DescendingKDR);
                    strongMsg = "Determing strong by: Battlelog KDR";
                    break;
                case DefineStrong.BattlelogKPM:
                    fromList.Sort(DescendingKPM);
                    strongMsg = "Determing strong by: Battlelog KPM";
                    break;
                default:
                    fromList.Sort(DescendingRoundScore);
                    strongMsg = "Determing strong by: Round Score";
                    break;
            }

            double above = ((fromList.Count * perMode.PercentOfTopOfTeamIsStrong) / 100.0) + 0.5;
            int strongest = Math.Max(0, Convert.ToInt32(above));
            int playerIndex = 0;
            int minPlayers = (isSQDM) ? 5 : fromList.Count; // for SQDM, apply top/strong/weak only if team has 5 or more players

            // Exclude if TopScorers enabled and a top scorer on the team
            int topPlayersPerTeam = 0;
            if (balanceSpeed != Speed.Fast && (TopScorers || balanceSpeed == Speed.Slow))
            {
                if (isSQDM)
                {
                    int maxCount = fromList.Count;
                    if (maxCount < 5)
                    {
                        topPlayersPerTeam = 0;
                    }
                    else if (maxCount <= 8)
                    {
                        topPlayersPerTeam = 1;
                    }
                    else if (totalPlayerCount <= 16)
                    {
                        topPlayersPerTeam = 2;
                    }
                    else
                    {
                        topPlayersPerTeam = 3;
                    }
                }
                else
                {
                    if (totalPlayerCount <= 22)
                    {
                        topPlayersPerTeam = 1;
                    }
                    else if (totalPlayerCount >= 42)
                    {
                        topPlayersPerTeam = 3;
                    }
                    else
                    {
                        topPlayersPerTeam = 2;
                    }
                }
            }
            // Loop is unconditional even when topPlayersPerTeam is zero, due to assigning playerIndex
            for (int i = 0; i < fromList.Count; ++i)
            {
                if (fromList[i].Name == player.Name)
                {
                    if (!mustMove
                    && needsBalancing
                    && balanceSpeed != Speed.Fast
                    && fromList.Count >= minPlayers
                    && topPlayersPerTeam != 0
                    && i < topPlayersPerTeam)
                    {
                        String why = (balanceSpeed == Speed.Slow) ? "Speed is slow, excluding top scorers" : "Top Scorers enabled";
                        if (!loggedStats)
                        {
                            DebugBalance(GetPlayerStatsString(name));
                            loggedStats = true;
                        }
                        DebugBalance("Excluding ^b" + player.FullName + "^n: " + why + " and this player is #" + (i + 1) + " on team " + GetTeamName(player.Team));
                        fExcludedRound = fExcludedRound + 1;
                        IncrementTotal();
                        return;
                    }
                    else
                    {
                        playerIndex = i;
                        break;
                    }
                }
            }
            isStrong = (playerIndex < strongest);

            // Exclude if too soon since last move
            if ((!mustMove || lenient) && player.MovedByMBTimestamp != DateTime.MinValue)
            {
                double mins = now.Subtract(player.MovedByMBTimestamp).TotalMinutes;
                if (mins < MinutesAfterBeingMoved)
                {
                    DebugBalance("Excluding ^b" + player.Name + "^n: last move was " + mins.ToString("F0") + " minutes ago, less than required " + MinutesAfterBeingMoved.ToString("F0") + " minutes");
                    fExcludedRound = fExcludedRound + 1;
                    IncrementTotal();
                    return;
                }
                else
                {
                    // reset
                    player.MovedByMBTimestamp = DateTime.MinValue;
                }
            }

            // Exclude if player joined less than MinutesAfterJoining
            double joinedMinutesAgo = GetPlayerJoinedTimeSpan(player).TotalMinutes;
            double enabledForMinutes = now.Subtract(fEnabledTimestamp).TotalMinutes;
            if ((!mustMove || lenient)
            && needsBalancing
            && (enabledForMinutes > MinutesAfterJoining)
            && balanceSpeed != Speed.Fast
            && (joinedMinutesAgo < MinutesAfterJoining))
            {
                if (!loggedStats)
                {
                    DebugBalance(GetPlayerStatsString(name));
                    loggedStats = true;
                }
                DebugBalance("Excluding ^b" + player.FullName + "^n: joined less than " + MinutesAfterJoining.ToString("F1") + " minutes ago (" + joinedMinutesAgo.ToString("F1") + ")");
                fExcludedRound = fExcludedRound + 1;
                IncrementTotal();
                return;
            }

            // Special exemption if tag not verified and fetches pending in the queue and joined less than 15 minutes ago
            if (!player.TagVerified && PriorityQueueCount() > 0 && joinedMinutesAgo < 15)
            {
                if (DebugLevel >= 7) DebugBalance("Skipping ^b" + player.Name + "^n, clan tag not verified yet");
                // Don't count this as an exemption
                // Don't increment the total
                return;
            }

            // Exclude if in squad with same tags
            if ((!mustMove || lenient) && SameClanTagsInSquad && !isDisperseByClanPop)
            {
                int cmt = CountMatchingTags(player, Scope.SameSquad);
                if (cmt >= 2)
                {
                    String et = ExtractTag(player);
                    DebugBalance("Excluding ^b" + name + "^n, " + cmt + " players in squad with tag [" + et + "]");
                    fExcludedRound = fExcludedRound + 1;
                    IncrementTotal();
                    return;
                }
            }

            // Exclude if in team with same tags
            if ((!mustMove || lenient) && SameClanTagsInTeam && !isDisperseByClanPop)
            {
                int cmt = CountMatchingTags(player, Scope.SameTeam);
                if (cmt >= 5 && !isDisperseByClanPop)
                {
                    String et = ExtractTag(player);
                    DebugBalance("Excluding ^b" + name + "^n, " + cmt + " players in team with tag [" + et + "]");
                    fExcludedRound = fExcludedRound + 1;
                    IncrementTotal();
                    return;
                }
            }

            // Exclude if on friends list
            if ((!mustMove || lenient) && OnFriendsList)
            {
                int cmf = CountMatchingFriends(player, Scope.SameSquad);
                if (cmf >= 2)
                {
                    DebugBalance("Excluding ^b" + player.FullName + "^n, " + cmf + " players in squad are friends (friendex = " + player.Friendex + ")");
                    fExcludedRound = fExcludedRound + 1;
                    IncrementTotal();
                    return;
                }
                if (ApplyFriendsListToTeam)
                {
                    cmf = CountMatchingFriends(player, Scope.SameTeam);
                    if (cmf >= 5)
                    {
                        DebugBalance("Excluding ^b" + player.FullName + "^n, " + cmf + " players in team are friends (friendex = " + player.Friendex + ")");
                        fExcludedRound = fExcludedRound + 1;
                        IncrementTotal();
                        return;
                    }
                }
            }

            /* - moved earlier, left here in case need to restore:
            // Exempt if this player already been moved for balance or unstacking
            if ((!mustMove && GetMoves(player) >= 1) || (mustMove && GetMoves(player) >= maxDispersalMoves)) {
                DebugBalance("Exempting ^b" + name + "^n, already moved this round");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }
            */

            /* Balance */

            int toTeamDiff = 0;
            int toTeam = ToTeam(name, player.Team, false, out toTeamDiff, ref mustMove); // take into account dispersal by Rank, etc.

            if (toTeam == 0 || toTeam == player.Team)
            {
                if (needsBalancing || mustMove)
                {
                    if (DebugLevel >= 7) DebugBalance("Exempting ^b" + name + "^n, target team selected is same or zero");
                    fExemptRound = fExemptRound + 1;
                    IncrementTotal();
                    return;
                }
            }

            int numTeams = 2; //(isSQDM) ? 4 : 2; // TBD, what is max squad size for SQDM?
            int maxTeamSlots = (MaximumServerSize / numTeams);
            int maxTeamPerMode = (perMode.MaxPlayers / numTeams);
            List<PlayerModel> lt = GetTeam(toTeam);
            int toTeamSize = (lt == null) ? 0 : lt.Count;

            if (toTeamSize == maxTeamSlots || toTeamSize == maxTeamPerMode)
            {
                if (DebugLevel >= 8) DebugBalance("Exempting ^b" + name + "^n, target team is full " + toTeamSize);
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }

            if (mustMove) DebugBalance("^4MUST MOVE^0 ^b" + name + "^n from " + GetTeamName(player.Team) + " to " + GetTeamName(toTeam));

            if ((!mustMove || lenient) && needsBalancing && toTeamDiff <= MaxDiff())
            {
                DebugBalance("Exempting ^b" + name + "^n, difference between " + GetTeamName(player.Team) + " team and " + GetTeamName(toTeam) + " team is only " + toTeamDiff);
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }

            /* Moved ticket ratios up here for Rout Percentage exemption */
            double ratio = 1;
            double t1Tickets = 0;
            double t2Tickets = 0;
            if (IsCTF() || IsCarrierAssault() || IsObliteration())
            {
                // Use team points, not tickets
                double usPoints = GetTeamPoints(1);
                double ruPoints = GetTeamPoints(2);
                if (usPoints <= 0) usPoints = 1;
                if (ruPoints <= 0) ruPoints = 1;
                ratio = (usPoints > ruPoints) ? (usPoints / ruPoints) : (ruPoints / usPoints);
            }
            else
            {
                // Otherwise use ticket ratio
                if (fTickets[losingTeam] >= 1)
                {
                    if (IsRush())
                    {
                        // normalize Rush ticket ratio
                        double attackers = fTickets[1];
                        double defenders = fMaxTickets - (fRushMaxTickets - fTickets[2]);
                        defenders = Math.Max(defenders, attackers / 2);
                        ratio = (attackers > defenders) ? (attackers / Math.Max(1, defenders)) : (defenders / Math.Max(1, attackers));
                        t1Tickets = attackers;
                        t2Tickets = defenders;
                    }
                    else
                    {
                        t1Tickets = Convert.ToDouble(fTickets[winningTeam]);
                        t2Tickets = Convert.ToDouble(fTickets[losingTeam]);
                        ratio = t1Tickets / Math.Max(1, t2Tickets);
                    }
                }
            }

            if ((fBalanceIsActive || mustMove) && toTeam != 0 && balanceSpeed != Speed.Stop)
            {
                String ts = null;
                if (isSQDM)
                {
                    ts = fTeam1.Count + "(A) vs " + fTeam2.Count + "(B) vs " + fTeam3.Count + "(C) vs " + fTeam4.Count + "(D)";
                }
                else
                {
                    ts = fTeam1.Count + "(" + GetTeamName(1) + ") vs " + fTeam2.Count + "(" + GetTeamName(2) + ")";
                }
                if (mustMove)
                {
                    DebugBalance("Autobalancing because ^b" + name + "^n must be moved");
                }
                else
                {
                    DebugBalance("Autobalancing because difference of " + diff + " is greater than " + MaxDiff() + ", [" + ts + "]");
                }
                double abTime = now.Subtract(fLastBalancedTimestamp).TotalSeconds;
                if (abTime > 0)
                {
                    DebugBalance("^2^bAutobalance has been active for " + abTime.ToString("F1") + " seconds!");
                }

                if (!loggedStats)
                {
                    DebugBalance(GetPlayerStatsString(name) + ((isStrong) ? " STRONG" : " WEAK"));
                    loggedStats = true;
                }

                /* Exemptions */

                // Handle Rout exemptions
                double ratioPercentage = ratio * 100;
                if (perMode.RoutPercentage > 100 && ratioPercentage >= perMode.RoutPercentage)
                {
                    DebugBalance("Rout detected, winning/losing ratio of " + ratioPercentage.ToString("F0") + " is greater than " + perMode.RoutPercentage.ToString("F0"));
                    if (isStrong)
                    {
                        String si = "(" + playerIndex + " of " + strongest + ")";
                        DebugBalance("Exempting ^b" + name + "^n^9 " + si + ", strong players are not moved during a rout");
                        fExemptRound = fExemptRound + 1;
                        IncrementTotal();
                        return;
                    }
                    else if (mustMove && lenient)
                    {
                        DebugBalance("Exempting ^b" + name + "^n^9, dispersal players are not moved during a rout when dispersal is lenient");
                        fExemptRound = fExemptRound + 1;
                        IncrementTotal();
                        return;
                    }
                }

                // Already on the smallest team
                if ((!mustMove || lenient) && player.Team == smallestTeam)
                {
                    DebugBalance("Exempting ^b" + name + "^n, already on the smallest team");
                    fExemptRound = fExemptRound + 1;
                    IncrementTotal();
                    return;
                }

                // SQDM, not on the biggest team
                if (isSQDM && !mustMove && balanceSpeed != Speed.Fast && player.Team != biggestTeam)
                {
                    // Make sure player's team isn't the same size as biggest
                    List<PlayerModel> aTeam = GetTeam(player.Team);
                    List<PlayerModel> bigTeam = GetTeam(biggestTeam);
                    if (aTeam == null || bigTeam == null || (aTeam != null && bigTeam != null && aTeam.Count < bigTeam.Count))
                    {
                        DebugBalance("Exempting ^b" + name + "^n, not on the biggest team");
                        fExemptRound = fExemptRound + 1;
                        IncrementTotal();
                        return;
                    }
                }

                // Exempt if only moving weak players and is strong
                if (!mustMove && perMode.OnlyMoveWeakPlayers && isStrong)
                {
                    DebugBalance("Exempting strong ^b" + name + "^n, Only Move Weak Players set to True for " + simpleMode);
                    fExemptRound = fExemptRound + 1;
                    IncrementTotal();
                    return;
                }

                // Strong/Weak exemptions
                if (!mustMove && balanceSpeed != Speed.Fast && fromList.Count >= minPlayers)
                {
                    if (DebugLevel > 5) DebugBalance(strongMsg);
                    // don't move weak player to losing team, unless we are only moving weak players
                    if (!isStrong && toTeam == losingTeam && !perMode.OnlyMoveWeakPlayers)
                    {
                        DebugBalance("Exempting ^b" + name + "^n, don't move weak player to losing team (#" + (playerIndex + 1) + " of " + fromList.Count + ", top " + (strongest) + ")");
                        fExemptRound = fExemptRound + 1;
                        IncrementTotal();
                        return;
                    }

                    // don't move strong player to winning team
                    if (isStrong && toTeam == winningTeam)
                    {
                        DebugBalance("Exempting ^b" + name + "^n, don't move strong player to winning team (#" + (playerIndex + 1) + " of " + fromList.Count + ", median " + (strongest) + ")");
                        fExemptRound = fExemptRound + 1;
                        IncrementTotal();
                        return;
                    }

                    // Don't move to same team
                    if (player.Team == toTeam)
                    {
                        if (DebugLevel >= 7) DebugBalance("Exempting ^b" + name + "^n, don't move player to his own team!");
                        IncrementTotal(); // no matching stat, reflect total deaths handled
                        return;
                    }
                }

                /* Move for balance */

                int origTeam = player.Team;
                String origName = GetTeamName(player.Team);

                if (lastMoveFrom != 0)
                {
                    origTeam = lastMoveFrom;
                    origName = GetTeamName(origTeam);
                }

                MoveInfo move = new MoveInfo(name, player.Tag, origTeam, origName, toTeam, GetTeamName(toTeam), YellDurationSeconds);
                move.For = MoveType.Balance;
                move.Format(this, ChatMovedForBalance, false, false);
                move.Format(this, YellMovedForBalance, true, false);
                String why = (mustMove) ? "to disperse evenly" : ("because difference is " + diff);
                log = "^4^bBALANCE^n^0 moving ^b" + player.FullName + "^n from " + move.SourceName + " team to " + move.DestinationName + " team " + why;
                log = (EnableLoggingOnlyMode) ? "^9(SIMULATING)^0 " + log : log;
                DebugWrite(log, 3);

                DebugWrite("^9" + move, 8);

                player.LastMoveFrom = player.Team;
                StartMoveImmediate(move, false);

                if (EnableLoggingOnlyMode)
                {
                    // Simulate completion of move
                    OnPlayerTeamChange(name, toTeam, 0);
                    OnPlayerMovedByAdmin(name, toTeam, 0, false); // simulate reverse order
                }
                // no increment total, handled later when move is processed
                return;
            }

            if (!fBalanceIsActive)
            {
                fLastBalancedTimestamp = now;
                if (DebugLevel >= 8) ConsoleDebug("fLastBalancedTimestamp = " + fLastBalancedTimestamp.ToString("HH:mm:ss"));
            }

            /* Unstack */

            // Not enabled or not full round
            if (!EnableUnstacking)
            {
                if (DebugLevel >= 8) DebugBalance("Unstack is disabled, Enable Unstacking is set to False");
                IncrementTotal();
                return;
            }
            else if (!fIsFullRound)
            {
                if (DebugLevel >= 7) DebugBalance("Unstack is disabled, not a full round");
                IncrementTotal();
                return;
            }

            // Sanity checks
            if (winningTeam <= 0 || winningTeam >= fTickets.Length || losingTeam <= 0 || losingTeam >= fTickets.Length || balanceSpeed == Speed.Stop)
            {
                if (DebugLevel >= 5) DebugBalance("Skipping unstack for player that was killed ^b" + name + "^n: winning = " + winningTeam + ", losingTeam = " + losingTeam + ", speed = " + balanceSpeed);
                IncrementTotal(); // no matching stat, reflect total deaths handled
                return;
            }

            // Server is full, can't swap
            if (totalPlayerCount > (MaximumServerSize - 2) || totalPlayerCount > (perMode.MaxPlayers - 2))
            {
                // TBD - kick idle players?
                if (DebugLevel >= 7) DebugBalance("No room to swap players for unstacking");
                IncrementTotal(); // no matching stat, reflect total deaths handled
                return;
            }

            // Disabled per-mode
            if (perMode.CheckTeamStackingAfterFirstMinutes == 0)
            {
                if (DebugLevel >= 5) DebugBalance("Unstacking has been disabled, Check Team Stacking After First Minutes set to zero");
                IncrementTotal(); // no matching stat, reflect total deaths handled
                return;
            }

            double tirMins = GetTimeInRoundMinutes();

            // Too soon to unstack
            if (tirMins < perMode.CheckTeamStackingAfterFirstMinutes)
            {
                DebugBalance("Too early to check for unstacking, skipping ^b" + name + "^n");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }

            // Maximum swaps already done
            if ((fUnstackedRound / 2) >= perMode.MaxUnstackingSwapsPerRound)
            {
                if (DebugLevel >= 6) DebugBalance("Maximum swaps have already occurred this round (" + (fUnstackedRound / 2) + ")");
                fUnstackState = UnstackState.Off;
                IncrementTotal(); // no matching stat, reflect total deaths handled
                return;
            }

            // Whitelisted
            if (OnWhitelist)
            {
                if (CheckWhitelist(player, WL_UNSTACK))
                {
                    DebugBalance("Excluding from unstacking due to being whitelisted, ^b" + name + "^n");
                    fExcludedRound = fExcludedRound + 1;
                    IncrementTotal();
                    return;
                }
            }

            /* - moved earlier, left here in case need to restore:
            double ratio = 1;
            double t1Tickets = 0;
            double t2Tickets = 0;
            if (IsCTF() || IsCarrierAssault()) {
                // Use team points, not tickets
                double usPoints = GetTeamPoints(1);
                double ruPoints = GetTeamPoints(2);
                if (usPoints <= 0) usPoints = 1;
                if (ruPoints <= 0) ruPoints = 1;
                ratio = (usPoints > ruPoints) ? (usPoints/ruPoints) : (ruPoints/usPoints);
            } else {
                // Otherwise use ticket ratio
                if (fTickets[losingTeam] >= 1) {
                    if (IsRush()) {
                        // normalize Rush ticket ratio
                        double attackers = fTickets[1];
                        double defenders = fMaxTickets - (fRushMaxTickets - fTickets[2]);
                        defenders = Math.Max(defenders, attackers/2);
                        ratio = (attackers > defenders) ? (attackers/Math.Max(1, defenders)) : (defenders/Math.Max(1, attackers));
                        t1Tickets = attackers;
                        t2Tickets = defenders;
                    } else {
                        t1Tickets = Convert.ToDouble(fTickets[winningTeam]);
                        t2Tickets = Convert.ToDouble(fTickets[losingTeam]);
                        ratio =  t1Tickets / Math.Max(1, t2Tickets);
                    }
                }
            }
            */

            // Ticket difference greater than per-mode maximum for unstacking
            int ticketGap = Convert.ToInt32(Math.Abs(t1Tickets - t2Tickets));
            if (perMode.MaxUnstackingTicketDifference > 0 && ticketGap > perMode.MaxUnstackingTicketDifference)
            {
                DebugBalance("Ticket difference of " + ticketGap + " exceeds Max Unstacking Ticket Difference of " + perMode.MaxUnstackingTicketDifference + ", skipping ^b" + name + "^n");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }

            String um = "Current ratio " + (ratio * 100.0).ToString("F0") + " vs. unstack ratio of " + (unstackTicketRatio * 100.0).ToString("F0");

            // Using player stats instead of ticket ratio
            if (perMode.EnableUnstackingByPlayerStats)
            {
                double a1 = GetAveragePlayerStats(1, perMode.DetermineStrongPlayersBy);
                double a2 = GetAveragePlayerStats(2, perMode.DetermineStrongPlayersBy);
                ratio = (a1 > a2) ? (a1 / Math.Max(0.01, a2)) : (a2 / Math.Max(0.01, a1));
                ratio = Math.Min(ratio, 50.0); // cap at 50x

                // Don't unstack if the team with the lowest average stats is the winning team
                // We don't want to send strong players to the team with the highest score!
                if ((a1 < a2 && winningTeam == 1)
                || (a2 < a1 && winningTeam == 2))
                {
                    if (DebugLevel >= 7) DebugBalance("Team with lowest avg. stats is the winning team, do not unstack: " + a1.ToString("F1") + " vs " + a2.ToString("F1") + ", winning team is " + GetTeamName(winningTeam));
                    IncrementTotal();
                    return;
                }
                String cmp = (a1 > a2) ? (a1.ToString("F1") + "/" + a2.ToString("F1")) : (a2.ToString("F1") + "/" + a1.ToString("F1"));
                um = "Average " + perMode.DetermineStrongPlayersBy + " stats ratio is " + (ratio * 100.0).ToString("F0") + " (" + cmp + ") vs. unstack ratio of " + (unstackTicketRatio * 100.0).ToString("F0");
            }

            // Using ticket loss instead of ticket ratio?
            if (perMode.EnableTicketLossRatio && false)
            { // disable for this release
                double a1 = GetAverageTicketLossRate(1, false);
                double a2 = GetAverageTicketLossRate(2, false);
                ratio = (a1 > a2) ? (a1 / Math.Max(1, a2)) : (a2 / Math.Max(1, a1));
                ratio = Math.Min(ratio, 50.0); // cap at 50x
                um = "Ticket loss ratio is " + (ratio * 100.0).ToString("F0") + " vs. unstack ratio of " + (unstackTicketRatio * 100.0).ToString("F0");

                // Don't unstack if the team with the highest loss rate is the winning team
                // We don't want to send strong players to the team with the highest score!
                if ((a1 > a2 && winningTeam == 1)
                || (a2 > a1 && winningTeam == 2))
                {
                    if (DebugLevel >= 7) DebugBalance("Team with highest ticket loss rate is the winning team, do not unstack: " + a1.ToString("F1") + " vs " + a2.ToString("F1") + ", winning team is " + GetTeamName(winningTeam));
                    IncrementTotal();
                    return;
                }
            }

            if (unstackTicketRatio == 0 || ratio < unstackTicketRatio)
            {
                bool ticketRatioOk = true;
                bool scoreRatioOk = true;
                int maxStages = 4;
                bool isRush = IsRush();
                if (fServerInfo != null && isRush) maxStages = GetRushMaxStages(fServerInfo.Map);
                if (isRush && perMode.EnableAdvancedRushUnstacking && fRushStage > 0 && fRushStage < maxStages)
                {
                    // Check team points as well as tickets
                    double usPoints = GetTeamPoints(1);
                    double ruPoints = GetTeamPoints(2);
                    if (usPoints <= 0) usPoints = 1;
                    if (ruPoints <= 0) ruPoints = 1;
                    ratio = (usPoints > ruPoints) ? (usPoints / ruPoints) : (ruPoints / usPoints);
                    if (DebugLevel >= 6) DebugBalance("Checking Advanced Rush Unstacking (by score): stage = " + fRushStage);
                    scoreRatioOk = (unstackTicketRatio == 0 || ratio < unstackTicketRatio);
                    if (!scoreRatioOk)
                    {
                        um = "(Advanced) score ratio is " + (ratio * 100.0).ToString("F0") + "% (" + usPoints.ToString("F0") + "/" + ruPoints.ToString("F0") + ") vs " + (unstackTicketRatio * 100.0).ToString("F0");
                    }
                }
                if (ticketRatioOk && scoreRatioOk)
                {
                    if (DebugLevel >= 6) DebugBalance("No unstacking needed: " + um);
                    IncrementTotal(); // no matching stat, reflect total deaths handled
                    return;
                }
            }

            // Handle Rout exemptions
            if (perMode.RoutPercentage > 100 && ratio >= perMode.RoutPercentage)
            {
                DebugBalance("No unstacking during a rout, skipping ^b" + name + "^n");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }

            /*
            Cases:
            1) Never unstacked before, timer is 0 and group count is 0
            2) Within a group, timer is 0 and group count is > 0 but < max
            3) Between groups, timer is > 0 and group count is 0
            */

            double nsis = NextSwapGroupInSeconds(perMode); // returns 0 for case 1 and case 2

            if (nsis > 0)
            {
                if (DebugLevel >= 6) DebugBalance("Too soon to do another unstack swap group, wait another " + nsis.ToString("F1") + " seconds!");
                IncrementTotal(); // no matching stat, reflect total deaths handled
                return;
            }
            else
            {
                fFullUnstackSwapTimestamp = DateTime.MinValue; // turn off timer
            }

            // Are the minimum number of players present to decide strong vs weak?
            if (!mustMove && balanceSpeed != Speed.Fast && fromList.Count < minPlayers)
            {
                DebugBalance("Not enough players in team to determine strong vs weak, skipping ^b" + name + "^n, ");
                fExemptRound = fExemptRound + 1;
                IncrementTotal();
                return;
            }

            // Otherwise, unstack!
            DebugBalance("^6Unstacking!^0 " + um);

            if (DebugLevel >= 6)
            {
                if (isStrong)
                {
                    DebugBalance("Player ^b" + player.Name + "^n is strong: #" + (playerIndex + 1) + " of " + fromList.Count + ", above #" + strongest + " at " + perMode.PercentOfTopOfTeamIsStrong.ToString("F0") + "%");
                }
                else
                {
                    DebugBalance("Player ^b" + player.Name + "^n is weak: #" + (playerIndex + 1) + " of " + fromList.Count + ", equal or below #" + strongest + " at " + perMode.PercentOfTopOfTeamIsStrong.ToString("F0") + "%");
                }
            }

            if (!loggedStats)
            {
                DebugBalance(GetPlayerStatsString(name));
                loggedStats = true;
            }

            MoveInfo moveUnstack = null;


            int origUnTeam = player.Team;
            String origUnName = GetTeamName(player.Team);
            String strength = "strong";

            if (lastMoveFrom != 0)
            {
                origUnTeam = lastMoveFrom;
                origUnName = GetTeamName(origUnTeam);
            }

            if (fUnstackState == UnstackState.Off)
            {
                // First swap
                DebugBalance("For ^b" + name + "^n, first swap of " + perMode.NumberOfSwapsPerGroup);
                fUnstackState = UnstackState.SwappedWeak;
            }

            switch (fUnstackState)
            {
                case UnstackState.SwappedWeak:
                    // Swap strong to losing team
                    if (isStrong)
                    {
                        // Don't move to same team
                        if (player.Team == losingTeam)
                        {
                            if (DebugLevel >= 6) DebugBalance("Skipping strong ^b" + name + "^n, don't move player to his own team!");
                            fExemptRound = fExemptRound + 1;
                            IncrementTotal();
                            return;
                        }
                        DebugBalance("Sending strong player ^0^b" + player.FullName + "^n^9 to losing team " + GetTeamName(losingTeam));
                        moveUnstack = new MoveInfo(name, player.Tag, origUnTeam, origUnName, losingTeam, GetTeamName(losingTeam), YellDurationSeconds);
                        toTeam = losingTeam;
                        fUnstackState = UnstackState.SwappedStrong;
                        if (EnableTicketLossRateLogging) UpdateTicketLossRateLog(now, losingTeam, 0);
                    }
                    else
                    {
                        DebugBalance("Skipping ^b" + name + "^n, don't move weak player to losing team (#" + (playerIndex + 1) + " of " + fromList.Count + ", median " + (strongest) + ")");
                        fExemptRound = fExemptRound + 1;
                        IncrementTotal();
                        return;
                    }
                    break;
                case UnstackState.SwappedStrong:
                    // Swap weak to winning team
                    if (!isStrong)
                    {
                        // Don't move to same team
                        if (player.Team == winningTeam)
                        {
                            if (DebugLevel >= 6) DebugBalance("Skipping weak ^b" + name + "^n, don't move player to his own team!");
                            fExemptRound = fExemptRound + 1;
                            IncrementTotal();
                            return;
                        }
                        DebugBalance("Sending weak player ^0^b" + player.FullName + "^n^9 to winning team " + GetTeamName(winningTeam));
                        moveUnstack = new MoveInfo(name, player.Tag, origUnTeam, origUnName, winningTeam, GetTeamName(winningTeam), YellDurationSeconds);
                        toTeam = winningTeam;
                        fUnstackState = UnstackState.SwappedWeak;
                        strength = "weak";
                        FinishedFullSwap(name, perMode); // updates group count
                        if (EnableTicketLossRateLogging) UpdateTicketLossRateLog(now, 0, winningTeam);
                    }
                    else
                    {
                        DebugBalance("Skipping ^b" + name + "^n, don't move strong player to winning team (#" + (playerIndex + 1) + " of " + fromList.Count + ", median " + (strongest) + ")");
                        fExemptRound = fExemptRound + 1;
                        IncrementTotal();
                        return;
                    }
                    break;
                case UnstackState.Off:
                // fall thru
                default: return;
            }

            /* Move for unstacking */

            log = "^4^bUNSTACK^n^0 moving " + strength + " ^b" + player.FullName + "^n from " + moveUnstack.SourceName + " to " + moveUnstack.DestinationName + " because: " + um;
            log = (EnableLoggingOnlyMode) ? "^9(SIMULATING)^0 " + log : log;
            DebugWrite(log, 3);
            moveUnstack.For = MoveType.Unstack;
            moveUnstack.Format(this, ChatMovedToUnstack, false, false);
            moveUnstack.Format(this, YellMovedToUnstack, true, false);

            DebugWrite("^9" + moveUnstack, 8);

            if (player.LastMoveFrom == 0) player.LastMoveFrom = player.Team;
            StartMoveImmediate(moveUnstack, false);

            if (EnableLoggingOnlyMode)
            {
                // Simulate completion of move
                OnPlayerTeamChange(name, toTeam, 0);
                OnPlayerMovedByAdmin(name, toTeam, 0, false); // simulate reverse order
            }
            // no increment total, handled by unstacking move
        }


        private void FastBalance(String trigger)
        {

            /* Useful variables */

            PlayerModel player = null;
            String simpleMode = String.Empty;
            PerModeSettings perMode = GetPerModeSettings();
            int winningTeam = 0;
            int losingTeam = 0;
            int biggestTeam = 0;
            int smallestTeam = 0;
            int[] ascendingSize = null;
            int[] descendingTickets = null;
            String strongMsg = String.Empty;
            int diff = 0;
            DateTime now = DateTime.Now;
            String log = String.Empty;
            int level = 6;
            int adj = 1;

            /* Sanity checks */

            if (fServerInfo == null)
            {
                return;
            }

            if (fGameState != GameState.Playing)
            {
                return;
            }

            if (IsNonBalancingMode())
            {
                return;
            }

            if (trigger.Contains("Kill"))
            {
                level = 8;
                adj = 0;
            }

            if (fLastFastMoveTimestamp != DateTime.MinValue && now.Subtract(fLastFastMoveTimestamp).TotalSeconds < 25)
            {
                if (DebugLevel >= (level + adj)) DebugFast("Too soon to check for fast balance again, wait another " + (25.0 - now.Subtract(fLastFastMoveTimestamp).TotalSeconds).ToString("F1") + " seconds");
                return;
            }

            Speed balanceSpeed = GetBalanceSpeed(perMode);

            if (balanceSpeed == Speed.Stop)
            {
                if (DebugLevel >= (level + adj)) DebugFast("Speed is Stop, fast balance check skipped. " + trigger + " was trigger"); // DebugBalance on purpose to get repeat filtering
                return;
            }

            int totalPlayerCount = TotalPlayerCount();

            if (DebugLevel >= (level + adj)) DebugFast(trigger + "Checking if fast balance is needed, " + totalPlayerCount + " players");

            if (totalPlayerCount >= (MaximumServerSize - 1))
            {
                if (DebugLevel >= (level + adj)) DebugFast("Server is full, no balancing or unstacking will be attempted!");
                return;
            }

            if (totalPlayerCount >= (perMode.MaxPlayers - 1))
            {
                if (DebugLevel >= (level + adj)) DebugFast("Server is full by per-mode Max Players, no balancing or unstacking will be attempted!");
                return;
            }

            int floorPlayers = (perMode.EnableLowPopulationAdjustments) ? 4 : 5;
            if (totalPlayerCount < floorPlayers)
            {
                if (DebugLevel >= (level + adj)) DebugFast("Not enough players in server, minimum is " + floorPlayers);
                return;
            }

            if (totalPlayerCount > 0)
            {
                AnalyzeTeams(out diff, out ascendingSize, out descendingTickets, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);
            }

            // Adjust speed to Fast?
            if (balanceSpeed != Speed.Fast)
            {
                if (diff > MaxFastDiff())
                {
                    balanceSpeed = Speed.Fast;
                }
            }
            if (balanceSpeed != Speed.Fast || diff <= MaxFastDiff())
            {
                if (diff > 1 && DebugLevel >= level) DebugFast("Fast balance not active, diff is only " + diff + ", requires more than " + MaxFastDiff());
                return;
            }

            // Prepare for player selection
            if (smallestTeam < 1)
            {
                DebugFast("Cannot determine smallest team: " + smallestTeam);
                return;
            }
            List<PlayerModel> big = new List<PlayerModel>();
            List<PlayerModel> tmp = GetTeam(biggestTeam);
            if (tmp == null || tmp.Count < 1 || biggestTeam < 1)
            {
                DebugFast("Cannot determine biggest team: " + biggestTeam);
                return;
            }
            big.AddRange(tmp);
            tmp = new List<PlayerModel>();
            foreach (PlayerModel p in big)
            {
                if (p == null) continue;
                if (fGameVersion != GameVersion.BF3 && p.Role >= 0 && p.Role < ROLE_NAMES.Length && p.Role != ROLE_PLAYER)
                {
                    if (DebugLevel >= 7) DebugFast("Excluding ^b" + p.Name + "^n, role is " + ROLE_NAMES[p.Role]);
                    continue;
                }
                else if (OnWhitelist && CheckWhitelist(p, WL_BALANCE))
                { // exclude if on whitelist
                    if (DebugLevel >= 7) DebugFast("Excluding ^b" + p.FullName + "^n: on Whitelist");
                    continue;
                }
                else if (p.MovedByMBTimestamp != DateTime.MinValue)
                { // exclude if moved recently 
                    double mins = now.Subtract(p.MovedByMBTimestamp).TotalMinutes;
                    if (mins < MinutesAfterBeingMoved)
                    {
                        if (DebugLevel >= 7) DebugFast("Excluding ^b" + p.Name + "^n: last move was " + mins.ToString("F0") + " minutes ago, less than required " + MinutesAfterBeingMoved.ToString("F0") + " minutes");
                        continue;
                    }
                    else
                    {
                        // reset
                        p.MovedByMBTimestamp = DateTime.MinValue;
                    }
                }

                tmp.Add(p);
            }
            big = tmp;

            // Select player
            if (DebugLevel >= 7) ConsoleDebug("FastBalance selecting player");
            if (big.Count < 1)
            {
                if (DebugLevel >= level) DebugFast("All players on " + GetTeamName(biggestTeam) + " team were excluded, unable to select the " + SelectFastBalanceBy + " player");
                return;
            }
            String kstat = String.Empty;
            switch (SelectFastBalanceBy)
            {
                case ForceMove.Weakest:
                    {
                        switch (perMode.DetermineStrongPlayersBy)
                        {
                            case DefineStrong.RoundScore:
                                big.Sort(DescendingRoundScore);
                                kstat = "S";
                                break;
                            case DefineStrong.RoundSPM:
                                big.Sort(DescendingRoundSPM);
                                kstat = "SPM";
                                break;
                            case DefineStrong.RoundKills:
                                big.Sort(DescendingRoundKills);
                                kstat = "K";
                                break;
                            case DefineStrong.RoundKDR:
                                big.Sort(DescendingRoundKDR);
                                kstat = "KDR";
                                break;
                            case DefineStrong.PlayerRank:
                                big.Sort(DescendingPlayerRank);
                                kstat = "R";
                                break;
                            case DefineStrong.RoundKPM:
                                big.Sort(DescendingRoundKPM);
                                kstat = "KPM";
                                break;
                            case DefineStrong.BattlelogSPM:
                                big.Sort(DescendingSPM);
                                kstat = "bSPM";
                                break;
                            case DefineStrong.BattlelogKDR:
                                big.Sort(DescendingKDR);
                                kstat = "bKDR";
                                break;
                            case DefineStrong.BattlelogKPM:
                                big.Sort(DescendingKPM);
                                kstat = "bKPM";
                                break;
                            default:
                                big.Sort(DescendingRoundScore);
                                break;
                        }

                        // Select weakest
                        player = big[big.Count - 1];
                        DebugFast("Selected WEAKEST player ^b" + player.FullName + "^n, " + kstat + ": " + GetPlayerStat(player, perMode.DetermineStrongPlayersBy).ToString("F1"));
                        break;
                    }

                case ForceMove.Newest:
                    {
                        // Descending by elapsed join time
                        big.Sort(delegate (PlayerModel lhs, PlayerModel rhs)
                        {
                            if (lhs == null)
                            {
                                return ((rhs == null) ? 0 : -1);
                            }
                            else if (rhs == null)
                            {
                                return ((lhs == null) ? 0 : 1);
                            }
                            double lTime = GetPlayerJoinedTimeSpan(lhs).TotalSeconds;
                            double rTime = GetPlayerJoinedTimeSpan(rhs).TotalSeconds;
                            if (lTime < rTime) return 1;
                            if (lTime > rTime) return -1;
                            return 0;
                        });
                        // Select newest
                        player = big[big.Count - 1];
                        DebugFast("Selected NEWEST player ^b" + player.FullName + "^n, joined " + GetPlayerJoinedTimeSpan(player).TotalMinutes.ToString("F1") + " minutes ago");
                        break;
                    }

                case ForceMove.Random:
                    {
                        Random rnd = new Random();
                        player = big[rnd.Next(big.Count)];
                        DebugFast("Selected RANDOM player ^b" + player.FullName);
                        break;
                    }
            }

            /* Move for fast balance */

            if (DebugLevel >= 7) ConsoleDebug("Move for fast balance");

            int origTeam = player.Team;
            String origName = GetTeamName(player.Team);
            int lastMoveFrom = player.LastMoveFrom;

            if (lastMoveFrom != 0)
            {
                origTeam = lastMoveFrom;
                origName = GetTeamName(origTeam);
            }

            MoveInfo move = new MoveInfo(player.Name, player.Tag, origTeam, origName, smallestTeam, GetTeamName(smallestTeam), 0);
            move.For = MoveType.Balance;
            // private message to player before getting killed
            move.Format(this, ChatMovedForBalance, false, true);
            move.Format(this, YellMovedForBalance, true, true);
            // regular message for after move
            move.Format(this, ChatMovedForBalance, false, false);
            move.Format(this, YellMovedForBalance, true, false);
            move.Fast = true;
            String why = "because difference is " + diff;
            log = "^4^bFAST BALANCE^n^0 moving ^b" + player.FullName + "^n from " + move.SourceName + " team to " + move.DestinationName + " team " + why;
            log = (EnableLoggingOnlyMode) ? "^9(SIMULATING)^0 " + log : log;
            DebugWrite(log, 3);

            DebugWrite("^9" + move, 8);

            player.LastMoveFrom = player.Team;
            fLastFastMoveTimestamp = DateTime.Now;

            KillAndMoveAsync(move);

            /*
            if (EnableLoggingOnlyMode) {
                // Simulate completion of move
                OnPlayerTeamChange(name, toTeam, 0);
                OnPlayerMovedByAdmin(name, toTeam, 0, false); // simulate reverse order
            }
            */

        }


        private bool IsKnownPlayer(String name)
        {
            bool check = false;
            lock (fAllPlayers)
            {
                check = fAllPlayers.Contains(name);
            }
            return check;
        }

        private bool AddNewPlayer(String name, int team)
        {
            bool known = false;
            bool needsFetch = false;
            PlayerModel player = null;
            lock (fKnownPlayers)
            {
                if (!fKnownPlayers.ContainsKey(name))
                {
                    player = new PlayerModel(name, team);
                    fKnownPlayers[name] = player;
                    needsFetch = true;
                }
                else
                {
                    player = fKnownPlayers[name];
                    player.Team = team;
                    player.FirstSeenTimestamp = DateTime.Now;
                    known = true;
                    needsFetch = !(player.TagVerified && player.StatsVerified);
                }
                if (player != null) player.LastSeenTimestamp = DateTime.Now;
            }
            lock (fAllPlayers)
            {
                if (!fAllPlayers.Contains(name)) fAllPlayers.Add(name);
            }
            if (needsFetch)
            {
                AddPlayerFetch(name);
            }
            UpdateFromWhitelist(player);
            return known;
        }

        private void RemovePlayer(String name)
        {
            bool gameChange = false;
            bool removeFetch = false;
            lock (fKnownPlayers)
            {
                if (fKnownPlayers.ContainsKey(name))
                {
                    // Keep around for MODEL_TIMEOUT minutes, in case player rejoins
                    PlayerModel m = fKnownPlayers[name];
                    m.ResetRound();
                    m.LastSeenTimestamp = DateTime.Now;
                    m.FirstSeenTimestamp = DateTime.MinValue;
                    removeFetch = true;
                }
            }
            if (removeFetch) RemovePlayerFetch(name);
            lock (fAllPlayers)
            {
                if (fAllPlayers.Contains(name)) fAllPlayers.Remove(name);

                if (fAllPlayers.Count < 4)
                {
                    if (fGameState != GameState.Warmup)
                    {
                        fGameState = GameState.Warmup;
                        gameChange = true;
                    }
                }
            }
            if (gameChange)
            {
                DebugWrite("RemovePlayer: ^b^3Game state = " + fGameState, 6);
            }
        }


        private void UpdatePlayerModel(String name, int team, int squad, String eaGUID, int score, int kills, int deaths, int rank, int role)
        {
            bool known = false;
            if (!IsKnownPlayer(name))
            {
                switch (fPluginState)
                {
                    case PluginState.JustEnabled:
                    case PluginState.Reconnected:
                        String state = (fPluginState == PluginState.JustEnabled) ? "JustEnabled" : "Reconnected";
                        if (team != 0)
                        {
                            known = AddNewPlayer(name, team);
                            String verb = (known) ? "^6renewing^0" : "^4adding^0";
                            DebugWrite(state + " state, " + verb + " new player: ^b" + name, 4);
                        }
                        else
                        {
                            DebugWrite(state + " state, unassigned player: ^b" + name, 4);
                            if (!fUnassigned.Contains(name)) fUnassigned.Add(name);
                            return;
                        }
                        break;
                    case PluginState.Active:
                        if (role == ROLE_PLAYER)
                        {
                            DebugWrite("Update waiting for ^b" + name + "^n to be assigned a team", 4);
                            if (!fUnassigned.Contains(name)) fUnassigned.Add(name);
                            return;
                        }
                        else
                        {
                            String sRole = (role == ROLE_SPECTATOR) ? "spectator" : "commander";
                            DebugWrite("Update adding " + sRole + " ^b" + name, 4);
                            AddNewPlayer(name, team); // add commanders and spectators
                        }
                        break;
                    case PluginState.Error:
                        DebugWrite("Error state, adding new player: ^b" + name, 4);
                        AddNewPlayer(name, team);
                        break;
                    default:
                        return;
                }
            }

            int unTeam = -2;
            PlayerModel m = null;

            lock (fKnownPlayers)
            {
                if (!fKnownPlayers.ContainsKey(name))
                {
                    ConsoleDebug("UpdatePlayerModel: player ^b" + name + "^n not in master table!");
                    return;
                }
                m = fKnownPlayers[name];
            }

            if (m.Team != team)
            {
                unTeam = m.Team;
                m.Team = team;
            }
            m.Squad = squad;
            m.EAGUID = eaGUID;
            m.ScoreRound = score;
            m.KillsRound = kills;
            m.DeathsRound = deaths;
            m.Rank = rank;
            m.Role = role;

            if (m.Role != ROLE_PLAYER)
                DebugWrite("UpdatePlayerModel: " + name + " has role = " + m.Role, 8);

            m.LastSeenTimestamp = DateTime.Now;

            // Computed
            m.KDRRound = m.KillsRound / Math.Max(1, m.DeathsRound);
            double mins = (m.FirstSpawnTimestamp == DateTime.MinValue) ? 1 : Math.Max(1, DateTime.Now.Subtract(m.FirstSpawnTimestamp).TotalMinutes);
            m.SPMRound = m.ScoreRound / mins;
            m.KPMRound = m.KillsRound / mins;

            // Accumulated
            // TBD

            // Friends
            UpdatePlayerFriends(m); // Overkill, but insures that Friendex is always updated

            if (!EnableLoggingOnlyMode && unTeam != -2 && !fPendingTeamChange.ContainsKey(name))
            {
                ConsoleDebug("UpdatePlayerModel:^b" + name + "^n has team " + unTeam + " but update says " + team + "!");
            }
        }


        private void UpdatePlayerTeam(String name, int team)
        {
            bool isKnown = IsKnownPlayer(name);
            if (!isKnown)
            {
                lock (fKnownPlayers)
                {
                    isKnown = fKnownPlayers.ContainsKey(name);
                }
                if (!isKnown)
                {
                    ConsoleDebug("UpdatePlayerTeam(" + name + ", " + team + ") not known!");
                    return;
                }
                lock (fAllPlayers)
                {
                    if (!fAllPlayers.Contains(name)) fAllPlayers.Add(name);
                }
            }

            PlayerModel m = GetPlayer(name);
            if (m == null) return;
            if (m.Role != ROLE_PLAYER)
                return;

            m.LastMoveFrom = 0; // reset

            if (m.Team != team)
            {
                if (m.Team == 0)
                {
                    DebugWrite("Assigning ^b" + name + "^n to " + team, 4);
                }
                else
                {
                    DebugWrite("^9Update player ^b" + name + "^n team from " + m.Team + " to " + team, 7);
                    m.Team = team;
                }
                m.LastSeenTimestamp = DateTime.Now;
            }
        }

        private void ValidateModel(List<CPlayerInfo> players, String revWhy)
        {
            if (fLastValidationTimestamp != DateTime.MinValue)
            {
                TimeSpan elapsed = DateTime.Now.Subtract(fLastValidationTimestamp);
                if (elapsed.TotalSeconds < 90.0)
                {
                    DebugWrite("Skipping revalidation: too soon, only " + elapsed.TotalSeconds.ToString("F0") + " seconds since last ValidateModel", 4);
                    return;
                }
            }
            fLastValidationTimestamp = DateTime.Now;

            DebugWrite("Revalidating all players and teams: " + revWhy, 3);

            // forget the active list, might be incorrect
            lock (fAllPlayers)
            {
                fAllPlayers.Clear();
            }
            fUnassigned.Clear();

            if (fGotLogin || fServerCrashed || (fTimeOutOfJoint > 0 && GetTimeInRoundMinutes() - fTimeOutOfJoint > 3.0))
            {
                fMoving.Clear();
                fReassigned.Clear();
            }

            if (players.Count == 0)
            {
                // no players, so waiting state
                fGameState = GameState.Warmup;
            }
            else
            {
                fPluginState = PluginState.Reconnected;
                // rebuild the data model and cancel any pending moves
                foreach (CPlayerInfo p in players)
                {
                    try
                    {
                        int bf4Type = (fGameVersion != GameVersion.BF3) ? p.Type : ROLE_PLAYER;
                        UpdatePlayerModel(p.SoldierName, p.TeamID, p.SquadID, p.GUID, p.Score, p.Kills, p.Deaths, p.Rank, bf4Type);
                        CheckAbortMove(p.SoldierName);
                    }
                    catch (Exception e)
                    {
                        ConsoleException(e);
                    }
                }
                /* Special handling for Reconnected state */
                fGameState = (TotalPlayerCount() < 4) ? GameState.Warmup : GameState.Unknown;
                UpdateTeams();
                UpdateAllFromWhitelist();
            }
            if (fServerCrashed) fRoundStartTimestamp = DateTime.Now;
            fPluginState = PluginState.Active;
            DebugWrite("^9ValidateModel: ^b^3State = " + fPluginState, 6);
            DebugWrite("^9ValidateModel: ^b^3Game state = " + fGameState, 6);
        }


        private bool CheckTeamSwitch(String name, int toTeam)
        {

            if (fPluginState != PluginState.Active || fGameState != GameState.Playing) return false;

            // Get model
            PlayerModel player = GetPlayer(name);
            if (player == null) return false;
            bool bogusMove = false;
            int lastMoveTo = 0;
            int lastMoveFrom = player.LastMoveFrom;

            // Same team?
            if (toTeam == player.Team)
            {
                /*
                This could happen with the following sequence of actions:
                + Player died and was moved from 1 to 2 for balance immediately, spawn messages set
                + While still dead, player switches himself back to 1 before respawning
                + All of this happens before a listPlayers refresh, so the model still thinks he is in team 1
                We have to detect that the switch is not to the intended team and fix everything up.
                */
                if (player.LastMoveTo != 0 && player.LastMoveTo != toTeam)
                {
                    DebugUnswitch("Player team switch: ^b" + name + "^n trying to switch to " + GetTeamName(toTeam) + " during a plugin move to " + GetTeamName(player.LastMoveTo));
                    bogusMove = true;
                    lastMoveTo = player.LastMoveTo;
                    player.LastMoveTo = 0;
                    DebugUnswitch("Ovewriting previous chat message for ^b" + name + "^n: " + player.SpawnChatMessage);
                    player.SpawnChatMessage = String.Empty;
                    player.SpawnYellMessage = String.Empty;
                }
                else
                {
                    DebugUnswitch("Player team switch: ^b" + name + "^n, player model already updated to " + GetTeamName(toTeam) + " team");
                    return true;
                }
            }
            else
            {
                DebugUnswitch("Player team switch: ^b" + name + "^n from " + GetTeamName(player.Team) + " team to " + GetTeamName(toTeam) + " team");
            }

            // Allow special cases
            if (player.Role != ROLE_PLAYER)
            {
                DebugUnswitch("ALLOWED: not a player role (Role = " + player.Role + ")");
                SetSpawnMessages(name, String.Empty, String.Empty, false);
                CheckAbortMove(name);
                return true;
            }
            else if (player.Team == 0)
            {
                DebugUnswitch("ALLOWED: switching from team 0 (Neutral)");
                SetSpawnMessages(name, String.Empty, String.Empty, false);
                CheckAbortMove(name);
                return true;
            }

            // Check if move already in progress for this player and abort it
            bool sendAbortMessage = false;
            lock (fMoveStash)
            {
                if (fMoveStash.Count > 0)
                {
                    // list only ever has one item
                    if (fMoveStash[0].Name == name)
                    {
                        fMoveStash.Clear();
                    }
                }
            }
            if (sendAbortMessage)
            {
                DebugUnswitch("ABORTED (by move stash): abort previous move by ^b" + name);
                sendAbortMessage = false;
            }

            // Whitelisted?
            if (OnWhitelist)
            {
                if (CheckWhitelist(player, WL_SWITCH))
                {
                    DebugUnswitch("ALLOWED: On whitelist: ^b" + name);
                    SetSpawnMessages(name, String.Empty, String.Empty, false);
                    CheckAbortMove(name);
                    return true;
                }
            }

            // Low population adjustments?
            PerModeSettings perMode = GetPerModeSettings();
            if (perMode.EnableLowPopulationAdjustments && GetPopulation(perMode, true) == Population.Low)
            {
                DebugUnswitch("ALLOWED: Enable Low Population Adjustments is True and population is Low: ^b" + name);
                SetSpawnMessages(name, String.Empty, String.Empty, false);
                CheckAbortMove(name);
                return true;
            }

            // Check forbidden cases
            bool isSQDM = IsSQDM();
            bool isDispersal = IsInDispersalList(player, false);
            bool isRank = IsRankDispersal(player);
            bool isClanDispersal = IsClanDispersal(player, false);
            bool forbidden = (((isDispersal || isRank || isClanDispersal) && Forbid(perMode, ForbidSwitchingAfterDispersal)) || (player.MovesByMBRound > 0 && !isSQDM && Forbid(perMode, ForbidSwitchingAfterAutobalance)));

            // Unlimited time?
            if (!forbidden && UnlimitedTeamSwitchingDuringFirstMinutesOfRound > 0 && GetTimeInRoundMinutes() <= UnlimitedTeamSwitchingDuringFirstMinutesOfRound)
            {
                DebugUnswitch("ALLOWED: Time in round " + GetTimeInRoundMinutes().ToString("F0") + " <= " + UnlimitedTeamSwitchingDuringFirstMinutesOfRound.ToString("F0") + ": ^b" + name);
                SetSpawnMessages(name, String.Empty, String.Empty, false);
                CheckAbortMove(name);
                return true;
            }

            // Minutes after joining?
            if (!forbidden && MinutesAfterJoining > 0 && GetPlayerJoinedTimeSpan(player).TotalMinutes <= MinutesAfterJoining)
            {
                DebugUnswitch("ALLOWED: Time since joining " + GetPlayerJoinedTimeSpan(player).TotalMinutes.ToString("F0") + " <= " + MinutesAfterJoining.ToString("F0") + ": ^b" + name);
                SetSpawnMessages(name, String.Empty, String.Empty, false);
                CheckAbortMove(name);
                return true;
            }

            // Helps?
            int diff = 0;
            int biggestTeam = 0;
            int smallestTeam = 0;
            int winningTeam = 0;
            int losingTeam = 0;
            int[] ascendingSize = null;
            int[] descendingTickets = null;
            int fromTeam = player.Team;
            MoveInfo move = null;
            bool toLosing = false;
            bool toSmallest = false;

            /*
            A player that was previously moved by the plugin is forbidden from moving to any
            other team by their own initiative for the rest of the round, unless this is
            SQDM mode. In SQDM, if a player is moved from A to B and then later decides
            to move to C, the losing team, that is allowed. Even in SQDM, though, no player
            is allowed to move to the winning team.

            All dispersal players are forbidden from moving themselves.
            */

            AnalyzeTeams(out diff, out ascendingSize, out descendingTickets, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);

            int iFrom = 0;
            int iTo = 0;

            if (isSQDM)
            {
                // Moving to any team with fewer tickets is encouraged
                for (int i = 0; i < descendingTickets.Length; ++i)
                {
                    if (fromTeam == descendingTickets[i]) iFrom = i;
                    if (toTeam == descendingTickets[i]) iTo = i;
                }
                toLosing = (iTo > iFrom);
            }
            else
            {
                toLosing = (toTeam == losingTeam);
            }

            // Trying to switch to losing team?
            if (!forbidden && toLosing && toTeam != biggestTeam)
            {
                move = new MoveInfo(player.Name, player.Tag, fromTeam, GetTeamName(fromTeam), toTeam, GetTeamName(toTeam), YellDurationSeconds);
                move.Format(this, ChatDetectedGoodTeamSwitch, false, true);
                move.Format(this, YellDetectedGoodTeamSwitch, true, true);
                DebugUnswitch("ALLOWED: Team switch to losing team ^b: " + name);
                SetSpawnMessages(name, move.ChatBefore, move.YellBefore, false);
                CheckAbortMove(name);
                return true;
            }

            if (isSQDM)
            {
                // Moving to any team with fewer players is encouraged
                for (int i = 0; i < ascendingSize.Length; ++i)
                {
                    if (fromTeam == ascendingSize[i]) iFrom = i;
                    if (toTeam == ascendingSize[i]) iTo = i;
                }
                toSmallest = (iTo < iFrom);
            }
            else
            {
                toSmallest = (toTeam == smallestTeam);
            }

            // Trying to switch to smallest team?
            if (!forbidden && toSmallest && toTeam != winningTeam)
            {
                move = new MoveInfo(player.Name, player.Tag, fromTeam, GetTeamName(fromTeam), toTeam, GetTeamName(toTeam), YellDurationSeconds);
                move.Format(this, ChatDetectedGoodTeamSwitch, false, true);
                move.Format(this, YellDetectedGoodTeamSwitch, true, true);
                DebugUnswitch("ALLOWED: Team switch to smallest team ^b: " + name);
                SetSpawnMessages(name, move.ChatBefore, move.YellBefore, false);
                CheckAbortMove(name);
                return true;
            }

            // Adjust for SQDM
            if (isSQDM && fServerInfo != null)
            {
                if (GetPopulation(perMode, true) == Population.Low)
                {
                    // Allow team switch to any team except biggest and winning
                    if (!forbidden && toTeam != biggestTeam && toTeam != winningTeam)
                    {
                        DebugUnswitch("ALLOWED: SQDM Low population and not switching to biggest or winning team: ^b" + name);
                        SetSpawnMessages(name, String.Empty, String.Empty, false);
                        CheckAbortMove(name);
                        return true;
                    }
                }
            }

            // Allow if ticket/point difference is less than allowed margin
            double win = 0;
            double lose = 0;
            double margin = 100;
            if (IsCTF() || IsCarrierAssault() || IsObliteration())
            {
                win = GetTeamPoints(winningTeam);
                if (win == 0) win = 1;
                lose = GetTeamPoints(losingTeam);
                if (lose == 0) lose = 1;
                margin = ((win > lose) ? win / lose : lose / win);
                // margin is 110%
                if (!forbidden && (margin * 100) <= 110)
                {
                    DebugUnswitch("ALLOWED: move by ^b" + name + "^n because score margin is only " + (margin * 100).ToString("F0") + "%");
                    SetSpawnMessages(name, String.Empty, String.Empty, false);
                    CheckAbortMove(name);
                    return true;
                }
            }
            else
            {
                win = fTickets[winningTeam];
                if (win == 0) win = 1;
                lose = fTickets[losingTeam];
                if (lose == 0) lose = 1;
                margin = ((win > lose) ? win / lose : lose / win);
                // margin is 105%
                if (!forbidden && (margin * 100) <= 105)
                {
                    DebugUnswitch("ALLOWED: move by ^b" + name + "^n because margin is only " + (margin * 100).ToString("F0") + "%");
                    SetSpawnMessages(name, String.Empty, String.Empty, false);
                    CheckAbortMove(name);
                    return true;
                }
            }


            // Otherwise, do not allow the team switch
            int origTeam = player.Team;
            String origName = GetTeamName(player.Team);

            if (lastMoveFrom != 0 && toTeam != lastMoveFrom)
            {
                DebugUnswitch("Setting toTeam from " + GetTeamName(toTeam) + " to original LastMoveFrom = " + GetTeamName(lastMoveFrom));
                toTeam = lastMoveFrom;
            }

            if (bogusMove)
            {
                origTeam = lastMoveTo;
                origName = GetTeamName(lastMoveTo);
            }

            // select forbidden message from: moved by autobalance, moved to unstack, dispersal, ...
            String badChat = ChatDetectedBadTeamSwitch;
            String badYell = YellDetectedBadTeamSwitch;

            ForbidBecause why = ForbidBecause.None;

            if (player.MovesByMBRound > 0 && !isSQDM)
            {
                why = ForbidBecause.MovedByBalancer;
                if (!Forbid(perMode, ForbidSwitchingAfterAutobalance))
                {
                    DebugUnswitch("ALLOWED: move by ^b" + name + "^n because ^bForbid Switch After Autobalance^n is False");
                    SetSpawnMessages(name, String.Empty, String.Empty, false);
                    CheckAbortMove(name);
                    return true;
                }
            }
            else if (toTeam == winningTeam)
            {
                why = ForbidBecause.ToWinning;
                if (!Forbid(perMode, ForbidSwitchingToWinningTeam))
                {
                    DebugUnswitch("ALLOWED: move by ^b" + name + "^n because ^bForbid Switch To Winning Team^n is False");
                    SetSpawnMessages(name, String.Empty, String.Empty, false);
                    CheckAbortMove(name);
                    return true;
                }
            }
            else if (toTeam == biggestTeam)
            {
                why = ForbidBecause.ToBiggest;
                if (!Forbid(perMode, ForbidSwitchingToBiggestTeam))
                {
                    DebugUnswitch("ALLOWED: move by ^b" + name + "^n because ^bForbid Switch To Biggest Team^n is False");
                    SetSpawnMessages(name, String.Empty, String.Empty, false);
                    CheckAbortMove(name);
                    return true;
                }
            }
            else if (isDispersal)
            {
                why = ForbidBecause.DisperseByList;
                if (!Forbid(perMode, ForbidSwitchingAfterDispersal))
                {
                    DebugUnswitch("ALLOWED: move by ^b" + name + "^n because ^bForbid Switch After Dispersal^n is False");
                    SetSpawnMessages(name, String.Empty, String.Empty, false);
                    CheckAbortMove(name);
                    return true;
                }
            }
            else if (isRank)
            {
                why = ForbidBecause.DisperseByRank;
                if (!Forbid(perMode, ForbidSwitchingAfterDispersal))
                {
                    DebugUnswitch("ALLOWED: move by ^b" + name + "^n because ^bForbid Switch After Dispersal^n is False");
                    SetSpawnMessages(name, String.Empty, String.Empty, false);
                    CheckAbortMove(name);
                    return true;
                }
            }
            else if (isClanDispersal)
            {
                why = ForbidBecause.DisperseByClan;
                if (!Forbid(perMode, ForbidSwitchingAfterDispersal))
                {
                    DebugUnswitch("ALLOWED: move by ^b" + name + "^n because ^bForbid Switch After Dispersal^n is False");
                    SetSpawnMessages(name, String.Empty, String.Empty, false);
                    CheckAbortMove(name);
                    return true;
                }
            }

            // Check switch to same team?
            if (toTeam == origTeam)
            {
                ConsoleDebug("CheckTeamSwitch: ^b" + name + "^n, can't forbid unswitch to same team " + GetTeamName(toTeam) + "?");
                SetSpawnMessages(name, String.Empty, String.Empty, false);
                CheckAbortMove(name);
                return true;
            }

            /*
            Too soon to move again.

            This can happen when another plugin, particularly another instance of MB, is moving players.
            Players get into a ping-poing unswitch loop. Adding a time check will prevent this.
            */
            double esm = DateTime.Now.Subtract(player.MovedTimestamp).TotalSeconds;
            if (esm < 15)
            {
                DebugUnswitch("IGNORED: switch by ^b" + name + "^n, too soon (" + esm.ToString("F1") + " secs ago) since last move, maybe another plugin is switching this player?");
                SetSpawnMessages(name, String.Empty, String.Empty, false);
                CheckAbortMove(name);
                return true;
            }

            // Tried to switch toTeam from origTeam, so moving from toTeam back to origTeam
            move = new MoveInfo(name, player.Tag, toTeam, GetTeamName(toTeam), origTeam, origName, YellDurationSeconds);
            move.For = MoveType.Unswitch;
            move.Because = why;
            move.Format(this, badChat, false, true);
            move.Format(this, badYell, true, true);
            move.Format(this, ChatAfterUnswitching, false, false);
            move.Format(this, YellAfterUnswitching, true, false);
            player.LastMoveFrom = 0;

            if (DebugLevel >= 8) DebugUnswitch(move.ToString());

            if (isSQDM || !EnableImmediateUnswitch)
            {
                // Delay action until after the player spawns
                DebugUnswitch("FORBIDDEN: delaying unswitch action until spawn of ^b" + name + "^n from " + move.SourceName + " back to " + move.DestinationName);

                if (player.DelayedMove != null)
                {
                    CheckAbortMove(name);
                }
                player.DelayedMove = move;

                if (!String.IsNullOrEmpty(player.SpawnChatMessage))
                {
                    DebugUnswitch("IGNORED: previously delayed spawn message for ^b" + name + "^n: " + player.SpawnChatMessage);
                    SetSpawnMessages(name, String.Empty, String.Empty, false);
                }
            }
            else
            {
                // Do the move immediately
                DebugUnswitch("FORBIDDEN: immediately unswitch ^b" + name + "^n from " + move.SourceName + " back to " + move.DestinationName);
                String log = "^4^bUNSWITCHING^n^0 ^b" + player.FullName + "^n from " + move.SourceName + " back to " + move.DestinationName;
                log = (EnableLoggingOnlyMode) ? "^9(SIMULATING)^0 " + log : log;
                DebugWrite(log, 3);
                StartMoveImmediate(move, true);
            }

            return false;
        }

        private void CheckAbortMove(String name)
        {
            lock (fMoveQ)
            {
                if (fMoveQ.Count > 0)
                {
                    bool foundAbort = false;
                    foreach (MoveInfo mi in fMoveQ)
                    {
                        if (mi.Name == name)
                        {
                            mi.aborted = true;
                            foundAbort = true;
                        }
                    }
                    if (foundAbort) Monitor.Pulse(fMoveQ);
                }
            }

            PlayerModel player = GetPlayer(name);
            if (player == null) return;

            if (player.DelayedMove != null)
            {
                DebugUnswitch("IGNORED: abort delayed move of ^b" + name + "^n to " + player.DelayedMove.DestinationName);
                player.DelayedMove = null;
            }
        }

        private void SpawnUpdate(String name)
        {
            bool ok = false;
            bool updated = false;
            DateTime now = DateTime.Now;
            lock (fKnownPlayers)
            {
                PlayerModel m = null;
                if (fKnownPlayers.TryGetValue(name, out m))
                {
                    if (m.Role != ROLE_PLAYER)
                        return;
                    ok = true;
                    // If first spawn timestamp is earlier than round start, update it
                    if (m.FirstSpawnTimestamp == DateTime.MinValue || DateTime.Compare(m.FirstSpawnTimestamp, fRoundStartTimestamp) < 0)
                    {
                        m.FirstSpawnTimestamp = now;
                        updated = true;
                    }
                    m.LastSeenTimestamp = now;
                    m.IsDeployed = true;
                }
            }

            if (!ok)
            {
                ConsoleDebug("player " + name + " spawned, but not a known player!");
            }

            if (updated)
            {
                DebugWrite("^9Spawn: ^b" + name + "^n @ " + now.ToString("HH:mm:ss"), 6);
            }
        }


        private void KillUpdate(String killer, String victim)
        {
            if (fPluginState != PluginState.Active) return;
            bool okVictim = false;
            bool okKiller = false;
            DateTime now = DateTime.Now;
            TimeSpan tir = TimeSpan.FromSeconds(0); // Time In Round
            lock (fKnownPlayers)
            {
                PlayerModel m = null;

                if (fKnownPlayers.TryGetValue(killer, out m))
                {
                    if (m.Role == ROLE_PLAYER)
                    {
                        m.LastSeenTimestamp = now;
                        m.IsDeployed = true;
                    }
                    okKiller = true;
                }
                if (killer == victim)
                {
                    okVictim = okKiller;
                }
                else
                {
                    if (fKnownPlayers.TryGetValue(victim, out m))
                    {
                        if (m.Role == ROLE_PLAYER)
                        {
                            m.LastSeenTimestamp = now;
                            m.IsDeployed = false;
                            tir = now.Subtract((m.FirstSpawnTimestamp != DateTime.MinValue) ? m.FirstSpawnTimestamp : now);
                        }
                        okVictim = true;
                    }
                }

            }

            if (!okKiller)
            {
                ConsoleDebug("player ^b" + killer + "^n is a killer, but not a known player!");
            }

            if (!okVictim)
            {
                ConsoleDebug("player ^b" + victim + "^n is a victim, but not a known player!");
            }
        }


        private void StartMoveImmediate(MoveInfo move, bool sendMessages)
        {
            // Do an immediate move, also used by the move thread
            if (!fIsEnabled || fPluginState != PluginState.Active)
            {
                ConsoleDebug("StartMoveImmediate called while fIsEnabled is " + fIsEnabled + " or fPluginState is " + fPluginState);
                return;
            }

            fLastFastMoveTimestamp = DateTime.Now; // Any move resets the timer for fast moves

            // Send before messages?
            if (sendMessages)
            {
                Yell(move.Name, move.YellBefore);
                Chat(move.Name, move.ChatBefore, (move.For == MoveType.Unswitch || QuietMode)); // player only if unswitch or Quiet
            }

            lock (fMoving)
            {
                if (!fMoving.ContainsKey(move.Name)) fMoving[move.Name] = move;
            }
            // Do the move
            if (!EnableLoggingOnlyMode)
            {
                int toSquad = ToSquad(move.Name, move.Destination);
                ServerCommand("admin.movePlayer", move.Name, move.Destination.ToString(), toSquad.ToString(), "false");
                ScheduleListPlayers(10);
            }

            // Remember move
            PlayerModel player = GetPlayer(move.Name);
            if (player != null)
            {
                if (player.LastMoveTo != 0) ConsoleDebug("StartMoveImmediate: ^b" + move.Name + "^n player.LastMoveTo != 0, " + player.LastMoveTo);
                player.LastMoveTo = move.Destination;
            }

            // Log move
            String r = null;
            switch (move.For)
            {
                case MoveType.Balance: r = " for balance"; break;
                case MoveType.Unstack: r = " to unstack teams"; break;
                case MoveType.Unswitch: r = " to unswitch player"; break;
                default: r = " for ???"; break;
            }
            String moving = (move.Fast) ? "FAST MOVING" : "MOVING";
            String doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^1" + moving + "^0 " : "^b^1" + moving + "^0 ";
            DebugWrite(doing + move.Name + "^n from " + move.SourceName + " to " + move.DestinationName + r, 4);
        }

        private bool FinishMove(String name, int team)
        {
            // If this is an MB move, handle it
            MoveInfo move = null;
            lock (fMoving)
            {
                if (fMoving.ContainsKey(name))
                {
                    move = fMoving[name];
                    fMoving.Remove(name);
                    try
                    {
                        UpdatePlayerTeam(name, team);
                        UpdateTeams();
                        if (move.For == MoveType.Balance) { ++fBalancedRound; IncrementMoves(name); IncrementTotal(); }
                        else if (move.For == MoveType.Unstack) { ++fUnstackedRound; IncrementMoves(name); IncrementTotal(); }
                        else if (move.For == MoveType.Unswitch) { ++fUnswitchedRound; UpdateMoveTime(name); IncrementTotal(); }
                    }
                    catch (Exception e)
                    {
                        ConsoleException(e);
                    }
                }
            }
            if (move != null)
            {
                // MB move for balance/unstacking/unswitching
                SetSpawnMessages(move.Name, move.ChatAfter, move.YellAfter, (move.For == MoveType.Unswitch || QuietMode));
            }
            return (move != null);
        }

        private void KillAndMoveAsync(MoveInfo move)
        {
            lock (fMoveQ)
            {
                fMoveQ.Enqueue(move);
                Monitor.Pulse(fMoveQ);
            }
        }

        public void MoveLoop()
        {
            try
            {
                while (fIsEnabled)
                {
                    MoveInfo move = null;
                    lock (fMoveQ)
                    {
                        while (fMoveQ.Count == 0)
                        {
                            Monitor.Wait(fMoveQ);
                            if (!fIsEnabled) return;
                        }
                        move = fMoveQ.Dequeue();
                    }

                    // Check abort flag
                    if (move.aborted)
                    {
                        DebugUnswitch("ABORTING original move for ^b" + move.Name + "^n to " + move.DestinationName + ", newer move in progress");
                        continue;
                    }

                    // Sending before messages
                    Yell(move.Name, move.YellBefore);
                    Chat(move.Name, move.ChatBefore, (move.For == MoveType.Balance || move.For == MoveType.Unswitch || QuietMode)); // player only if balancing or unswitching or Quiet

                    // Stash for check later
                    lock (fMoveStash)
                    {
                        fMoveStash.Clear();
                        fMoveStash.Add(move);
                    }

                    // Pause
                    Thread.Sleep(Convert.ToInt32(move.Delay * 1000));
                    if (!fIsEnabled) return;

                    // Player may have started another move during the delay, check and abort
                    lock (fMoveStash)
                    {
                        if (fMoveStash.Count == 0)
                        {
                            DebugUnswitch("ABORTING original move for ^b" + move.Name + "^n to " + move.DestinationName + ", new move pending");
                            continue;
                        }
                        fMoveStash.Clear();
                    }
                    lock (fMoveQ)
                    {
                        foreach (MoveInfo mi in fMoveQ)
                        {
                            if (mi.Name == move.Name)
                            {
                                DebugUnswitch("ABORTING original move for ^b" + move.Name + "^n to " + move.DestinationName + ", now moving to " + mi.DestinationName);
                                continue;
                            }
                        }
                    }

                    // Make sure player is dead
                    if (!EnableLoggingOnlyMode)
                    {
                        ServerCommand("admin.killPlayer", move.Name);
                        DebugWrite("^b^1ADMIN KILL^0 " + move.Name, 4);
                    }
                    else
                    {
                        DebugWrite("^9(SIMULATING) ^b^1ADMIN KILL^0 " + move.Name, 4);
                    }

                    // Pause
                    Thread.Sleep(1 * 1000);
                    if (!fIsEnabled) return;

                    // Move player
                    StartMoveImmediate(move, false);
                }
            }
            catch (ThreadAbortException)
            {
                fAborted = true;
                return;
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
            finally
            {
                if (!fAborted) ConsoleWrite("^bMoveLoop^n thread stopped", 0);
            }
        }


        private void Reassign(String name, int fromTeam, int toTeam, int diff)
        {
            if (toTeam == 0) toTeam = fromTeam;
            // This is not a known player yet, so not PlayerModel to use
            // Just do a raw move as quickly as possible, no messages, just logging
            String doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^4REASSIGNING^0^n new player ^b" : "^b^4REASSIGNING^0^n new player ^b";
            String because = (diff > 0) ? ", because difference is " + diff : String.Empty;
            if (!fWhileScrambling)
            {
                DebugWrite(doing + name + "^n from " + GetTeamName(fromTeam) + " team to " + GetTeamName(toTeam) + " team" + because, 4);
            }
            else
            {
                DebugWrite(doing + name + "^n to " + GetTeamName(toTeam) + " team" + because, 4);
            }
            int toSquad = ToSquad(name, toTeam);
            if (!EnableLoggingOnlyMode)
            {
                if (fromTeam != toTeam) fReassigned.Add(name);
                ServerCommand("admin.movePlayer", name, toTeam.ToString(), toSquad.ToString(), "false");
                if (fWhileScrambling)
                {
                    lock (fExtrasLock)
                    {
                        if (!fExtraNames.Contains(name)) fExtraNames.Add(name);
                        fDebugScramblerSuspects[name] = "New player ^b{0}^n joined " + GetTeamName(toTeam) + "/" + GetSquadName(toSquad);
                    }
                    // Can't use reassigning logic if player is already in the right team
                    if (fromTeam == toTeam)
                    {
                        IncrementTotal(); // no matching stat, reflects non-reassigment joins
                        AddNewPlayer(name, toTeam);
                        UpdateTeams();
                        DebugWrite("^4New player^0: ^b" + name + "^n, assigned to " + GetTeamName(toTeam) + " team during scrambling", 4);
                    }
                }
                ScheduleListPlayers(1);
            }
            else
            {
                // Simulate reassignment
                fReassigned.Add(name);
                ScheduleListPlayers(1);
                OnPlayerTeamChange(name, toTeam, toSquad);
            }
        }

        private bool IsModelInSync()
        {
            lock (fMoving)
            {
                return (fMoving.Count == 0 && fReassigned.Count == 0);
            }
        }

        private void ValidateMove(String name)
        {
            /*
            This may be the return leg of the round-trip to insure that
            a move for balance MB has completed. If fMoving still
            contains the player's name, the move failed.
            */
            bool completedMove = true;
            lock (fMoving)
            {
                if (fMoving.ContainsKey(name))
                {
                    completedMove = false;
                    fMoving.Remove(name);
                }
            }
            if (!completedMove)
            {
                ConsoleDebug("Move of ^b" + name + "^n failed!");
                IncrementTotal();
                fFailedRound = fFailedRound + 1;
                return;
            }
            /*
            This may be the return leg of the round-trip to insure that
            a reassignment of a player by MB has completed. If fReassigned still
            contains the player's name, the move failed.
            */
            bool completedReassign = true;
            lock (fReassigned)
            {
                if (fReassigned.Contains(name))
                {
                    completedReassign = false;
                    fReassigned.Remove(name);
                }
            }
            if (!completedReassign)
            {
                ConsoleDebug("Reassign of ^b" + name + "^n failed!");
                fFailedRound = fFailedRound + 1;
                IncrementTotal();
                AddNewPlayer(name, 0);
                UpdateTeams();
                return;
            }
        }

        private void Chat(String who, String what)
        {
            Chat(who, what, QuietMode);
        }

        private void Chat(String who, String what, bool quiet)
        {
            String doing = null;
            if (String.IsNullOrEmpty(what)) return;
            if (quiet)
            {
                if (!EnableLoggingOnlyMode)
                {
                    ServerCommand("admin.say", what, "player", who); // chat player only
                }
                ProconChatPlayer(who, what);
                doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^1CHAT^0^n to ^b" : "^b^1CHAT^0^n to ^b";
                DebugWrite(doing + who + "^n: " + what, 4);
            }
            else
            {
                if (!EnableLoggingOnlyMode)
                {
                    ServerCommand("admin.say", what, "all"); // chat all
                }
                ProconChat(what);
                doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^1CHAT^0^n to all: " : "^b^1CHAT^0^n to all: ";
                DebugWrite(doing + what, 4);
            }
            SendToAllSubscribers(what);
        }

        private void Yell(String who, String what)
        {
            String doing = null;
            if (String.IsNullOrEmpty(what)) return;
            if (!EnableLoggingOnlyMode)
            {
                ServerCommand("admin.yell", what, YellDurationSeconds.ToString("F0"), "player", who); // yell to player
            }
            doing = (EnableLoggingOnlyMode) ? "^9(SIMULATING) ^b^1YELL^0^n to ^b" : "^b^1YELL^0^n to ^b";
            DebugWrite(doing + who + "^n: " + what, 4);
        }

        private void ProconChat(String what)
        {
            if (fAborted) return;
            if (String.IsNullOrEmpty(what)) return;
            if (EnableLoggingOnlyMode) what = "(SIMULATING) " + what;
            if (LogChat) ExecuteCommand("procon.protected.chat.write", GetPluginName() + " > All: " + what);
        }

        private void ProconChatPlayer(String who, String what)
        {
            if (fAborted) return;
            if (String.IsNullOrEmpty(what)) return;
            if (EnableLoggingOnlyMode) what = "(SIMULATING) " + what;
            if (LogChat) ExecuteCommand("procon.protected.chat.write", GetPluginName() + " > " + who + ": " + what);
        }

        private void SendToAllSubscribers(String what)
        {
            if (String.IsNullOrEmpty(what)) return;
            try
            {
                List<String> subscribers = new List<String>();
                lock (fAllPlayers)
                {
                    foreach (String name in fAllPlayers)
                    {
                        PlayerModel p = GetPlayer(name);
                        if (p != null && p.Subscribed)
                        {
                            subscribers.Add(name);
                        }
                    }
                }
                foreach (String who in subscribers)
                {
                    if (!EnableLoggingOnlyMode)
                    {
                        ServerCommand("admin.say", what, "player", who); // chat player only
                        if (DebugLevel >= 7) ConsoleDebug("Sent chat message to subscriber ^b" + who);
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        private void GarbageCollectKnownPlayers()
        {
            int n = 0;
            bool revalidate = false;
            lock (fKnownPlayers)
            {
                List<String> garbage = new List<String>();

                // collect up garbage
                foreach (String name in fKnownPlayers.Keys)
                {
                    PlayerModel m = fKnownPlayers[name];
                    m.LastMoveTo = 0; // reset this value while we are here
                    if (DateTime.Now.Subtract(m.LastSeenTimestamp).TotalMinutes > MODEL_TIMEOUT)
                    {
                        if (IsKnownPlayer(name))
                        {
                            ConsoleDebug("^b" + name + "^n has timed out and is still on active players list, idling?");
                            // Revalidate the data model
                            revalidate = true;
                        }
                        else
                        {
                            garbage.Add(name);
                        }
                    }
                }

                // remove garbage
                if (garbage.Count > 0) foreach (String name in garbage)
                    {
                        fKnownPlayers.Remove(name);
                        n = n + 1;
                    }
            }

            if (revalidate)
            {
                lock (fAllPlayers)
                {
                    fAllPlayers.Clear();
                    ScheduleListPlayers(1);
                }
            }

            if (n > 0)
            {
                DebugWrite("^9Garbage collected " + n + " old players from known players table", 6);
            }
        }

        private Phase GetPhase(PerModeSettings perMode, bool verbose)
        {
            if (perMode == null) return Phase.Mid;
            // earlyTickets relative to max for count down, 0 for count up
            // lateTickets relative to 0 for count down, max for count up
            double earlyTickets = perMode.DefinitionOfEarlyPhaseFromStart;
            double lateTickets = perMode.DefinitionOfLatePhaseFromEnd;
            Phase phase = Phase.Mid;

            if (fServerInfo == null) return phase;

            if (AdjustForMetro(perMode))
            {
                lateTickets = perMode.MetroAdjustedDefinitionOfLatePhase;
            }

            // Special handling for CTF & Carrier Assault modes
            bool isCTF = IsCTF();
            bool isCarrierAssault = IsCarrierAssault();
            bool isObliteration = IsObliteration();
            if (isCTF || isCarrierAssault || isObliteration)
            {
                if (fRoundStartTimestamp == DateTime.MinValue) return Phase.Early;

                double earlyMinutes = earlyTickets;
                double lateMinutes = lateTickets;

                // TBD - assume max round time is 20 minutes for CTF at 100%
                // TBD - assume max round time is 30 minutes for CRL/CRS at 100%
                double maxMinutes = ((isCTF) ? 20 : 30) * fRoundTimeLimit;
                if (verbose && DebugLevel >= 8) ConsoleDebug("fRoundTimeLimit = " + (fRoundTimeLimit * 100).ToString("F0") + ", maxMinutes = " + maxMinutes);
                //double totalRoundMins = DateTime.Now.Subtract(fRoundStartTimestamp).TotalMinutes;
                double totalRoundMins = GetTimeInRoundMinutes();

                /* moved to ValidateSettings, keep here for reference
                // Late is higher priority than early
                if (lateMinutes > maxMinutes) {earlyMinutes = 0; lateMinutes = maxMinutes;}
                if (earlyMinutes > (maxMinutes - lateMinutes)) {earlyMinutes = maxMinutes - lateMinutes;}
                */

                if (totalRoundMins <= earlyMinutes)
                {
                    phase = Phase.Early;
                }
                else if (totalRoundMins >= (maxMinutes - lateMinutes))
                {
                    phase = Phase.Late;
                }
                else
                {
                    phase = Phase.Mid;
                }

                if (verbose && DebugLevel >= 8) ConsoleDebug("Phase: " + phase + " (" + totalRoundMins.ToString("F0") + " mins [" + earlyMinutes.ToString("F0") + " - " + (maxMinutes - lateMinutes).ToString("F0") + "])");
                return phase;
            }

            if (fServerInfo.TeamScores == null || fServerInfo.TeamScores.Count < 2) return Phase.Mid;

            double tickets = -1;
            double goal = 0;
            bool countDown = true;

            if (fMaxTickets == -1) return Phase.Early;

            if (IsCountUp())
            {
                countDown = false;
                foreach (TeamScore ts in fServerInfo.TeamScores)
                {
                    if (ts.TeamID == 1)
                    {
                        goal = ts.WinningScore;
                        break;
                    }
                }
            }

            // Find ticket count closest to end
            foreach (TeamScore ts in fServerInfo.TeamScores)
            {
                if (tickets == -1)
                {
                    tickets = ts.Score;
                }
                else
                {
                    if (countDown)
                    {
                        if (ts.Score < tickets)
                        {
                            tickets = ts.Score;
                        }
                    }
                    else
                    {
                        if (ts.Score > tickets)
                        {
                            tickets = ts.Score;
                        }
                    }
                }
            }

            if (countDown)
            {
                // Late takes priority over early
                if (lateTickets > fMaxTickets) { earlyTickets = 0; lateTickets = fMaxTickets; }
                if (lateTickets > (fMaxTickets - earlyTickets)) { earlyTickets = fMaxTickets - lateTickets; }

                if (tickets <= lateTickets)
                {
                    phase = Phase.Late;
                }
                else if (fIsFullRound && (earlyTickets < fMaxTickets) && tickets >= (fMaxTickets - earlyTickets))
                {
                    phase = Phase.Early;
                }
                else
                {
                    phase = Phase.Mid;
                }
            }
            else
            {
                // count up
                // Late takes priority over early
                if (lateTickets > goal) { earlyTickets = 0; lateTickets = goal; }
                if (earlyTickets > (goal - lateTickets)) { earlyTickets = goal - lateTickets; }

                if (lateTickets < goal && tickets >= (goal - lateTickets))
                {
                    phase = Phase.Late;
                }
                else if (tickets <= earlyTickets)
                {
                    phase = Phase.Early;
                }
                else
                {
                    phase = Phase.Mid;
                }
            }

            if (verbose && DebugLevel >= 8) ConsoleDebug("Phase: " + phase + " (" + tickets + " of " + fMaxTickets + " to " + goal + ", " + RemainingTicketPercent(tickets, goal).ToString("F0") + "%)");

            return phase;
        }

        private Population GetPopulation(PerModeSettings perMode, bool verbose)
        {
            if (fServerInfo == null) return Population.Medium;

            int highPop = perMode.DefinitionOfHighPopulationForPlayers;
            int lowPop = perMode.DefinitionOfLowPopulationForPlayers;
            Population pop = Population.Low;

            int totalPop = TotalPlayerCount();

            if (totalPop <= lowPop)
            {
                pop = Population.Low;
            }
            else if (totalPop >= highPop)
            {
                pop = Population.High;
            }
            else
            {
                pop = Population.Medium;
            }

            if (verbose && DebugLevel >= 8) ConsoleDebug("Population: " + pop + " (" + totalPop + " [" + lowPop + " - " + highPop + "])");

            return pop;
        }

        private double GetUnstackTicketRatio(PerModeSettings perMode)
        {
            Phase phase = GetPhase(perMode, false);
            Population pop = GetPopulation(perMode, false);
            double unstackTicketRatio = 0;

            if (perMode.CheckTeamStackingAfterFirstMinutes == 0) return 0;

            switch (phase)
            {
                case Phase.Early:
                    switch (pop)
                    {
                        case Population.Low: unstackTicketRatio = EarlyPhaseTicketPercentageToUnstack[0]; break;
                        case Population.Medium: unstackTicketRatio = EarlyPhaseTicketPercentageToUnstack[1]; break;
                        case Population.High: unstackTicketRatio = EarlyPhaseTicketPercentageToUnstack[2]; break;
                        default: break;
                    }
                    break;
                case Phase.Mid:
                    switch (pop)
                    {
                        case Population.Low: unstackTicketRatio = MidPhaseTicketPercentageToUnstack[0]; break;
                        case Population.Medium: unstackTicketRatio = MidPhaseTicketPercentageToUnstack[1]; break;
                        case Population.High: unstackTicketRatio = MidPhaseTicketPercentageToUnstack[2]; break;
                        default: break;
                    }
                    break;
                case Phase.Late:
                    switch (pop)
                    {
                        case Population.Low: unstackTicketRatio = LatePhaseTicketPercentageToUnstack[0]; break;
                        case Population.Medium: unstackTicketRatio = LatePhaseTicketPercentageToUnstack[1]; break;
                        case Population.High: unstackTicketRatio = LatePhaseTicketPercentageToUnstack[2]; break;
                        default: break;
                    }
                    break;
                default: break;
            }

            // apply rush adjustment
            if (IsRush() && fRushStage > 0 && fRushStage <= 5 && unstackTicketRatio > 100)
            {
                double adj = 0;
                switch (fRushStage)
                {
                    case 1: adj = perMode.Stage1TicketPercentageToUnstackAdjustment; break;
                    case 2: adj = perMode.Stage2TicketPercentageToUnstackAdjustment; break;
                    case 3: adj = perMode.Stage3TicketPercentageToUnstackAdjustment; break;
                    case 4: adj = perMode.Stage4And5TicketPercentageToUnstackAdjustment; break;
                    case 5: adj = perMode.Stage4And5TicketPercentageToUnstackAdjustment; break;
                    default: break;
                }
                if (adj != 0) unstackTicketRatio = unstackTicketRatio + adj;
            }

            if (unstackTicketRatio <= 100) unstackTicketRatio = 0;

            if (AdjustForMetro(perMode))
            {
                double old = unstackTicketRatio;
                switch (phase)
                {
                    case Phase.Early: unstackTicketRatio = 0; break;
                    // case Phase.Mid: speed = Speed.Slow; break; // use whatever is specified
                    case Phase.Late: unstackTicketRatio = 0; break;
                }
                if (old != unstackTicketRatio) ConsoleDebug("GetUnstackTicketRatio: Adjusted for Metro from " + old + " to " + unstackTicketRatio);
            }

            return (unstackTicketRatio / 100.0);
        }

        private Speed GetBalanceSpeed(PerModeSettings perMode)
        {
            Phase phase = GetPhase(perMode, true);
            Population pop = GetPopulation(perMode, true);
            Speed speed = Speed.Adaptive;

            switch (phase)
            {
                case Phase.Early:
                    switch (pop)
                    {
                        case Population.Low: speed = EarlyPhaseBalanceSpeed[0]; break;
                        case Population.Medium: speed = EarlyPhaseBalanceSpeed[1]; break;
                        case Population.High: speed = EarlyPhaseBalanceSpeed[2]; break;
                        default: break;
                    }
                    break;
                case Phase.Mid:
                    switch (pop)
                    {
                        case Population.Low: speed = MidPhaseBalanceSpeed[0]; break;
                        case Population.Medium: speed = MidPhaseBalanceSpeed[1]; break;
                        case Population.High: speed = MidPhaseBalanceSpeed[2]; break;
                        default: break;
                    }
                    break;
                case Phase.Late:
                    switch (pop)
                    {
                        case Population.Low: speed = LatePhaseBalanceSpeed[0]; break;
                        case Population.Medium: speed = LatePhaseBalanceSpeed[1]; break;
                        case Population.High: speed = LatePhaseBalanceSpeed[2]; break;
                        default: break;
                    }
                    break;
                default: break;
            }
            if (AdjustForMetro(perMode))
            {
                Speed old = speed;
                switch (phase)
                {
                    case Phase.Early: speed = Speed.Stop; break;
                    case Phase.Mid: speed = Speed.Slow; break;
                    case Phase.Late: speed = Speed.Stop; break;
                }
                if (old != speed) ConsoleDebug("GetBalanceSpeed: Adjusted for Metro from " + old + " to " + speed);
            }
            return speed;
        }

        private void SetTag(PlayerModel player, Hashtable data)
        {
            if (data == null)
            {
                player.TagFetchStatus.State = FetchState.Failed;
                player.TagVerified = true;
                ConsoleDebug("SetTag ^b" + player.Name + "^n data = null");
                return;
            }
            player.TagFetchStatus.State = FetchState.Succeeded;
            player.TagVerified = true;

            if (!data.ContainsKey("clanTag") || ((String)data["clanTag"] == null))
            {
                DebugFetch("Request clanTag(^b" + player.Name + "^n), no clanTag key in data");
                return;
            }

            player.Tag = (String)data["clanTag"];
            if (!String.IsNullOrEmpty(player.Tag)) DebugFetch("Set tag ^b" + player.Tag + "^n for ^b" + player.Name);
            UpdateFromWhitelist(player);
            UpdatePlayerFriends(player);
            if (IsInDispersalList(player, false)) DebugFetch("^b" + player.FullName + "^n in Dispersal Group " + player.DispersalGroup);
        }

        private void SetStats(PlayerModel player, Hashtable stats)
        {
            player.StatsFetchStatus.State = FetchState.Failed;
            if (stats == null)
            {
                ConsoleDebug("SetStats ^b" + player.Name + "^n stats = null");
                return;
            }

            Dictionary<String, double> propValues = new Dictionary<String, double>();
            propValues["kdRatio"] = -1;
            propValues["timePlayed"] = -1;
            propValues["kills"] = -1;
            propValues["scorePerMinute"] = -1;
            propValues["deaths"] = -1;
            propValues["rsDeaths"] = -1;
            propValues["rsKills"] = -1;
            propValues["rsScore"] = -1;
            propValues["rsTimePlayed"] = -1;

            foreach (DictionaryEntry entry in stats)
            {
                try
                {
                    if (entry.Key == null) continue;
                    String entryKey = (String)(entry.Key.ToString());

                    // skip entries we are not interested in 
                    if (!propValues.ContainsKey(entryKey)) continue;
                    if (entry.Value == null) continue;

                    String entryValue = (String)(entry.Value.ToString());

                    double dValue = -1;
                    if (!String.IsNullOrEmpty(entryValue)) Double.TryParse(entryValue, out dValue);
                    propValues[entryKey] = (Double.IsNaN(dValue)) ? -1 : dValue;
                }
                catch (Exception) { }
            }

            // Now set the player values, starting with AllTime
            double allTimeMinutes = Math.Max(1, propValues["timePlayed"] / 60);
            double kills = propValues["kills"];
            kills = (kills < 1) ? 0 : kills;
            double deaths = propValues["deaths"];
            deaths = (deaths < 1) ? 1 : deaths;
            double kdr = propValues["kdRatio"];
            if (kdr < 0)
            {
                kdr = kills / deaths;
            }

            player.KDR = kdr;
            player.SPM = propValues["scorePerMinute"];
            player.KPM = propValues["kills"] / allTimeMinutes;

            // Using Reset?
            String type = "All-Time";
            if (WhichBattlelogStats == BattlelogStats.Reset && propValues["rsTimePlayed"] > 0)
            {
                type = "Reset";
                double resetMinutes = Math.Max(1, propValues["rsTimePlayed"] / 60);
                double resetKDR = propValues["rsKills"] / Math.Max(1, propValues["rsDeaths"]);
                if (resetKDR > 0) player.KDR = resetKDR;
                double resetSPM = propValues["rsScore"] / resetMinutes;
                if (resetSPM > 0) player.SPM = resetSPM;
                double resetKPM = propValues["rsKills"] / resetMinutes;
                if (resetKPM > 0) player.KPM = resetKPM;
            }
            player.StatsFetchStatus.State = FetchState.Succeeded;
            player.StatsVerified = true;
            String msg = type + " [bKDR:" + player.KDR.ToString("F2") + ", bSPM:" + player.SPM.ToString("F0") + ", bKPM:" + player.KPM.ToString("F1") + "]";
            String ver = fGameVersion.ToString();
            DebugFetch("^4Player " + ver + " stats updated ^0^b" + player.Name + "^n, " + msg);
        }


        private void Scrambler(List<TeamScore> teamScores)
        {
            // Clear the debug lists
            try
            {
                fDebugScramblerBefore[0].Clear();
                fDebugScramblerBefore[1].Clear();
                fDebugScramblerAfter[0].Clear();
                fDebugScramblerAfter[1].Clear();
                fDebugScramblerStartRound[0].Clear();
                fDebugScramblerStartRound[1].Clear();
                lock (fExtrasLock)
                {
                    fDebugScramblerSuspects.Clear();
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }

            // Check all the reasons not to scramble
            if (fServerInfo == null)
            {
                ConsoleDebug("Scrambler: fServerInfo is null!");
                return;
            }

            PerModeSettings perMode = GetPerModeSettings();

            if (!perMode.EnableScrambler)
            {
                DebugScrambler("Enable Scrambler is False, no scramble this round");
                return;
            }

            if (OnlyByCommand && !fScrambleByCommand)
            {
                DebugScrambler("Only By Command is True and no command was issued, no scramble this round");
                return;
            }

            int current = fServerInfo.CurrentRound + 1; // zero based index
            if (!fScrambleByCommand && OnlyOnNewMaps && current < fServerInfo.TotalRounds)
            {
                DebugScrambler("Only scrambling new maps and this is only round " + current + " of " + fServerInfo.TotalRounds);
                return;
            }

            if (IsSQDM())
            {
                DebugScrambler("SQDM can't be scrambled");
                return;
            }

            int totalPlayers = TotalPlayerCount();
            int minNeeded = (perMode.EnableLowPopulationAdjustments) ? 6 : 16;
            if (!KeepSquadsTogether && !KeepClanTagsInSameTeam && !KeepFriendsInSameTeam)
            {
                DebugScrambler("All Keep settings are False, relaxing min needed requirement!");
                minNeeded = 6;
            }
            if (!fScrambleByCommand && totalPlayers < minNeeded)
            {
                DebugScrambler("Not enough players to scramble, at least " + minNeeded + " required: " + totalPlayers);
                return;
            }

            if (!IsCTF() && !IsCarrierAssault() && !IsObliteration() && !fScrambleByCommand && OnlyOnFinalTicketPercentage > 100)
            {
                if (teamScores == null || teamScores.Count < 2)
                {
                    DebugScrambler("DEBUG: no final team scores");
                    return;
                }
                bool countDown = true;

                if (fMaxTickets == -1) return;

                double goal = fMaxTickets;
                double a = (teamScores[0].Score == 1) ? 0 : teamScores[0].Score;
                double b = (teamScores[1].Score == 1) ? 0 : teamScores[1].Score;

                /*
                if (IsRush()) {
                    // normalize Rush ticket ratio
                    b = fMaxTickets - (fRushMaxTickets - b);
                    b = Math.Max(b, 1);
                }
                */

                if (IsCountUp())
                {
                    countDown = false;
                    goal = teamScores[0].WinningScore;
                }

                /*
                double ratio = 0;
                if (countDown) {
                    // ratio of difference from max
                    if (a < b) {
                        ratio = (goal - a) / Math.Max(1, (goal - b)); 
                        DebugScrambler("Ratio T1/T2: " + a + " vs " + b + " <- [" + goal + "]: " + (goal-a) + "/" + Math.Max(1, (goal-b)) + " = " + ratio.ToString("F2"));
                    } else {
                        ratio = (goal - b) / Math.Max(1, (goal - a));
                        DebugScrambler("Ratio T2/T1: " + a + " vs " + b + " <- [" + goal + "]: " + (goal-b) + "/" + Math.Max(1, (goal-a)) + " = " + ratio.ToString("F2"));
                    }
                } else {
                    // direct ratio
                    if (a > b) {
                        ratio = a / Math.Max(1, b);
                        DebugScrambler("Ratio T1/T2: " + a + " vs " + b + " -> [" + goal + "]: " + a + "/" + Math.Max(1, b) + " = " + ratio.ToString("F2"));
                    } else {
                        ratio = b / Math.Max(1, a);
                        DebugScrambler("Ratio T2/T2: " + a + " vs " + b + " -> [" + goal + "]: " + b + "/" + Math.Max(1, a) + " = " + ratio.ToString("F2"));
                    }
                }
                */

                String smsg = String.Empty;
                double ratio = ComputeTicketRatio(a, b, goal, countDown, out smsg);
                DebugScrambler(smsg);

                if ((ratio * 100) < OnlyOnFinalTicketPercentage)
                {
                    DebugScrambler("Only On Final Ticket Percentage >= " + OnlyOnFinalTicketPercentage.ToString("F0") + "%, but ratio is only " + (ratio * 100).ToString("F0") + "%, no scramble this round");
                    return;
                }
                else
                {
                    DebugScrambler("Only On Final Ticket Percentage >= " + OnlyOnFinalTicketPercentage.ToString("F0") + "% and ratio is " + (ratio * 100).ToString("F0") + "%");
                }
            }

            DebugScrambler("Scrambling teams by " + ScrambleBy + " in " + DelaySeconds.ToString("F0") + " seconds");

            Chat("all", TeamsWillBeScrambled, false);

            // Activate the scrambler thread
            lock (fScramblerLock)
            {
                fScramblerLock.MaxDelay = DelaySeconds;
                fScramblerLock.LastUpdate = DateTime.Now;
                Monitor.Pulse(fScramblerLock);
            }
        }


        private void ScrambleLoneWolves(List<PlayerModel> loneWolves, Dictionary<int, SquadRoster> squads, int whichTeam)
        {
            // Add lone wolves to empty squads
            int key = 0;
            int emptyId = 1;
            SquadRoster home = null;
            bool filling = false;
            // Do Team 1 first
            foreach (PlayerModel wolf in loneWolves)
            {
                if (wolf.Team != whichTeam)
                    continue;
                bool goback = true;
                while (goback)
                {
                    if (!filling)
                    {
                        // Need to find an empty squad
                        key = (wolf.Team * 1000) + emptyId;
                        while (squads.ContainsKey(key))
                        {
                            emptyId = emptyId + 1;
                            if (emptyId > (SQUAD_NAMES.Length - 1)) break;
                            key = (wolf.Team * 1000) + emptyId;
                        }
                        filling = true;
                    }
                    if (emptyId > (SQUAD_NAMES.Length - 1)) break;
                    if (filling)
                    {
                        // Add wolf to the squad we are filling until full
                        key = (wolf.Team * 1000) + emptyId;
                        home = AddPlayerToSquadRoster(squads, wolf, key, emptyId, false);
                        if (home == null || !home.Roster.Contains(wolf))
                        {
                            // Full
                            filling = false;
                            continue;
                        }
                        else
                        {
                            // Next wolf
                            DebugScrambler("Lone wolf ^b" + wolf.FullName + "^n filled in empty squad " + wolf.Team + "/" + emptyId);
                            goback = false;
                            continue;
                        }
                    }
                }
            }
        }

        private void ScrambleByCommand(int winner, bool logOnly)
        {
            try
            {
                fDebugScramblerBefore[0].Clear();
                fDebugScramblerBefore[1].Clear();
                fDebugScramblerAfter[0].Clear();
                fDebugScramblerAfter[1].Clear();
                fDebugScramblerStartRound[0].Clear();
                fDebugScramblerStartRound[1].Clear();
                lock (fExtrasLock)
                {
                    fDebugScramblerSuspects.Clear();
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }

            fWinner = winner;

            // Activate the scrambler thread
            lock (fScramblerLock)
            {
                fScramblerLock.MaxDelay = (logOnly) ? -1 : DelaySeconds;
                fScramblerLock.LastUpdate = DateTime.Now;
                Monitor.Pulse(fScramblerLock);
            }
        }

        private void ScramblerLoop()
        {
            /*
            Strategy: Scan each team and build filtered team and optionally squad lists.
            The ScrambleBy metric of each item in the pool is calculated. The pool is
            sorted according to the ScrambleBy setting. The best player/squad is assigned
            to the losing team, then the team total is calculated. More strong players/squads
            are added to the losing team until its metric sum is greater than the winning team,
            then players/squads are added to the winning team until it is greater, and so on.
            If at any time a team is full, the remainder of the players/squads are added to 
            the other team.

            Finally, each member of the new team is checked and if they need to be moved,
            a move command is issued.  Since this is between rounds, a special move command
            that bypasses all move tracking is used.
            */
            try
            {
                DateTime last = DateTime.MinValue;
                while (fIsEnabled)
                {
                    double delay = 0;
                    DateTime since = DateTime.MinValue;
                    bool logOnly = false;

                    fWhileScrambling = false;
                    lock (fExtrasLock)
                    {
                        fExtraNames.Clear();
                    }

                    lock (fScramblerLock)
                    {
                        while (fScramblerLock.MaxDelay == 0)
                        {
                            Monitor.Wait(fScramblerLock);
                            if (!fIsEnabled) return;
                        }
                        if (fScramblerLock.MaxDelay == -1)
                        {
                            fScramblerLock.MaxDelay = 0;
                            logOnly = true;
                        }
                        delay = fScramblerLock.MaxDelay;
                        since = fScramblerLock.LastUpdate;
                        fScramblerLock.MaxDelay = 0;
                        fScramblerLock.LastUpdate = DateTime.MinValue;
                    }

                    if (since == DateTime.MinValue) continue;

                    if (!logOnly && last != DateTime.MinValue && DateTime.Now.Subtract(last).TotalMinutes < 3)
                    {
                        DebugScrambler("^0Last scramble was less than 5 minutes ago, skipping!");
                        continue;
                    }

                    try
                    {

                        PerModeSettings perMode = GetPerModeSettings();

                        // wait specified number of seconds
                        if (delay > 0)
                        {
                            bool listUpdated = false;
                            while (DateTime.Now.Subtract(since).TotalSeconds < delay)
                            {
                                try
                                {
                                    if (!listUpdated && delay - DateTime.Now.Subtract(since).TotalSeconds <= 5)
                                    {
                                        // update the player list within 5 seconds of the delay expiring
                                        listUpdated = true;
                                        DebugScrambler("Last chance player list update, account for players who have left");
                                        ServerCommand("admin.listPlayers", "all");
                                    }
                                }
                                catch (Exception) { }
                                Thread.Sleep(1000); // 1 second
                                if (!fIsEnabled) return;
                            }
                        }

                        String extra = String.Empty;
                        if (DivideBy == DivideByChoices.ClanTag) extra = " [" + ClanTagToDivideBy + "]";
                        String kst = String.Empty;
                        if (KeepSquadsTogether) kst = ", KeepSquadsTogether";
                        String kctiss = String.Empty;
                        if (KeepClanTagsInSameTeam)
                        {
                            kctiss = ", KeepClansTagsInSameTeam";
                            if (KeepFriendsInSameTeam) kctiss = kctiss + ", KeepFriendsInSameTeam";
                        }
                        DebugScrambler("Starting scramble of " + TotalPlayerCount() + " players, winner was T" + fWinner + "(" + GetTeamName(fWinner) + ")");
                        DebugScrambler("Using (" + ScrambleBy + kst + kctiss + ", DivideBy = " + DivideBy + extra + ")");
                        if (!logOnly) last = DateTime.Now;

                        // Build a filtered list
                        List<String> toScramble = new List<String>();
                        //List<String> exempt = new List<String>();
                        PlayerModel player = null;

                        lock (fAllPlayers)
                        {
                            foreach (String egg in fAllPlayers)
                            {
                                try
                                {
                                    player = GetPlayer(egg);
                                    if (player == null) continue;

                                    // For debugging
                                    if (player.Team > 0 && player.Team <= 2)
                                    {
                                        fDebugScramblerBefore[player.Team - 1].Add(player.ClonePlayer());
                                    }
                                    else continue; // skip joining players

                                    // Add this player to list of scramblers
                                    toScramble.Add(egg);
                                }
                                catch (Exception e)
                                {
                                    if (DebugLevel >= 8) ConsoleException(e);
                                }
                            }

                            // Now that we have captured our master list, handle new joins with care
                            if (toScramble.Count > 0 && !logOnly) fWhileScrambling = true;
                        }

                        if (toScramble.Count == 0) continue;

                        // Build squad tables, clan tables and overall list
                        List<SquadRoster> all = new List<SquadRoster>();
                        List<PlayerModel> usHaveNoSquad = new List<PlayerModel>();
                        List<PlayerModel> ruHaveNoSquad = new List<PlayerModel>();
                        List<SquadRoster> usSquadOfOne = new List<SquadRoster>();
                        List<SquadRoster> ruSquadOfOne = new List<SquadRoster>();
                        Dictionary<int, SquadRoster> squads = new Dictionary<int, SquadRoster>(); // key int is (team * 1000) + squad
                        List<PlayerModel> loneWolves = new List<PlayerModel>();
                        int key = 0;
                        String debugMsg = String.Empty;

                        foreach (String egg in toScramble)
                        {
                            try
                            {
                                if (!IsKnownPlayer(egg)) continue; // might have left while we were working
                                player = GetPlayer(egg);
                                if (player == null) continue;
                                if (player.Team < 1) continue; // skip players that are still joining
                                PlayerModel clone = player.ClonePlayer(); // from now on, use a clone
                                if (clone.Squad < 1)
                                {
                                    if (clone.Squad == 0)
                                    {
                                        if (clone.Team == 1) { usHaveNoSquad.Add(clone); }
                                        else if (clone.Team == 2) { ruHaveNoSquad.Add(clone); }
                                    }
                                    continue; // skip players not in a squad
                                }
                                key = 9000; // free pool
                                int squadId = clone.Squad;
                                if (KeepSquadsTogether)
                                {
                                    key = (Math.Max(0, clone.Team) * 1000) + Math.Max(0, clone.Squad);
                                    if (key < 1000)
                                    {
                                        loneWolves.Add(clone);
                                        continue;
                                    }
                                    else
                                    {
                                        DebugScrambler("Keeping ^b" + clone.FullName + "^n together with squad, using key " + key);
                                    }
                                    AddPlayerToSquadRoster(squads, clone, key, squadId, true);
                                }
                                else if (KeepClanTagsInSameTeam)
                                {
                                    String tt = ExtractTag(clone);
                                    if (tt == null) tt = String.Empty;
                                    int numInSquad = CountMatchingTags(clone, Scope.SameSquad);
                                    // Keep players with same clan tag in the same team
                                    //if (numInSquad >= 2) {
                                    key = (Math.Max(0, clone.Team) * 1000) + Math.Max(0, clone.Squad); // 0 is okay, makes lone-wolf pool
                                    if (String.IsNullOrEmpty(tt) || key < 1000)
                                    {
                                        loneWolves.Add(clone);
                                        continue;
                                    }
                                    else if (numInSquad >= 2)
                                    {
                                        DebugScrambler("Keeping ^b" + clone.Name + "^n together with " + numInSquad + " tags [" + tt + "] with squad, using key " + key);
                                    }
                                    /*
                                    } else {
                                        loneWolves.Add(clone);
                                        continue;
                                    */
                                    //}
                                    AddPlayerToSquadRoster(squads, clone, key, squadId, true);
                                }
                                else if (CheckWhitelist(clone, WL_BALANCE))
                                { // Leave Whitelisted players in same team and squad
                                    key = (Math.Max(0, clone.Team) * 1000) + Math.Max(0, clone.Squad); // 0 is okay, makes lone-wolf pool
                                    DebugScrambler("Keeping whitelisted ^b" + clone.FullName + "^n in same team and squad, using key " + key);

                                    SquadRoster tsr = AddPlayerToSquadRoster(squads, clone, key, squadId, true);
                                    if (tsr != null)
                                    {
                                        tsr.WhitelistCount = tsr.WhitelistCount + 1;
                                    }
                                }
                                else
                                {
                                    loneWolves.Add(clone);
                                }
                            }
                            catch (Exception e)
                            {
                                if (DebugLevel >= 8) ConsoleException(e);
                            }
                        }

                        // Add lone wolves to empty squads
                        ScrambleLoneWolves(loneWolves, squads, 1);
                        ScrambleLoneWolves(loneWolves, squads, 2);
                        /*
                        bool filling = false;
                        // Do Team 1 first
                        foreach (PlayerModel wolf in loneWolves) {
                            if (wolf.Team != 1)
                                continue;
                            bool goback = true;
                            while (goback) {
                                if (!filling) {
                                    // Need to find an empty squad
                                    key = (wolf.Team * 1000) + emptyId;
                                    while (squads.ContainsKey(key)) {
                                        emptyId = emptyId + 1;
                                        if (emptyId > (SQUAD_NAMES.Length - 1)) break;
                                        key = (wolf.Team * 1000) + emptyId;
                                    }
                                    filling = true;
                                }
                                if (emptyId > (SQUAD_NAMES.Length - 1)) break;
                                if (filling) {
                                    // Add wolf to the squad we are filling until full
                                    key = (wolf.Team * 1000) + emptyId;
                                    home = AddPlayerToSquadRoster(squads, wolf, key, emptyId, false);
                                    if (home == null || !home.Roster.Contains(wolf)) {
                                        // Full
                                        filling = false;
                                        continue;
                                    } else {
                                        // Next wolf
                                        DebugScrambler("Lone wolf ^b" + wolf.Name + "^n filled in empty squad " + wolf.Team + "/" + emptyId);
                                        goback = false;
                                        continue;
                                    }
                                }
                            }
                        }
                        */

                        // Sum up the metric for each squad
                        foreach (int k in squads.Keys)
                        {
                            SquadRoster sr = squads[k];
                            if (sr.Roster.Count == 1)
                            {
                                if (sr.Roster[0].Team == 1) { usSquadOfOne.Add(sr); }
                                else if (sr.Roster[0].Team == 2) { ruSquadOfOne.Add(sr); }
                            }
                            switch (ScrambleBy)
                            {
                                case DefineStrong.RoundScore:
                                    foreach (PlayerModel p in sr.Roster)
                                    {
                                        sr.Metric = sr.Metric + p.ScoreRound;
                                    }
                                    break;
                                case DefineStrong.RoundSPM:
                                    foreach (PlayerModel p in sr.Roster)
                                    {
                                        sr.Metric = sr.Metric + p.SPMRound;
                                    }
                                    break;
                                case DefineStrong.RoundKills:
                                    foreach (PlayerModel p in sr.Roster)
                                    {
                                        sr.Metric = sr.Metric + p.KillsRound;
                                    }
                                    break;
                                case DefineStrong.RoundKDR:
                                    foreach (PlayerModel p in sr.Roster)
                                    {
                                        sr.Metric = sr.Metric + p.KDRRound;
                                    }
                                    break;
                                case DefineStrong.PlayerRank:
                                    foreach (PlayerModel p in sr.Roster)
                                    {
                                        sr.Metric = sr.Metric + p.Rank;
                                    }
                                    break;
                                case DefineStrong.RoundKPM:
                                    foreach (PlayerModel p in sr.Roster)
                                    {
                                        sr.Metric = sr.Metric + p.KPMRound;
                                    }
                                    break;
                                case DefineStrong.BattlelogSPM:
                                    foreach (PlayerModel p in sr.Roster)
                                    {
                                        sr.Metric = sr.Metric + ((p.StatsVerified) ? p.SPM : p.SPMRound);
                                    }
                                    break;
                                case DefineStrong.BattlelogKDR:
                                    foreach (PlayerModel p in sr.Roster)
                                    {
                                        sr.Metric = sr.Metric + ((p.StatsVerified) ? p.KDR : p.KDRRound);
                                    }
                                    break;
                                case DefineStrong.BattlelogKPM:
                                    foreach (PlayerModel p in sr.Roster)
                                    {
                                        sr.Metric = sr.Metric + ((p.StatsVerified) ? p.KPM : p.KPMRound);
                                    }
                                    break;
                                default:
                                    foreach (PlayerModel p in sr.Roster)
                                    {
                                        sr.Metric = sr.Metric + p.ScoreRound;
                                    }
                                    break;
                            }

                            String ot = (sr.Roster[0].Team == 1) ? "T1" : "T2";
                            DebugScrambler(ot + "/" + GetSquadName(sr.Squad) + "(" + sr.Roster.Count + ") " + ScrambleBy + ":" + sr.Metric.ToString("F1"));

                            switch (DivideBy)
                            {
                                case DivideByChoices.ClanTag:
                                    foreach (PlayerModel p in sr.Roster)
                                    {
                                        if (!String.IsNullOrEmpty(ClanTagToDivideBy) && ExtractTag(p) == ClanTagToDivideBy) sr.ClanTagCount = sr.ClanTagCount + 1;
                                    }
                                    debugMsg = "ClanTag[" + ClanTagToDivideBy + "] " + sr.ClanTagCount;
                                    break;
                                case DivideByChoices.DispersalGroup:
                                    {
                                        int[] gCount = new int[3] { 0, 0, 0 };
                                        foreach (PlayerModel p in sr.Roster)
                                        {
                                            if (IsInDispersalList(p, true))
                                            {
                                                if (p.DispersalGroup == 1 || p.DispersalGroup == 2)
                                                {
                                                    gCount[p.DispersalGroup] = gCount[p.DispersalGroup] + 1;
                                                }
                                            }
                                        }
                                        if (gCount[1] != 0 || gCount[2] != 0)
                                        {
                                            sr.DispersalGroup = (gCount[1] > gCount[2]) ? 1 : 2;
                                        }
                                        debugMsg = "Dispersal Group = " + sr.DispersalGroup;
                                        break;
                                    }
                                case DivideByChoices.None:
                                default:
                                    break;
                            }

                            if (DivideBy != DivideByChoices.None) DebugScrambler("Divide " + ot + "/" + GetSquadName(sr.Squad) + " by " + debugMsg);

                            all.Add(sr);
                        }
                        squads.Clear();

                        if (all.Count == 0) continue;

                        // Sort squads
                        all.Sort(DescendingMetricSquad);

                        DebugScrambler("After sorting:");
                        foreach (SquadRoster ds in all)
                        {
                            String oldt = (ds.Roster[0].Team == 1) ? "T1" : "T2";
                            DebugScrambler("    " + ScrambleBy + ":" + ds.Metric.ToString("F1") + " " + oldt + "/" + GetSquadName(ds.Squad));
                        }

                        // Prepare the new team lists
                        List<PlayerModel> usScrambled = new List<PlayerModel>();
                        Dictionary<int, SquadRoster> usSquads = new Dictionary<int, SquadRoster>();
                        double usMetric = 0;
                        List<PlayerModel> ruScrambled = new List<PlayerModel>();
                        Dictionary<int, SquadRoster> ruSquads = new Dictionary<int, SquadRoster>();
                        double ruMetric = 0;

                        // Dole out squads, keeping metric in balance, starting with the losing team
                        List<PlayerModel> target = (fWinner == 0 || fWinner == 1) ? ruScrambled : usScrambled;
                        Dictionary<int, SquadRoster> targetSquadTable = (fWinner == 0 || fWinner == 1) ? ruSquads : usSquads;
                        int teamMax = MaximumServerSize / 2;
                        debugMsg = String.Empty;

                        // Pre-process DivideBy setting
                        if (DivideBy == DivideByChoices.DispersalGroup)
                        {
                            // Skim the dispersal squads off the top
                            List<PlayerModel> localTarget = null;
                            List<SquadRoster> copy = new List<SquadRoster>(all);
                            foreach (SquadRoster disp in copy)
                            {
                                if (disp.DispersalGroup == 1 && usScrambled.Count < teamMax)
                                {
                                    localTarget = usScrambled;
                                    debugMsg = "T1 (" + GetTeamName(1) + ")";
                                }
                                else if (disp.DispersalGroup == 2 && ruScrambled.Count < teamMax)
                                {
                                    localTarget = ruScrambled;
                                    debugMsg = "T2 (" + GetTeamName(2) + ")";
                                }
                                else
                                {
                                    continue;
                                }
                                DebugScrambler("Squad " + GetSquadName(disp.Squad) + ", Dispersal Group " + disp.DispersalGroup + " to " + debugMsg + " team");
                                AssignSquadToTeam(disp, targetSquadTable, usScrambled, ruScrambled, localTarget);
                                all.Remove(disp);
                            }
                            if (usScrambled == target && target.Count >= teamMax) target = ruScrambled;
                            if (ruScrambled == target && target.Count >= teamMax) target = usScrambled;
                        }

                        SquadRoster squad = (all.Count > 0) ? all[0] : null;
                        List<PlayerModel> opposing = null;
                        Dictionary<int, SquadRoster> opposingSquadTable = null;
                        do
                        {
                            if (squad == null) break;

                            all.Remove(squad);

                            AssignSquadToTeam(squad, targetSquadTable, usScrambled, ruScrambled, target);

                            // Recalc team metrics
                            SumMetricByTeam(usScrambled, ruScrambled, out usMetric, out ruMetric);
                            if (logOnly || DebugLevel >= 6) DebugScrambler("Updated scrambler metrics " + ScrambleBy + ": T1(" + usScrambled.Count + ") = " + usMetric.ToString("F1") + ", T2(" + ruScrambled.Count + ") = " + ruMetric.ToString("F1"));

                            if (usScrambled.Count >= teamMax && ruScrambled.Count >= teamMax)
                            {
                                all.Clear(); // no more room, skip remaining squads
                                break;
                            }

                            if (all.Count == 0) break;

                            // Choose new target team based on metrics
                            if (usScrambled.Count >= teamMax && ruScrambled.Count < teamMax)
                            {
                                target = ruScrambled;
                                targetSquadTable = ruSquads;
                                opposing = usScrambled;
                                squad = all[0];
                                continue; // skip additional checks, no other choice
                            }
                            else if (ruScrambled.Count >= teamMax && usScrambled.Count < teamMax)
                            {
                                target = usScrambled;
                                targetSquadTable = usSquads;
                                opposing = ruScrambled;
                                squad = all[0];
                                continue; // skip additional checks, no other choice
                            }
                            else if (usMetric < ruMetric)
                            {
                                target = usScrambled;
                                targetSquadTable = usSquads;
                                opposing = ruScrambled;
                                debugMsg = "Scrambling to target = T1 (" + GetTeamName(1) + ")";
                            }
                            else
                            {
                                target = ruScrambled;
                                targetSquadTable = ruSquads;
                                opposing = usScrambled;
                                debugMsg = "Scrambling to target = T2 (" + GetTeamName(2) + ")";
                            }

                            // Override choice if teams would be too unbalanced by player count
                            if (target.Count > opposing.Count)
                            {
                                // Take a weak squad from the end of the list instead
                                squad = all[all.Count - 1];
                                // assign to the opposing team
                                List<PlayerModel> tmp = target;
                                target = opposing;
                                opposing = tmp;
                                if (target == usScrambled)
                                {
                                    targetSquadTable = usSquads;
                                    debugMsg = "^4REVISED for count target = T1 (" + GetTeamName(1) + ")";
                                }
                                else
                                {
                                    targetSquadTable = ruSquads;
                                    debugMsg = "^4REVISED for count target = T2 (" + GetTeamName(2) + ")";
                                }
                            }
                            else
                            {
                                squad = all[0]; // use strongest squad
                            }

                            if (logOnly || DebugLevel >= 6)
                            {
                                DebugScrambler(" ");
                                DebugScrambler(debugMsg + ", squad " + GetSquadName(squad.Squad) + " (" + squad.Roster.Count + ")");
                            }

                        } while (all.Count > 0);

                        if (!fIsEnabled) return;

                        // Make sure player counts aren't too out of balance
                        if (usScrambled.Count <= teamMax && ruScrambled.Count <= teamMax && Math.Abs(usScrambled.Count - ruScrambled.Count) > 1)
                        {
                            int needed = Math.Abs(usScrambled.Count - ruScrambled.Count) / 2;
                            int toTeamId = 0;
                            int targetDispersalGroup = 0;
                            List<PlayerModel> opposingCopy = new List<PlayerModel>();
                            List<PlayerModel> tmpCopy = new List<PlayerModel>();
                            List<PlayerModel> oppHaveNoSquad = null;
                            List<SquadRoster> oppSquadOfOne = null;

                            if (usScrambled.Count < ruScrambled.Count)
                            {
                                target = usScrambled;
                                targetSquadTable = usSquads;
                                targetDispersalGroup = 1;
                                toTeamId = 1;
                                opposing = ruScrambled;
                                opposingSquadTable = ruSquads;
                                oppHaveNoSquad = ruHaveNoSquad;
                                oppSquadOfOne = ruSquadOfOne;
                                debugMsg = "T1 (" + GetTeamName(1) + ") needs " + needed + " more players";
                            }
                            else
                            {
                                target = ruScrambled;
                                targetSquadTable = ruSquads;
                                targetDispersalGroup = 2;
                                toTeamId = 2;
                                opposing = usScrambled;
                                opposingSquadTable = usSquads;
                                oppHaveNoSquad = usHaveNoSquad;
                                oppSquadOfOne = usSquadOfOne;
                                debugMsg = "T2 (" + GetTeamName(2) + ") needs " + needed + " more players";
                            }

                            DebugScrambler("Adjusting team sizes, T1(" + usScrambled.Count + "/" + fTeam1.Count + ") vs T2(" + ruScrambled.Count + "/" + fTeam2.Count + ") " + debugMsg);

                            // See if we have some new players that joined after we started scrambling
                            List<String> extras = null;
                            lock (fExtrasLock)
                            {
                                if (fExtraNames.Count > 0)
                                {
                                    extras = new List<String>();
                                    extras.AddRange(fExtraNames);
                                }
                            }
                            if (extras != null)
                            {
                                foreach (String ename in extras)
                                {
                                    try
                                    {
                                        PlayerModel xtra = GetPlayer(ename);
                                        if (xtra == null) continue;
                                        SquadRoster sr = null;
                                        if (targetSquadTable.TryGetValue(xtra.Squad, out sr))
                                        {
                                            if (sr.Roster.Count >= fMaxSquadSize) continue;
                                            sr.Roster.Add(xtra);
                                        }
                                        else
                                        {
                                            sr = new SquadRoster(xtra.Squad);
                                            sr.Roster.Add(xtra);
                                            targetSquadTable[xtra.Squad] = sr;
                                        }
                                        DebugScrambler("Adding new joining player ^b" + xtra.FullName + "^n to " + GetTeamName(toTeamId) + " team");
                                        target.Add(xtra);
                                        lock (fExtrasLock)
                                        {
                                            if (fExtraNames.Contains(ename)) fExtraNames.Remove(ename);
                                        }
                                        --needed;
                                        if (needed == 0) break;
                                    }
                                    catch (Exception e)
                                    {
                                        ConsoleException(e);
                                    }
                                }
                            }

                            // Rearrange opposing team scrambled list so that squad-of-one and have-no-squad players come first
                            tmpCopy.AddRange(opposing);
                            foreach (SquadRoster monoSquad in oppSquadOfOne)
                            {
                                PlayerModel op = monoSquad.Roster[0];
                                opposingCopy.Add(op);
                                tmpCopy.Remove(op);
                            }
                            oppSquadOfOne.Clear();
                            foreach (PlayerModel op in oppHaveNoSquad)
                            {
                                opposingCopy.Add(op);
                                tmpCopy.Remove(op);
                            }
                            oppHaveNoSquad.Clear();
                            // Since team list is sorted, take from the weak end of the team
                            for (int j = tmpCopy.Count - 1; j >= 0; --j)
                            {
                                opposingCopy.Add(tmpCopy[j]);
                            }
                            tmpCopy.Clear();

                            // Move players from opposing team to target team until counts are in balance
                            while (opposing.Count > 0 && (opposing.Count - target.Count) > 1)
                            {
                                PlayerModel filler = null;

                                // Loop through the rearranged copy of opposing team to find a filler player to move to the target team
                                // We use a copy since the original list has to be modified
                                foreach (PlayerModel f in opposingCopy)
                                {
                                    if (f == null) break;
                                    filler = f;

                                    // Check to make sure Dispersal isn't violated
                                    if (DivideBy == DivideByChoices.DispersalGroup && IsInDispersalList(filler, true) && filler.DispersalGroup != targetDispersalGroup)
                                    {
                                        filler = null;
                                        continue;
                                    }

                                    // Make sure player doesn't have clan tag being divided
                                    String ft = ExtractTag(filler);
                                    if (ft == null) ft = String.Empty;
                                    if (DivideBy == DivideByChoices.ClanTag && ft == ClanTagToDivideBy)
                                    {
                                        filler = null;
                                        continue;
                                    }

                                    // Make sure squad filler is coming from doesn't have clan tags to keep together
                                    int cmt = 0;
                                    SquadRoster fillerSquad = null;
                                    if ((KeepClanTagsInSameTeam || KeepSquadsTogether) && filler.Squad > 0 && opposingSquadTable.TryGetValue(filler.Squad, out fillerSquad) && fillerSquad != null)
                                    {
                                        foreach (PlayerModel mate in fillerSquad.Roster)
                                        {
                                            if (ft == ExtractTag(mate)) ++cmt;
                                        }

                                        int required = (KeepClanTagsInSameTeam) ? 1 : 2;

                                        if (cmt >= required)
                                        {
                                            filler = null;
                                            continue;
                                        }

                                        // TBD same check for friends if KeepFriendsInSameTeam is true
                                    }

                                    // Make sure player isn't whitelisted
                                    if (CheckWhitelist(filler, WL_BALANCE))
                                    {
                                        filler = null;
                                        continue;
                                    }

                                    // Otherwise, our candidate filler player is the one to go
                                    try
                                    {
                                        int formerSquad = filler.Squad;
                                        AssignFillerToTeam(filler, toTeamId, target, targetSquadTable);
                                        opposing.Remove(filler);
                                        SquadRoster fromSquad = null;
                                        if (formerSquad > 0 && opposingSquadTable.TryGetValue(formerSquad, out fromSquad) && fromSquad != null)
                                        {
                                            fromSquad.Roster.Remove(filler);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        ConsoleException(e);
                                    }

                                    // That's one down, how may more to go? Check in the outer while loop
                                    break;
                                }

                                // Check to make sure we found a filler
                                if (filler == null)
                                {
                                    DebugScrambler("^8Unable to balance teams for player count, giving up!");
                                    break;
                                }
                                else
                                {
                                    opposingCopy.Remove(filler);
                                }
                            }
                        }

                        // Final counts
                        DebugScrambler("Final scrambled team counts: T1(" + usScrambled.Count + "), T2(" + ruScrambled.Count + ")");

                        // Assert that everyone is in their proper team
                        foreach (PlayerModel clone in usScrambled)
                        {
                            if (clone.Team != 1)
                            {
                                ConsoleDebug("WARNING: ^b" + clone.FullName + "^n was in T" + clone.Team + "(" + GetTeamName(clone.Team) + "), correcting to T1");
                                clone.Team = 1;
                            }
                        }
                        foreach (PlayerModel clone in ruScrambled)
                        {
                            if (clone.Team != 2)
                            {
                                ConsoleDebug("WARNING: ^b" + clone.FullName + "^n was in T" + clone.Team + "(" + GetTeamName(clone.Team) + "), correcting to T2");
                                clone.Team = 2;
                            }
                        }

                        if (!fIsEnabled) return;

                        // Remember original squads
                        foreach (PlayerModel clone in usScrambled)
                        {
                            if (clone.ScrambledSquad == -1) clone.ScrambledSquad = clone.Squad;
                            if (clone.OriginalSquad == -1) clone.OriginalSquad = clone.Squad;
                        }
                        foreach (PlayerModel clone in ruScrambled)
                        {
                            if (clone.ScrambledSquad == -1) clone.ScrambledSquad = clone.Squad;
                            if (clone.OriginalSquad == -1) clone.OriginalSquad = clone.Squad;
                        }

                        // Using live PlayerModels, move players into squad 0 of their unscrambled teams 
                        // to avoid movement order overflows of squad size
                        List<String> unsquaded = new List<String>();
                        UnsquadMove(usSquads, ruSquads, logOnly, unsquaded); // uses live players, not clones!

                        // Pause 2 seconds to let game server catch up
                        DebugScrambler("Pause 2 seconds to let game server catch up");
                        Thread.Sleep(2 * 1000);

                        // Swap players if they have the same clan tag
                        if (!KeepSquadsTogether && KeepClanTagsInSameTeam)
                        {
                            if (DebugLevel >= 7)
                            {
                                DebugScrambler("BEFORE SWAPS");
                                ListSideBySide(usScrambled, ruScrambled, true, true);
                            }

                            SwapSameClanTags(ref usScrambled, ref ruScrambled);

                            if (DebugLevel >= 7)
                            {
                                DebugScrambler("AFTER SWAPS");
                                ListSideBySide(usScrambled, ruScrambled, true, true);
                            }
                        }

                        // Assert that no squad has more than fMaxSquadSize players
                        Dictionary<int, int> playerCount = new Dictionary<int, int>();
                        foreach (PlayerModel clone in usScrambled)
                        {
                            int num = 0;
                            if (clone.ScrambledSquad < 1 || clone.ScrambledSquad >= SQUAD_NAMES.Length)
                            {
                                ConsoleDebug("ASSERT: After unsquading T1, ^b" + clone.FullName + "^n has invalid ScrambledSquad = " + clone.ScrambledSquad);
                                continue;
                            }
                            clone.Squad = 0; // unsquad
                            if (playerCount.TryGetValue(clone.ScrambledSquad, out num))
                            {
                                num = num + 1;
                            }
                            playerCount[clone.Squad] = num;
                        }
                        foreach (int squadId in playerCount.Keys)
                        {
                            if (playerCount[squadId] > fMaxSquadSize)
                            {
                                ConsoleDebug("ASSERT: T1/" + GetSquadName(squadId) + " has > " + fMaxSquadSize + " players! = " + playerCount[squadId]);
                            }
                        }
                        playerCount.Clear();
                        foreach (PlayerModel clone in ruScrambled)
                        {
                            int num = 0;
                            if (clone.ScrambledSquad < 1 || clone.ScrambledSquad >= SQUAD_NAMES.Length)
                            {
                                ConsoleDebug("ASSERT: After unsquading T2, ^b" + clone.FullName + "^n has invalid ScrambledSquad = " + clone.ScrambledSquad);
                                continue;
                            }
                            clone.Squad = 0; // unsquad
                            if (playerCount.TryGetValue(clone.ScrambledSquad, out num))
                            {
                                num = num + 1;
                            }
                            playerCount[clone.Squad] = num;
                        }
                        foreach (int squadId in playerCount.Keys)
                        {
                            if (playerCount[squadId] > fMaxSquadSize)
                            {
                                ConsoleDebug("ASSERT: T2/" + GetSquadName(squadId) + " has > " + fMaxSquadSize + " players! = " + playerCount[squadId]);
                            }
                        }
                        playerCount.Clear();

                        // Now run through each cloned list and move any players that need moving
                        DebugScrambler("STARTING SCRAMBLE MOVES");
                        ScrambleStatus check = ScrambleTeams(usScrambled, ruScrambled, logOnly);
                        DebugScrambler("FINISHED SCRAMBLE MOVES");
                        switch (check)
                        {
                            case ScrambleStatus.CompletelyFull:
                                DebugScrambler("SERVER IS COMPLETELY FULL! No scrambling is possible.");
                                break;
                            case ScrambleStatus.Failure:
                                DebugScrambler("UNABLE TO SCRAMBLE, no room to move!");
                                break;
                            case ScrambleStatus.PartialSuccess:
                                DebugScrambler("SCRAMBLE ABORTED! Some moves completed, some failed!");
                                break;
                            case ScrambleStatus.Success:
                            default:
                                break;
                        }

                        ScheduleListPlayers(1); // refresh

                        // For debugging
                        foreach (PlayerModel clone in usScrambled)
                        {
                            if (!IsKnownPlayer(clone.Name)) continue;
                            fDebugScramblerAfter[0].Add(clone);
                        }
                        foreach (PlayerModel clone in ruScrambled)
                        {
                            if (!IsKnownPlayer(clone.Name)) continue;
                            fDebugScramblerAfter[1].Add(clone);
                        }

                        DebugScrambler("DONE!");
                        //if (logOnly || DebugLevel >= 6) CommandToLog("scrambled");
                    }
                    catch (Exception e)
                    {
                        ConsoleException(e);
                    }
                }
            }
            catch (ThreadAbortException)
            {
                fAborted = true;
                return;
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
            finally
            {
                fWhileScrambling = false;
                if (!fAborted) ConsoleWrite("^bScramblerLoop^n thread stopped", 6);
            }
        }


        private void AssignSquadToTeam(SquadRoster squad, Dictionary<int, SquadRoster> squadTable, List<PlayerModel> usScrambled, List<PlayerModel> ruScrambled, List<PlayerModel> origTarget)
        {
            /*
            The PlayerModel object is still live, so we can't change managed properties like Team or Squad.
            Instead, the assigned team is implied by the list (usScrambled or ruScrambled) the player is added to
            and the squad is remembered in the ScrambledSquad property. This is later used during the move
            command to assign the player to that squad in the destination team.
            */
            List<PlayerModel> target = origTarget;
            int teamMax = MaximumServerSize / 2;

            if (usScrambled.Count >= teamMax && ruScrambled.Count >= teamMax)
            {
                DebugScrambler("BOTH teams full! Skipping remaining free pool!");
                return;
            }
            int wasSquad = squad.Roster[0].Squad;

            // Remap if there is a squad collision
            if (squadTable.ContainsKey(squad.Squad))
            {
                RemapSquad(squadTable, squad);
            }
            squadTable[squad.Squad] = squad;
            int wasTeam = squad.Roster[0].Team;

            String special = String.Empty;
            if (squad.WhitelistCount > 0 && (wasTeam == 1 || wasTeam == 2))
            {
                target = (wasTeam == 1) ? usScrambled : ruScrambled;
                special = " (" + squad.WhitelistCount + " on Whitelist)";
            }

            String st = GetTeamName(wasTeam);
            String gt = GetTeamName((target == usScrambled) ? 1 : 2);
            DebugScrambler(st + "/" + GetSquadName(wasSquad) + " scrambled to " + gt + "/" + GetSquadName(squad.Squad) + special);

            // Assign the squad to the target team
            int toTeam = (target == usScrambled) ? 1 : 2;
            foreach (PlayerModel clone in squad.Roster)
            {
                clone.ScrambledSquad = squad.Squad;
                if (target.Count < teamMax && IsKnownPlayer(clone.Name))
                {
                    clone.Team = toTeam;
                    target.Add(clone);
                }
            }
        }



        private void SwapSameClanTags(ref List<PlayerModel> usScrambled, ref List<PlayerModel> ruScrambled)
        {
            /*
            Since all players have been moved to squad 0 at this point, only need to swap PlayerModel items
            between the two scramble lists. No actual moving is required.
            */
            DebugScrambler(" ");
            if (KeepFriendsInSameTeam)
            {
                DebugScrambler("Keeping clan tags and friends to same team");
            }
            else
            {
                DebugScrambler("Keeping clan tags to same team");
            }
            try
            {
                PerModeSettings perMode = GetPerModeSettings();
                String usName = GetTeamName(1);
                String ruName = GetTeamName(2);

                Dictionary<String, int[]> matesDistribution = new Dictionary<String, int[]>();

                // Calculate distribution between the two teams
                foreach (PlayerModel clone in usScrambled)
                {
                    String tagOrFriendex = ExtractTagOrFriendex(clone);
                    if (String.IsNullOrEmpty(tagOrFriendex)) continue;
                    int[] teamCounts = null;
                    if (matesDistribution.TryGetValue(tagOrFriendex, out teamCounts) && teamCounts != null)
                    {
                        teamCounts[1] = teamCounts[1] + 1;
                    }
                    else
                    {
                        teamCounts = new int[3] { 0, 0, 0 };
                        teamCounts[1] = teamCounts[1] + 1;
                        matesDistribution[tagOrFriendex] = teamCounts;
                    }
                }
                foreach (PlayerModel clone in ruScrambled)
                {
                    String tagOrFriendex = ExtractTagOrFriendex(clone);
                    if (String.IsNullOrEmpty(tagOrFriendex)) continue;
                    int[] teamCounts = null;
                    if (matesDistribution.TryGetValue(tagOrFriendex, out teamCounts) && teamCounts != null)
                    {
                        teamCounts[2] = teamCounts[2] + 1;
                    }
                    else
                    {
                        teamCounts = new int[3] { 0, 0, 0 };
                        teamCounts[2] = teamCounts[2] + 1;
                        matesDistribution[tagOrFriendex] = teamCounts;
                    }
                }

                // Find split tag counts
                List<String> splitTagsOrFriends = new List<String>();
                foreach (String id in matesDistribution.Keys)
                {
                    if (matesDistribution[id][1] == 0) continue;
                    if (matesDistribution[id][2] == 0) continue;
                    // Split!
                    DebugScrambler("Identifier ^b[" + id + "]^n is split: " + matesDistribution[id][1] + "/" + usName + " vs " + matesDistribution[id][2] + "/" + ruName);
                    splitTagsOrFriends.Add(id);
                }
                if (splitTagsOrFriends.Count == 0)
                {
                    if (KeepFriendsInSameTeam)
                    {
                        DebugScrambler("No clan tags or friends were split");
                    }
                    else
                    {
                        DebugScrambler("No clan tags were split");
                    }
                    return;
                }

                // Build squad table
                Dictionary<int, SquadRoster> squads = new Dictionary<int, SquadRoster>();
                foreach (PlayerModel us in usScrambled)
                {
                    int key = (1 * 1000) + us.ScrambledSquad;
                    AddPlayerToSquadRoster(squads, us, key, us.ScrambledSquad, false);
                }
                foreach (PlayerModel ru in ruScrambled)
                {
                    int key = (2 * 1000) + ru.ScrambledSquad;
                    AddPlayerToSquadRoster(squads, ru, key, ru.ScrambledSquad, false);
                }

                // Swap to maintain squad sizes and team sizes
                int target = 0;
                int opposing = 0;
                List<PlayerModel> targetList = null;
                List<PlayerModel> opposingList = null;
                bool allOk = true;

                foreach (String splitId in splitTagsOrFriends)
                {
                    try
                    {
                        DebugScrambler("Working on identifier [^b" + splitId + "^n]");
                        // Target team is the one with the majority
                        if (matesDistribution[splitId][1] > matesDistribution[splitId][2])
                        {
                            target = 1;
                            opposing = 2;
                            targetList = usScrambled;
                            opposingList = ruScrambled;
                        }
                        else
                        {
                            target = 2;
                            opposing = 1;
                            targetList = ruScrambled;
                            opposingList = usScrambled;
                        }
                        DebugScrambler("Target team is " + GetTeamName(target) + " with " + matesDistribution[splitId][target] + ", opposing team is " + GetTeamName(opposing) + " with " + matesDistribution[splitId][opposing]);
                        // List all squads that have this clan tag or friendex
                        List<int> clan = GetSquadsWithClanTagOrFriendex(splitId, squads);
                        // List players that need to move
                        List<PlayerModel> minority = new List<PlayerModel>();
                        List<PlayerModel> replacements = new List<PlayerModel>();
                        foreach (int key in clan)
                        {
                            if ((key / 1000) != target)
                            { // squad containing minority clan member from opposing team
                                foreach (PlayerModel mate in squads[key].Roster)
                                {
                                    String mId = ExtractTagOrFriendex(mate);
                                    if (mId == splitId && !minority.Contains(mate)) minority.Add(mate);
                                }
                            }
                        }
                        if (minority.Count == 0)
                        {
                            DebugScrambler("ASSERT: No minority clan members for [" + splitId + "]");
                            return;
                        }
                        // Need a list of replacements from the target team to swap, try non-clan members from target squads first
                        foreach (int key in clan)
                        {
                            if ((key / 1000) == target)
                            { // squad containing majority clan members from target team
                                foreach (PlayerModel rep in squads[key].Roster)
                                {
                                    String rId = ExtractTagOrFriendex(rep);
                                    if (String.IsNullOrEmpty(rId) && !replacements.Contains(rep))
                                    {
                                        replacements.Add(rep);
                                    }
                                    if (replacements.Count == minority.Count) break;
                                }
                            }
                        }
                        // Might not be any room in target squads, so pick non-tagged extras from end of sorted list
                        if (replacements.Count < minority.Count && targetList.Count > 0)
                        {
                            // start at the bottom of the sorted list and go up in metric
                            for (int x = (targetList.Count - 1); x >= 0; --x)
                            {
                                if (replacements.Count == minority.Count) break;
                                PlayerModel extra = targetList[x];
                                String xId = ExtractTagOrFriendex(extra);
                                if (String.IsNullOrEmpty(xId) && !replacements.Contains(extra))
                                {
                                    replacements.Add(extra);
                                }
                            }
                        }
                        // If not enough replacements, abandon minority players until equal
                        while (replacements.Count < minority.Count)
                        {
                            if (minority.Count == 0) break;
                            // Not enough replacements
                            PlayerModel mate = minority[0];
                            DebugScrambler("ASSERT: Not enough replacements " + minority.Count + " vs " + replacements.Count + " abandoning " + mate.Name);
                            minority.Remove(mate);
                        }
                        if (minority.Count == 0 || replacements.Count == 0 || (replacements.Count != minority.Count))
                        {
                            if (KeepFriendsInSameTeam)
                            {
                                DebugScrambler("Unable to swap clan members or friends to the target team");
                            }
                            else
                            {
                                DebugScrambler("Unable to swap clan members to the target team");
                            }
                            return;
                        }
                        // Purge the minority movers from the squad table and opposing list
                        foreach (PlayerModel mate in minority)
                        {
                            RemovePlayerFromSquadRoster(squads, mate.Name);
                            opposingList = RemovePlayerFromList(opposingList, mate.Name);
                        }
                        // Purge the replacements from the squad table and target list
                        foreach (PlayerModel rep in replacements)
                        {
                            RemovePlayerFromSquadRoster(squads, rep.Name);
                            targetList = RemovePlayerFromList(targetList, rep.Name);
                        }
                        // Swap the minority movers with the replacements
                        int i = 0;
                        foreach (PlayerModel mate in minority)
                        {
                            try
                            {
                                PlayerModel extra = replacements[i];
                                String mId = ExtractTagOrFriendex(mate);
                                String xId = ExtractTagOrFriendex(extra);
                                String mateName = (KeepFriendsInSameTeam && !String.IsNullOrEmpty(mId)) ? ("[" + mId + "]" + mate.Name) : (mate.FullName);
                                String extraName = (KeepFriendsInSameTeam && !String.IsNullOrEmpty(xId)) ? ("[" + xId + "]" + extra.Name) : (extra.FullName);
                                DebugScrambler("SWAP: ^b" + mateName + "^n/" + GetTeamName(opposing) + "/" + GetSquadName(mate.ScrambledSquad) + " with ^b" + extraName + "^n/" + GetTeamName(target) + "/" + GetSquadName(extra.ScrambledSquad));
                                int tmpSquad = extra.ScrambledSquad;
                                extra.ScrambledSquad = mate.ScrambledSquad;
                                mate.ScrambledSquad = tmpSquad;
                                extra.Team = opposing;
                                mate.Team = target;

                                targetList.Add(mate);
                                int mateKey = (1000 * mate.Team) + mate.ScrambledSquad;
                                AddPlayerToSquadRoster(squads, mate, mateKey, mate.ScrambledSquad, false);
                                opposingList.Add(extra);
                                int extraKey = (1000 * extra.Team) + extra.ScrambledSquad;
                                AddPlayerToSquadRoster(squads, extra, extraKey, extra.ScrambledSquad, false);
                                DebugScrambler("      Team " + GetTeamName(mate.Team) + " now has ^b" + mateName + "^n in " + GetSquadName(mate.ScrambledSquad) + " squad");
                                DebugScrambler("      Team " + GetTeamName(extra.Team) + " now has ^b" + extraName + "^n in " + GetSquadName(extra.ScrambledSquad) + " squad");
                            }
                            catch (Exception e)
                            {
                                ConsoleException(e);
                            }
                            ++i;
                        }
                        // Validate
                        int maxTeam = perMode.MaxPlayers / 2;
                        allOk = true;
                        if (targetList.Count > maxTeam)
                        {
                            ConsoleDebug("ASSERT: too many players on team " + GetTeamName(target));
                            allOk = false;
                        }
                        if (opposingList.Count > maxTeam)
                        {
                            ConsoleDebug("ASSERT: too many players on team " + GetTeamName(opposing));
                            allOk = false;
                        }
                        foreach (PlayerModel extra in opposingList)
                        {
                            String testTag = ExtractTagOrFriendex(extra);
                            if (testTag == splitId)
                            {
                                if (KeepFriendsInSameTeam)
                                {
                                    ConsoleDebug("ASSERT: minority clan member or friend not swapped ^b" + extra.FullName + "^n");
                                }
                                else
                                {
                                    ConsoleDebug("ASSERT: minority clan member not swapped ^b" + extra.FullName + "^n");
                                }
                                // this is tolerable, so leave allOk set to true
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        ConsoleException(e);
                    }
                    finally
                    {
                        // Must update the refs passed in
                        if (allOk)
                        {
                            if (target == 1)
                            {
                                if (targetList != null) usScrambled = targetList;
                                if (opposingList != null) ruScrambled = opposingList;
                            }
                            else
                            {
                                if (opposingList != null) usScrambled = opposingList;
                                if (targetList != null) ruScrambled = targetList;
                            }
                        }
                    }
                }
                if (KeepFriendsInSameTeam)
                {
                    DebugScrambler("Done keeping clan members or friends on the same teams!");
                }
                else
                {
                    DebugScrambler("Done keeping clan members on the same teams!");
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
            finally
            {
                DebugScrambler(" ");
            }
        }


        private List<int> GetSquadsWithClanTagOrFriendex(String id, Dictionary<int, SquadRoster> squads)
        {
            List<int> ret = new List<int>(); // list of keys into squad table
            foreach (int key in squads.Keys)
            {
                // anyone in this squad have the matching tagKey?
                foreach (PlayerModel mate in squads[key].Roster)
                {
                    String tagex = ExtractTagOrFriendex(mate);
                    if (tagex == id)
                    {
                        ret.Add(key); // add this squad to the list
                        break;
                    }
                }
            }
            return ret;
        }


        private void RemovePlayerFromSquadRoster(Dictionary<int, SquadRoster> squads, String name)
        {
            foreach (int key in squads.Keys)
            {
                SquadRoster squad = squads[key];
                List<PlayerModel> copy = new List<PlayerModel>();
                copy.AddRange(squad.Roster);
                foreach (PlayerModel mate in copy)
                {
                    if (mate.Name == name)
                    {
                        squad.Roster.Remove(mate);
                        return;
                    }
                }
            }
        }

        private List<PlayerModel> RemovePlayerFromList(List<PlayerModel> aList, String name)
        {
            List<PlayerModel> ret = new List<PlayerModel>();
            foreach (PlayerModel mate in aList)
            {
                if (mate.Name == name) continue;
                ret.Add(mate);
            }
            return ret;
        }

        private String ExtractTagOrFriendex(PlayerModel m)
        {
            /*
            For the swapper code, it is convenient to treat the Friend Index (Friendex) like a tag.
            The value of the index is formatted into a string "[x]", which should be impossible for real tags
            */
            if (m == null) return String.Empty;

            String tagOrFriendex = ExtractTag(m);
            if (KeepSquadsTogether || !KeepClanTagsInSameTeam) return tagOrFriendex;
            if (KeepFriendsInSameTeam && String.IsNullOrEmpty(tagOrFriendex) && m.Friendex != -1)
            {
                tagOrFriendex = String.Format("[{0}]", m.Friendex);
                //DebugScrambler("Using friendex ^b" + tagOrFriendex + "^n for ^b" + m.FullName);
            }
            return tagOrFriendex;
        }


        private ScrambleStatus ScrambleTeams(List<PlayerModel> usOrig, List<PlayerModel> ruOrig, bool logOnly)
        {
            /*
            We have to check every move to make sure that we are not overfilling a team
            or a squad. We get the live player count for each team. We start with the live
            count in case a new player has joined that would overfill the team. Player's leaving
            are ignored, since they help make a team less filled. Therefore these counts might
            be overestimates, but are never less than the actual live count. We can't wait
            for full round-trip updates, so we "dead reckon" the updated team counts for each
            move we make.
            */

            bool first = true;
            PerModeSettings perMode = GetPerModeSettings();
            int maxTeam = perMode.MaxPlayers / 2;
            int usCount = 0;
            int ruCount = 0;
            Dictionary<int, int> allocated = new Dictionary<int, int>(); // key is combined team + squad

            // Get live counts
            List<String> allCopy = new List<String>();
            lock (fAllPlayers)
            {
                allCopy.AddRange(fAllPlayers);
            }
            foreach (String name in allCopy)
            {
                PlayerModel m = GetPlayer(name);
                if (m == null || m.Role != ROLE_PLAYER)
                    continue;
                if (m.Team == 1) { ++usCount; }
                else if (m.Team == 2) { ++ruCount; }
            }


            // Check for full server
            if (usCount >= maxTeam && ruCount >= maxTeam)
            {
                List<PlayerModel> allPlayers = new List<PlayerModel>();
                allPlayers.AddRange(usOrig);
                allPlayers.AddRange(ruOrig);
                RestoreSquads(allPlayers, null, logOnly);
                return ScrambleStatus.CompletelyFull; // can't scramble, server is full
            }

            List<PlayerModel> usClone = new List<PlayerModel>();
            List<PlayerModel> ruClone = new List<PlayerModel>();
            // Remove players from these lists as they are moved
            usClone.AddRange(usOrig);
            ruClone.AddRange(ruOrig);

            // Move one by one, sending to the smallest team
            while (usClone.Count + ruClone.Count > 0)
            {
                if (DebugLevel >= 7) DebugScrambler("Team counts: Max = " + maxTeam + ", " + GetTeamName(1) + "(" + usCount + "), " + GetTeamName(2) + "(" + ruCount + ")");

                // Pick next list to pull from, using the one that represents moving to the lowest live count
                int nextList = 0;
                if (usCount < maxTeam && usClone.Count > 0 && (usCount <= ruCount || ruClone.Count == 0))
                {
                    nextList = 1;
                }
                else if (ruCount < maxTeam && ruClone.Count > 0 && (ruCount <= usCount || usClone.Count == 0))
                {
                    nextList = 2;
                }
                else
                {
                    List<PlayerModel> remaining = new List<PlayerModel>();
                    remaining.AddRange(usClone);
                    remaining.AddRange(ruClone);
                    RestoreSquads(remaining, allocated, logOnly);
                    if (first) return ScrambleStatus.Failure; // can't continue scramble, server is full
                    return ScrambleStatus.PartialSuccess;
                }

                // Pull from list and do the move
                DebugScrambler("Remaining to be moved: To " + GetTeamName(1) + "(" + usClone.Count + "), To " + GetTeamName(2) + "(" + ruClone.Count + ")");
                List<PlayerModel> pullFrom = (nextList == 1) ? usClone : ruClone;
                PlayerModel clone = pullFrom[0];
                pullFrom.Remove(clone);
                PlayerModel actual = GetPlayer(clone.Name);
                int actualTeam = (actual != null) ? actual.Team : 0;
                first = false;
                try
                {
                    ScrambleMove(clone, nextList, logOnly);
                    int num = 0;
                    int key = (nextList * 1000) + clone.ScrambledSquad;
                    if (allocated.TryGetValue(key, out num))
                    {
                        num = num + 1;
                    }
                    if (num > fMaxSquadSize)
                    {
                        DebugScrambler("WARNING: team " + nextList + ", squad " + clone.ScrambledSquad + " has more than " + fMaxSquadSize + " players!");
                    }
                    else
                    {
                        allocated[key] = num;
                    }
                }
                catch (Exception e) { ConsoleException(e); }

                // Moving to a new team?
                if (actualTeam != clone.Team)
                {
                    if (nextList == 1)
                    {
                        ++usCount;
                        --ruCount;
                    }
                    else
                    {
                        ++ruCount;
                        --usCount;
                    }
                } // otherwise moved to same team, so no change in team counts
            }
            return ScrambleStatus.Success;
        }

        private void ScrambleMove(PlayerModel clone, int where, bool logOnly)
        {
            int toSquad = 0;
            int toTeam = where;

            // Move to available squad
            if (!IsKnownPlayer(clone.Name)) return; // might have left
            String xt = ExtractTag(clone);
            String name = clone.Name;
            if (!String.IsNullOrEmpty(xt)) name = "[" + xt + "]" + name;
            toSquad = clone.ScrambledSquad;
            if (toSquad < 0 || toSquad > (SQUAD_NAMES.Length - 1))
            {
                ConsoleDebug("ScrambleMove: why is ^b" + name + "^n scrambled to squad " + toSquad + "?");
                return;
            }
            if (toSquad == 0)
            {
                ConsoleDebug("ScrambleMove: why is ^b" + name + "^n scrambled to squad 0?");
                return;
            }
            if (clone.Team == toTeam && clone.Squad == toSquad)
            {
                ConsoleDebug("ScrambleMove: skipping scramble move of ^b" + clone.FullName + "^n, already in right team and squad");
                return;
            }

            // Do the move
            if (!EnableLoggingOnlyMode && !logOnly)
            {
                DebugScrambler("^1^bMOVE^n^0 ^b" + clone.FullName + "^n to " + GetTeamName(toTeam) + " team, squad " + GetSquadName(toSquad));
                ServerCommand("admin.movePlayer", clone.Name, toTeam.ToString(), toSquad.ToString(), "false");
                Thread.Sleep(60);
            }
            else
            {
                DebugScrambler("^9(SIMULATED) ^1^bMOVE^n^0 ^b" + clone.FullName + "^n to " + GetTeamName(toTeam) + " team, squad " + GetSquadName(toSquad));
            }

            // For debugging and since this is a clone model, update Team & Squad to reflect move
            clone.Team = toTeam;
            clone.Squad = toSquad;
        }

        private void RestoreSquads(List<PlayerModel> allCopy, Dictionary<int, int> allocated, bool logOnly)
        {
            // Try to restore to original squad
            foreach (PlayerModel clone in allCopy)
            {
                try
                {
                    // Check to make sure original team and squad are available
                    if (clone.Team < 1 || clone.Team > 2 || clone.OriginalSquad < 1 || clone.OriginalSquad >= SQUAD_NAMES.Length) continue;
                    int toSquad = clone.OriginalSquad;
                    // If the original squad is full, pick one that isn't
                    if (allocated != null)
                    {
                        int key = (1000 * clone.Team) + toSquad;
                        while (allocated.ContainsKey(key) && allocated[key] >= fMaxSquadSize)
                        {
                            ++toSquad;
                            if (toSquad >= SQUAD_NAMES.Length)
                            {
                                break;
                            }
                            key = (1000 * clone.Team) + toSquad;
                        }
                        if (toSquad >= SQUAD_NAMES.Length)
                        {
                            DebugScrambler("UNABLE to restore ^b" + clone.FullName + "^n to a squad, skipping");
                            continue;
                        }
                    }
                    // Do the move
                    if (!EnableLoggingOnlyMode && !logOnly)
                    {
                        DebugScrambler("^1^bMOVE^n^0 ^b" + clone.FullName + "^n to " + GetTeamName(clone.Team) + " team, restore squad " + GetSquadName(toSquad));
                        ServerCommand("admin.movePlayer", clone.Name, clone.Team.ToString(), toSquad.ToString(), "false");
                        Thread.Sleep(60);
                    }
                    else
                    {
                        DebugScrambler("^9(SIMULATED) ^1^bMOVE^n^0 ^b" + clone.FullName + "^n to " + GetTeamName(clone.Team) + " team, restore squad " + GetSquadName(toSquad));
                    }
                }
                catch (Exception e)
                {
                    ConsoleException(e);
                }
            }

        }

        private SquadRoster AddPlayerToSquadRoster(Dictionary<int, SquadRoster> squads, PlayerModel player, int key, int squadId, bool ignoreSize)
        {
            SquadRoster squad = null;
            if (squads.TryGetValue(key, out squad))
            {
                if (ignoreSize || squad.Roster.Count < fMaxSquadSize)
                {
                    squad.Roster.Add(player);
                }
            }
            else
            {
                squad = new SquadRoster(squadId);
                squad.Roster.Add(player);
                squads[key] = squad;
            }
            return squad;
        }

        private void SumMetricByTeam(List<PlayerModel> usScrambled, List<PlayerModel> ruScrambled, out double usMetric, out double ruMetric)
        {
            usMetric = 0;
            ruMetric = 0;
            // sum up the metric by team
            switch (ScrambleBy)
            {
                case DefineStrong.RoundScore:
                    foreach (PlayerModel p in usScrambled)
                    {
                        usMetric = usMetric + p.ScoreRound;
                    }
                    foreach (PlayerModel p in ruScrambled)
                    {
                        ruMetric = ruMetric + p.ScoreRound;
                    }
                    break;
                case DefineStrong.RoundSPM:
                    foreach (PlayerModel p in usScrambled)
                    {
                        usMetric = usMetric + p.SPMRound;
                    }
                    foreach (PlayerModel p in ruScrambled)
                    {
                        ruMetric = ruMetric + p.SPMRound;
                    }
                    break;
                case DefineStrong.RoundKills:
                    foreach (PlayerModel p in usScrambled)
                    {
                        usMetric = usMetric + p.KillsRound;
                    }
                    foreach (PlayerModel p in ruScrambled)
                    {
                        ruMetric = ruMetric + p.KillsRound;
                    }
                    break;
                case DefineStrong.RoundKDR:
                    foreach (PlayerModel p in usScrambled)
                    {
                        usMetric = usMetric + p.KDRRound;
                    }
                    foreach (PlayerModel p in ruScrambled)
                    {
                        ruMetric = ruMetric + p.KDRRound;
                    }
                    break;
                case DefineStrong.PlayerRank:
                    foreach (PlayerModel p in usScrambled)
                    {
                        usMetric = usMetric + p.Rank;
                    }
                    foreach (PlayerModel p in ruScrambled)
                    {
                        ruMetric = ruMetric + p.Rank;
                    }
                    break;
                case DefineStrong.RoundKPM:
                    foreach (PlayerModel p in usScrambled)
                    {
                        usMetric = usMetric + p.KPMRound;
                    }
                    foreach (PlayerModel p in ruScrambled)
                    {
                        ruMetric = ruMetric + p.KPMRound;
                    }
                    break;
                case DefineStrong.BattlelogSPM:
                    foreach (PlayerModel p in usScrambled)
                    {
                        usMetric = usMetric + ((p.StatsVerified) ? p.SPM : p.SPMRound);
                    }
                    foreach (PlayerModel p in ruScrambled)
                    {
                        ruMetric = ruMetric + ((p.StatsVerified) ? p.SPM : p.SPMRound);
                    }
                    break;
                case DefineStrong.BattlelogKDR:
                    foreach (PlayerModel p in usScrambled)
                    {
                        usMetric = usMetric + ((p.StatsVerified) ? p.KDR : p.KDRRound);
                    }
                    foreach (PlayerModel p in ruScrambled)
                    {
                        ruMetric = ruMetric + ((p.StatsVerified) ? p.KDR : p.KDRRound);
                    }
                    break;
                case DefineStrong.BattlelogKPM:
                    foreach (PlayerModel p in usScrambled)
                    {
                        usMetric = usMetric + ((p.StatsVerified) ? p.KPM : p.KPMRound);
                    }
                    foreach (PlayerModel p in ruScrambled)
                    {
                        ruMetric = ruMetric + ((p.StatsVerified) ? p.KPM : p.KPMRound);
                    }
                    break;
                default:
                    foreach (PlayerModel p in usScrambled)
                    {
                        usMetric = usMetric + p.ScoreRound;
                    }
                    foreach (PlayerModel p in ruScrambled)
                    {
                        ruMetric = ruMetric + p.ScoreRound;
                    }
                    break;
            }
        }

        private void RemapSquad(Dictionary<int, SquadRoster> squadTable, SquadRoster squad)
        {
            int emptyId = 1;
            while (squadTable.ContainsKey(emptyId))
            {
                emptyId = emptyId + 1;
                if (emptyId > (SQUAD_NAMES.Length - 1))
                {
                    if (DebugLevel >= 8) ConsoleDebug("RemapSquad: ran out of empty squads!");
                    return;
                }
            }
            squad.Squad = emptyId;
        }

        private void RememberTeams()
        {
            fDebugScramblerStartRound[0].Clear();
            fDebugScramblerStartRound[1].Clear();
            lock (fAllPlayers)
            {
                foreach (String egg in fAllPlayers)
                {
                    try
                    {
                        PlayerModel player = GetPlayer(egg);
                        if (player == null) continue;

                        // For debugging
                        if (player.Team > 0 && player.Team <= 2)
                        {
                            fDebugScramblerStartRound[player.Team - 1].Add(player.ClonePlayer());
                        }
                        else continue; // skip joining players
                    }
                    catch (Exception e)
                    {
                        if (DebugLevel >= 8) ConsoleException(e);
                    }
                }
            }
            if (DebugLevel >= 6) CommandToLog("scrambled");
        }



        private void AssignFillerToTeam(PlayerModel filler, int toTeamId, List<PlayerModel> target, Dictionary<int, SquadRoster> targetSquadTable)
        {
            String who = GetTeamName(toTeamId);
            if ((target.Count + 1) > (MaximumServerSize / 2))
            {
                DebugScrambler("Team " + who + " is full, skipping filler assignment of ^b" + filler.FullName);
                return;
            }
            if (!IsKnownPlayer(filler.Name)) return; // might have left

            // Find a squad with room to add this player, otherwise create a squad
            int toSquadId = 0;
            int emptyId = 1;
            SquadRoster toSquad = null;
            foreach (int key in targetSquadTable.Keys)
            {
                toSquad = targetSquadTable[key];
                if (toSquad.Roster.Count == fMaxSquadSize) continue;
                toSquadId = key;
                break;
            }
            if (toSquadId == 0)
            {
                // Create a new squad
                while (targetSquadTable.ContainsKey(emptyId))
                {
                    ++emptyId;
                    if (emptyId >= SQUAD_NAMES.Length)
                    {
                        emptyId = 0;
                        break;
                    }
                }
                toSquadId = emptyId;
                ConsoleDebug("AssignFillerToTeam: created new squad " + GetSquadName(toSquadId));
            }
            else
            {
                ConsoleDebug("AssignFillerToTeam: using existing squad " + GetSquadName(toSquadId));
            }
            DebugScrambler("Filling in " + who + " team with player ^b" + filler.FullName + "^n to squad " + GetSquadName(toSquadId));
            filler.ScrambledSquad = toSquadId;
            filler.Team = toTeamId;
            target.Add(filler);
            toSquad = null;
            if (!targetSquadTable.ContainsKey(toSquadId))
            {
                toSquad = new SquadRoster(toSquadId);
                targetSquadTable[toSquadId] = toSquad;
            }
            else
            {
                toSquad = targetSquadTable[toSquadId];
            }
            toSquad.Roster.Add(filler);
        }

        private void UnsquadMove(Dictionary<int, SquadRoster> usSquads, Dictionary<int, SquadRoster> ruSquads, bool logOnly, List<String> unsquaded)
        {
            DebugScrambler("UNSQUADING DUPLICATE SQUADS");
            // Only need to unsquad when squad id exists on both teams

            List<int> onlyLogOnce = new List<int>();
            List<String> liveNames = new List<String>();
            lock (fAllPlayers)
            {
                liveNames.AddRange(fAllPlayers);
            }
            foreach (String name in liveNames)
            {
                try
                {
                    // Skip new joiners on the extras list, they are already out of the way.
                    lock (fExtrasLock)
                    {
                        if (fExtraNames.Contains(name)) continue;
                    }
                    PlayerModel livePlayerModel = GetPlayer(name); // Using live player model
                    SquadMove(livePlayerModel, livePlayerModel.Team, 0, logOnly);
                    unsquaded.Add(livePlayerModel.Name);
                }
                catch (Exception e)
                {
                    ConsoleException(e);
                }
            }

            DebugScrambler("FINISHED UNSQUADING");
        }

        private void SquadMove(PlayerModel clone, int toTeam, int toSquad, bool logOnly)
        {
            // Do the move
            if (!EnableLoggingOnlyMode && !logOnly)
            {
                DebugScrambler("^1^bMOVE SQUAD^n^0 ^b" + clone.FullName + "^n to " + GetTeamName(toTeam) + " team, " + GetSquadName(toSquad) + " squad");
                ServerCommand("admin.movePlayer", clone.Name, toTeam.ToString(), toSquad.ToString(), "false");
                Thread.Sleep(60);
            }
            else
            {
                DebugScrambler("^9(SIMULATED) ^1^bMOVE SQUAD^n^0 ^b" + clone.FullName + "^n to " + GetTeamName(toTeam) + " team,  " + GetSquadName(toSquad) + " squad");
            }
            // force squad to new squad id
            clone.Squad = toSquad;
        }














    } // end MULTIbalancer

} // end namespace PRoConEvents
