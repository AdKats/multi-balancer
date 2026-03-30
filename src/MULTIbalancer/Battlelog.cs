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
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Flurl.Http;

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
        /* ======================== BATTLELOG ============================= */

        private void AddPlayerFetch(String name)
        {
            if (!EnableBattlelogRequests) return;
            if (String.IsNullOrEmpty(name)) return;
            PlayerModel player = GetPlayer(name);
            if (player == null) return;
            if (player.TagFetchStatus.State != FetchState.New && player.TagFetchStatus.State != FetchState.InQueue)
            {
                DebugFetch("Cannot refetch tag for player ^b" + player.Name + "^n, previous result was " + player.TagFetchStatus.State);
                if (WhichBattlelogStats == BattlelogStats.ClanTagOnly) return;
            }
            if (player.StatsFetchStatus.State != FetchState.New && player.TagFetchStatus.State != FetchState.InQueue)
            {
                DebugFetch("Cannot refetch stats for player ^b" + player.Name + "^n, previous result was " + player.StatsFetchStatus.State);
                return;
            }
            player.TagFetchStatus.State = FetchState.InQueue;
            player.StatsFetchStatus.State = FetchState.InQueue;
            lock (fPriorityFetchQ)
            {
                if (!fPriorityFetchQ.Contains(name))
                {
                    fPriorityFetchQ.Enqueue(name);
                    Monitor.Pulse(fPriorityFetchQ);
                }
            }
        }

        private void RemovePlayerFetch(String name)
        {
            if (String.IsNullOrEmpty(name)) return;
            PlayerModel player = GetPlayer(name);
            if (player == null) return;
            lock (fPriorityFetchQ)
            {
                if (fPriorityFetchQ.Contains(name))
                {
                    player.TagFetchStatus.State = FetchState.Aborted;
                    player.StatsFetchStatus.State = FetchState.Aborted;
                    player.TagVerified = false;
                    player.StatsVerified = false;
                }
            }
        }

        public void FetchLoop()
        {
            try
            {
                DateTime since = DateTime.MinValue;
                Int32 requests = 1;

                while (fIsEnabled)
                {
                    String name = null;
                    Boolean isTagRequest = true;
                    Int32 n = 0;
                    lock (fPriorityFetchQ)
                    {
                        while (fPriorityFetchQ.Count == 0)
                        {
                            Monitor.Wait(fPriorityFetchQ);
                            if (!fIsEnabled) return;
                        }
                        /*
                        Tag requests have priority over stats requests.
                        Exhaust the tag queue before taking from the stats queue.
                        */
                        if (fPriorityFetchQ.TagQueue.Count > 0)
                        {
                            name = fPriorityFetchQ.TagQueue.Dequeue();
                        }
                        else if (fPriorityFetchQ.StatsQueue.Count > 0)
                        {
                            name = fPriorityFetchQ.StatsQueue.Dequeue();
                            isTagRequest = false;
                        }
                        n = fPriorityFetchQ.Count;
                    }

                    if (since == DateTime.MinValue) since = DateTime.Now;

                    String msg = n.ToString() + " request" + ((n > 1) ? "s" : "") + " in Battlelog request queue";
                    if (n == 0)
                    {
                        msg = "no more requests in Battlelog request queue";
                        DebugFetch("^0" + msg, 4);
                    }
                    else
                    {
                        DebugFetch("^0" + msg, 3);
                    }

                    PlayerModel player = GetPlayer(name);
                    if (player == null) continue;
                    if (!EnableBattlelogRequests)
                    {
                        player.TagFetchStatus.State = FetchState.Aborted; // drain the fetch queue
                        player.StatsFetchStatus.State = FetchState.Aborted; // drain the fetch queue
                    }
                    if (player.TagFetchStatus.State == FetchState.Aborted || player.StatsFetchStatus.State == FetchState.Aborted)
                    {
                        if (DebugLevel >= 8) ConsoleDebug("FetchLoop: fetch for ^b" + name + "^n was aborted!");
                        continue;
                    }

                    if (++requests > MaximumRequestRate)
                    {
                        // Wait remainder of 20 seconds before continuing
                        Int32 delay = 20 - Convert.ToInt32(DateTime.Now.Subtract(since).TotalSeconds);
                        if (delay > 0)
                        {
                            DebugFetch("Sleeping remaining " + delay + " seconds before sending next request");
                            while (delay > 0)
                            {
                                Thread.Sleep(1000);
                                if (!fIsEnabled) return;
                                if (!EnableBattlelogRequests) break;
                                --delay;
                            }
                        }
                        requests = 1; // reset
                        since = DateTime.Now;
                    }

                    String requestType = (isTagRequest) ? "clanTag" : "overview";
                    if (fIsCacheEnabled)
                    {
                        SendCacheRequest(name, requestType);
                    }
                    else
                    {
                        switch (fGameVersion)
                        {
                            case GameVersion.BFH:
                                SendBattlelogRequestBFH(name, requestType, null);
                                break;
                            case GameVersion.BF3:
                                SendBattlelogRequest(name, requestType, null);
                                break;
                            case GameVersion.BF4:
                            default:
                                SendBattlelogRequestBF4(name, requestType, null);
                                break;
                        }
                        PlayerModel pm = GetPlayer(name);
                        if (isTagRequest)
                        {
                            if (pm.TagFetchStatus.State != FetchState.Succeeded) pm.TagVerified = true;
                        }
                        else
                        {
                            if (pm.StatsFetchStatus.State != FetchState.Succeeded) pm.StatsVerified = true;
                        }
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
                if (!fAborted) ConsoleWrite("^bFetchLoop^n thread stopped", 0);
            }
        }

        private void SendBattlelogRequest(String name, String requestType, PlayerModel player)
        {
            try
            {
                String result = String.Empty;
                String err = String.Empty;

                if (player == null) player = GetPlayer(name);
                if (player == null) return;
                FetchInfo status = (requestType == "clanTag") ? player.TagFetchStatus : player.StatsFetchStatus;
                status.State = FetchState.Requesting;
                status.Since = DateTime.Now;
                status.RequestType = requestType;
                DebugFetch("Fetching from Battlelog " + requestType + "(^b" + name + "^n)");

                if (String.IsNullOrEmpty(player.PersonaId))
                {
                    // Get the main page
                    Boolean ok = false;
                    status.State = FetchState.Failed;
                    if (!fIsEnabled) return;
                    ok = FetchWebPage(ref result, "http://battlelog.battlefield.com/bf3/user/" + name);
                    if (!fIsEnabled) return;

                    if (!ok) return;

                    // Extract the personaId
                    MatchCollection pid = Regex.Matches(result, @"bf3/soldier/" + name + @"/stats/(\d+)(['""]|/\s*['""]|/[^/'""]+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    foreach (Match match in pid)
                    {
                        if (match.Success && !Regex.Match(match.Groups[2].Value.Trim(), @"(ps3|xbox)", RegexOptions.IgnoreCase).Success)
                        {
                            player.PersonaId = match.Groups[1].Value.Trim();
                            break;
                        }
                    }

                    if (String.IsNullOrEmpty(player.PersonaId))
                    {
                        DebugFetch("Request for ^b" + name + "^n failed, could not find persona-id!");
                        status.State = FetchState.Failed;
                        return;
                    }
                }

                if (requestType == "clanTag")
                {
                    // Extract the player tag
                    Match tag = Regex.Match(result, player.PersonaId + @"/pc/[/'"">\s]+\[\s*([a-zA-Z0-9]+)\s*\]\s*" + name, RegexOptions.IgnoreCase | RegexOptions.Singleline); // Fixed #9
                                                                                                                                                                                //Match tag = Regex.Match(result, @"\[\s*([a-zA-Z0-9]+)\s*\]\s*" + name, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (tag.Success)
                    {
                        Hashtable data = new Hashtable();
                        data["clanTag"] = tag.Groups[1].Value;
                        SetTag(player, data); // sets status.State
                        DebugFetch("^4Battlelog tag updated: ^b" + player.FullName);
                    }
                    else
                    {
                        // No tag
                        player.TagVerified = true;
                        status.State = FetchState.Succeeded;
                        DebugFetch("^4Battlelog says ^b" + player.Name + "^n has no tag");
                    }
                }
                else if (requestType == "overview")
                {
                    status.State = FetchState.Failed;
                    if (!fIsEnabled || WhichBattlelogStats == BattlelogStats.ClanTagOnly) return;
                    String furl = "http://battlelog.battlefield.com/bf3/overviewPopulateStats/" + player.PersonaId + "/bf3-us-assault/1/";
                    if (FetchWebPage(ref result, furl))
                    {
                        if (!fIsEnabled) return;

                        Hashtable json = (Hashtable)JSON.JsonDecode(result);

                        // verify we got a success message
                        if (!CheckSuccess(json, out err))
                        {
                            DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): " + err);
                            return;
                        }

                        // verify there is data structure
                        Hashtable data = null;
                        if (!json.ContainsKey("data") || (data = (Hashtable)json["data"]) == null)
                        {
                            DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): JSON response does not contain a data field (^4" + furl + "^0)");
                            return;
                        }

                        // verify there is stats structure
                        Hashtable stats = null;
                        if (!data.ContainsKey("overviewStats") || (stats = (Hashtable)data["overviewStats"]) == null)
                        {
                            DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): JSON response data does not contain overviewStats (^4" + furl + "^0)");
                            return;
                        }

                        // extract the fields from the stats
                        SetStats(player, stats); // sets status.State
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        private void SendBattlelogRequestBFH(String name, String requestType, PlayerModel player)
        { // TBD
            try
            {
                String result = String.Empty;
                String err = String.Empty;

                if (player == null) player = GetPlayer(name);
                if (player == null) return;

                FetchInfo status = (requestType == "clanTag") ? player.TagFetchStatus : player.StatsFetchStatus;
                status.State = FetchState.Requesting;
                status.Since = DateTime.Now;
                status.RequestType = requestType;
                DebugFetch("Fetching from Battlelog BF4 " + requestType + "(^b" + name + "^n)");

                /*
                try
                {
                    //Get persona
                    DoBattlelogWait();
                    String userResponse = ("http://battlelog.battlefield.com/bfh/user/" + aPlayer.player_name + "?nocacherandom=" + Environment.TickCount).GetStringAsync().Result;
                    Match pid = Regex.Match(userResponse, @"agent\/" + aPlayer.player_name + @"\/stats\/(\d+)");
                    if (!pid.Success)
                    {
                        Log.Warn("Could not find BFHL persona ID for " + aPlayer.player_name);
                        return;
                    }
                    aPlayer.player_personaID = pid.Groups[1].Value.Trim();
                    Log.Debug("Persona ID fetched for " + aPlayer.player_name + ":" + aPlayer.player_personaID, 4);
                    //Get tag
                    DoBattlelogWait();
                    String soldierResponse = ("http://battlelog.battlefield.com/bfh/agent/" + aPlayer.player_name + "/stats/" + aPlayer.player_personaID + "/pc/" + "?nocacherandom=" + Environment.TickCount).GetStringAsync().Result;
                    Match tag = Regex.Match(soldierResponse, @"\[\s*([a-zA-Z0-9]+)\s*\]\s*</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (!tag.Success || String.IsNullOrEmpty(tag.Groups[1].Value.Trim()))
                    {
                        Log.Debug("Could not find BFHL clan tag for " + aPlayer.player_name, 4);
                    }
                    else
                    {
                        aPlayer.player_clanTag = tag.Groups[1].Value.Trim();
                        Log.Debug("Clan tag [" + aPlayer.player_clanTag + "] found for " + aPlayer.player_name, 4);
                    }
                }
                catch (Exception e)
                {
                    Log.Exception("Error fetching BFHL player info", e);
                }
                */

                if (String.IsNullOrEmpty(player.PersonaId))
                {
                    // Get the main page
                    Boolean ok = false;
                    status.State = FetchState.Failed;
                    if (!fIsEnabled) return;
                    ok = FetchWebPage(ref result, "http://battlelog.battlefield.com/bfh/user/" + name + "?nocacherandom=" + Environment.TickCount);
                    if (!fIsEnabled) return;
                    if (!ok) return;

                    // Extract the personaId
                    Match pid = Regex.Match(result, @"agent\/" + name + @"\/stats\/(\d+)");
                    if (!pid.Success)
                    {
                        DebugFetch("Request for ^b" + name + "^n failed, could not find persona-id!");
                        status.State = FetchState.Failed;
                        return;
                    }
                    player.PersonaId = pid.Groups[1].Value.Trim();
                    DebugFetch("Persona ID fetched for " + name + ":" + player.PersonaId);
                }

                if (requestType == "clanTag")
                {
                    // Get the stats page
                    Boolean ok = false;
                    status.State = FetchState.Failed;
                    if (!fIsEnabled) return;
                    String bfhfurl = "http://battlelog.battlefield.com/bfh/agent/" + name + "/stats/" + player.PersonaId + "/pc/" + "?nocacherandom=" + Environment.TickCount;
                    ok = FetchWebPage(ref result, bfhfurl);
                    if (!fIsEnabled) return;
                    if (!ok) return;

                    // Extract the player tag
                    String bfhTag = String.Empty;
                    Match tag = Regex.Match(result, @"\[\s*([a-zA-Z0-9]+)\s*\]\s*</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    if (tag.Success)
                    {
                        bfhTag = tag.Groups[1].Value.Trim();
                    }
                    if (String.IsNullOrEmpty(bfhTag))
                    {
                        // No tag
                        player.Tag = String.Empty;
                        player.TagVerified = true;
                        status.State = FetchState.Succeeded;
                        DebugFetch("^4Battlelog says ^b" + player.Name + "^n has no BFH tag");
                    }
                    else
                    {
                        Hashtable tmp = new Hashtable();
                        tmp["clanTag"] = bfhTag;
                        SetTag(player, tmp); // sets status.State
                        DebugFetch("^4Battlelog BFH tag updated: ^b^1" + player.FullName);
                    }
                }
                else if (requestType == "overview")
                {
                    status.State = FetchState.Failed;
                    if (!fIsEnabled || WhichBattlelogStats == BattlelogStats.ClanTagOnly) return;
                    String furl = "http://battlelog.battlefield.com/bfh/warsawoverviewpopulate/" + player.PersonaId + "/1/";
                    if (FetchWebPage(ref result, furl))
                    {
                        if (!fIsEnabled) return;

                        Hashtable json = (Hashtable)JSON.JsonDecode(result);

                        // verify we got a success message
                        if (!CheckSuccess(json, out err))
                        {
                            DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): " + err);
                            return;
                        }

                        // verify there is data structure
                        Hashtable data = null;
                        if (!json.ContainsKey("data") || (data = (Hashtable)json["data"]) == null)
                        {
                            DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): JSON response does not contain a data field (^4" + furl + "^0)");
                            return;
                        }

                        // verify there is stats structure
                        Hashtable stats = null;
                        if (!data.ContainsKey("generalStats") || (stats = (Hashtable)data["generalStats"]) == null)
                        {
                            DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): JSON response data does not contain overviewStats (^4" + furl + "^0)");
                            return;
                        }

                        // extract the fields from the stats
                        SetStats(player, stats); // sets status.State
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        private void SendBattlelogRequestBF4(String name, String requestType, PlayerModel player)
        {
            try
            {
                String result = String.Empty;
                String err = String.Empty;

                if (player == null) player = GetPlayer(name);
                if (player == null) return;

                FetchInfo status = (requestType == "clanTag") ? player.TagFetchStatus : player.StatsFetchStatus;
                status.State = FetchState.Requesting;
                status.Since = DateTime.Now;
                status.RequestType = requestType;
                DebugFetch("Fetching from Battlelog BF4 " + requestType + "(^b" + name + "^n)");

                if (String.IsNullOrEmpty(player.PersonaId))
                {
                    // Get the main page
                    Boolean ok = false;
                    status.State = FetchState.Failed;
                    if (!fIsEnabled) return;
                    ok = FetchWebPage(ref result, "http://battlelog.battlefield.com/bf4/user/" + name);
                    if (!fIsEnabled) return;

                    if (!ok) return;

                    // Extract the personaId
                    MatchCollection pid = Regex.Matches(result, @"bf4/soldier/" + name + @"/stats/(\d+)(['""]|/\s*['""]|/[^/'""]+)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                    foreach (Match match in pid)
                    {
                        if (match.Success && !Regex.Match(match.Groups[2].Value.Trim(), @"(ps3|xbox)", RegexOptions.IgnoreCase).Success)
                        {
                            player.PersonaId = match.Groups[1].Value.Trim();
                            break;
                        }
                    }

                    if (String.IsNullOrEmpty(player.PersonaId))
                    {
                        DebugFetch("Request for ^b" + name + "^n failed, could not find persona-id!");
                        status.State = FetchState.Failed;
                        return;
                    }
                }

                if (requestType == "clanTag")
                {
                    // Get the stats page
                    Boolean ok = false;
                    status.State = FetchState.Failed;
                    if (!fIsEnabled) return;
                    String bf4furl = "http://battlelog.battlefield.com/bf4/warsawoverviewpopulate/" + player.PersonaId + "/1/";
                    ok = FetchWebPage(ref result, bf4furl);
                    if (!fIsEnabled) return;
                    if (!ok) return;

                    // Get tag from json
                    Hashtable jsonBF4 = (Hashtable)JSON.JsonDecode(result);

                    // verify we got a success message
                    if (!CheckSuccess(jsonBF4, out err))
                    {
                        DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): " + err);
                        return;
                    }

                    // verify there is data structure
                    Hashtable data = null;
                    if (!jsonBF4.ContainsKey("data") || (data = (Hashtable)jsonBF4["data"]) == null)
                    {
                        DebugFetch("Request BF4 " + status.RequestType + "(^b" + name + "^n): JSON response does not contain a data field (^4" + bf4furl + "^0)");
                        return;
                    }

                    // verify there is viewedPersonaInfo structure, okay if null!
                    Hashtable info = null;
                    if (!data.ContainsKey("viewedPersonaInfo") || (info = (Hashtable)data["viewedPersonaInfo"]) == null)
                    {
                        if (DebugLevel >= 7) DebugFetch("Request BF4" + status.RequestType + "(^b" + name + "^n): JSON response data does not contain viewedPersonaInfo");
                        // No tag
                        player.Tag = String.Empty;
                        player.TagVerified = true;
                        status.State = FetchState.Succeeded;
                        DebugFetch("^4Battlelog says ^b" + player.Name + "^n has no BF4 tag (no viewedPersonaInfo)");
                        return;
                    }

                    // Extract the player tag
                    String bf4Tag = String.Empty;
                    if (!info.ContainsKey("tag") || String.IsNullOrEmpty(bf4Tag = (String)info["tag"]))
                    {
                        // No tag
                        player.Tag = String.Empty;
                        player.TagVerified = true;
                        status.State = FetchState.Succeeded;
                        DebugFetch("^4Battlelog says ^b" + player.Name + "^n has no BF4 tag");
                    }
                    else
                    {
                        Hashtable tmp = new Hashtable();
                        tmp["clanTag"] = bf4Tag;
                        SetTag(player, tmp); // sets status.State
                        DebugFetch("^4Battlelog BF4 tag updated: ^b^1" + player.FullName);
                    }
                }
                else if (requestType == "overview")
                {
                    //DebugFetch("Stats fetch not supported for BF4 yet: " + player.Name);
                    status.State = FetchState.Failed;
                    if (!fIsEnabled || WhichBattlelogStats == BattlelogStats.ClanTagOnly) return;
                    String furl = "http://battlelog.battlefield.com/bf4/warsawoverviewpopulate/" + player.PersonaId + "/1/";
                    if (FetchWebPage(ref result, furl))
                    {
                        if (!fIsEnabled) return;

                        Hashtable json = (Hashtable)JSON.JsonDecode(result);

                        // verify we got a success message
                        if (!CheckSuccess(json, out err))
                        {
                            DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): " + err);
                            return;
                        }

                        // verify there is data structure
                        Hashtable data = null;
                        if (!json.ContainsKey("data") || (data = (Hashtable)json["data"]) == null)
                        {
                            DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): JSON response does not contain a data field (^4" + furl + "^0)");
                            return;
                        }

                        // verify there is stats structure
                        Hashtable stats = null;
                        if (!data.ContainsKey("overviewStats") || (stats = (Hashtable)data["overviewStats"]) == null)
                        {
                            DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): JSON response data does not contain overviewStats (^4" + furl + "^0)");
                            return;
                        }

                        // extract the fields from the stats
                        SetStats(player, stats); // sets status.State
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public Boolean IsCacheEnabled(Boolean verbose)
        {
            if (fGameVersion != GameVersion.BF3)
            {
                ConsoleWarn("BattlelogCache only supports BF3!");
                return false;
            }
            List<MatchCommand> registered = this.GetRegisteredCommands();
            foreach (MatchCommand command in registered)
            {
                if (command.RegisteredClassname.CompareTo("CBattlelogCache") == 0 && command.RegisteredMethodName.CompareTo("PlayerLookup") == 0)
                {
                    if (verbose) DebugFetch("^bBattlelog Cache^n plugin will be used for stats fetching!");
                    return true;
                }
                else
                {
                    DebugFetch("Registered P: " + command.RegisteredClassname + ", M: " + command.RegisteredMethodName);
                }
            }
            if (verbose) DebugWrite("^1^bBattlelog Cache^n plugin is disabled; installing/updating and enabling the plugin is recommended for use with " + GetPluginName() + "!", 3);
            return false;
        }

        private void SendCacheRequest(String name, String requestType)
        {
            try
            {
                /* 
                Called in the FetchLoop thread
                */
                Hashtable request = new Hashtable();
                request["playerName"] = name;
                request["pluginName"] = GetPluginName();
                request["pluginMethod"] = "CacheResponse";
                request["requestType"] = requestType;

                // Set up response entry
                PlayerModel player = GetPlayer(name);
                if (player == null) return;
                FetchInfo status = (requestType == "clanTag") ? player.TagFetchStatus : player.StatsFetchStatus;
                status.State = FetchState.Requesting;
                status.Since = DateTime.Now;
                status.RequestType = requestType;
                DebugFetch("Sending cache request " + requestType + "(^b" + name + "^n)");

                // Send request
                if (!fIsEnabled || fAborted) return;
                this.ExecuteCommand("procon.protected.plugins.call", "CBattlelogCache", "PlayerLookup", JSON.JsonEncode(request));
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public void CacheResponse(params String[] response)
        {
            try
            {
                /*
                Called from the Battlelog Cache plugin Response thread
                */
                String val = null;
                if (DebugLevel >= 8)
                {
                    DebugFetch("CacheResponse called with " + response.Length + " parameters");
                    for (Int32 i = 0; i < response.Length; ++i)
                    {
                        DebugFetch("#" + i + ") Length: " + response[i].Length);
                        val = response[i];
                        if (val.Length > 100) val = val.Substring(0, 500) + " ... ";
                        if (val.Contains("{")) val = val.Replace('{', '<').Replace('}', '>'); // ConsoleWrite doesn't like messages with "{" in it
                        DebugFetch("#" + i + ") Value: " + val);
                    }
                }

                String name = response[0]; // Player's name
                val = response[1]; // JSON string
                if (String.IsNullOrEmpty(name) || String.IsNullOrEmpty(val))
                {
                    DebugFetch("Invalid response from Battlelog Cache!");
                    return;
                }

                Hashtable header = (Hashtable)JSON.JsonDecode(val);

                if (header == null)
                {
                    DebugFetch("Request for ^b" + name + "^n failed!");
                    return;
                }

                String result = (String)header["type"];
                Double fetchTime = -1;
                Double.TryParse((String)header["fetchTime"], out fetchTime);
                Double age = -1;
                Double.TryParse((String)header["age"], out age);

                PlayerModel player = GetPlayer(name);
                if (player == null)
                {
                    DebugFetch("Unknown player ^b" + name);
                    return;
                }
                String err = String.Empty;
                String requestType = String.Empty;
                DateTime since = DateTime.Now;
                FetchInfo status = null;

                if (CheckSuccess(header, out err))
                {
                    // verify there is data structure
                    Hashtable d = null;
                    if (!header.ContainsKey("data") || (d = (Hashtable)header["data"]) == null)
                    {
                        ConsoleDebug("CacheResponse header does not contain data field!");
                        // FetchStatus left in Requesting state, since we can't decide which requestType this is
                        return;
                    }
                    if (d.ContainsKey("clanTag"))
                    {
                        requestType = "clanTag";
                    }
                    else if (d.ContainsKey("overviewStats"))
                    {
                        requestType = "overview";
                    }

                    if (player.TagFetchStatus.RequestType == requestType)
                    {
                        status = player.TagFetchStatus;
                    }
                    else if (player.StatsFetchStatus.RequestType == requestType)
                    {
                        status = player.StatsFetchStatus;
                    }
                    else
                    {
                        ConsoleDebug("CacheResponse unknown requestType: " + requestType);
                        return;
                    }
                    since = status.Since;

                    if (fetchTime > 0)
                    {
                        DebugFetch("Request " + status.RequestType + "(^b" + name + "^n) succeeded, cache refreshed from Battlelog, took ^2" + fetchTime.ToString("F1") + " seconds");
                    }
                    else if (age > 0)
                    {
                        TimeSpan a = TimeSpan.FromSeconds(age);
                        DebugFetch("Request " + status.RequestType + "(^b" + name + "^n) succeeded, cached stats used, age is " + a.ToString().Substring(0, 8));
                    }

                    // Apply the result to the player
                    switch (requestType)
                    {
                        case "clanTag":
                            SetTag(player, d);
                            if (String.IsNullOrEmpty(player.Tag))
                            {
                                DebugFetch("^4Battlelog Cache says ^b" + player.Name + "^n has no tag");
                            }
                            else
                            {
                                DebugFetch("^4Battlelog Cache tag updated: ^b" + player.FullName);
                            }
                            break;
                        case "overview":
                            {
                                // verify there is stats structure
                                Hashtable stats = null;
                                if ((stats = (Hashtable)d["overviewStats"]) == null)
                                {
                                    status.State = FetchState.Failed;
                                    DebugFetch("Request " + status.RequestType + "(^b" + name + "^n): Battlelog Cache response data does not contain overviewStats");
                                    return;
                                }
                                SetStats(player, stats);
                                break;
                            }
                        default:
                            break;
                    }
                }
                else
                {
                    if (player.TagFetchStatus.State == FetchState.Requesting)
                    {
                        player.TagFetchStatus.State = FetchState.Failed;
                        requestType = "clanTag";
                    }
                    else if (player.StatsFetchStatus.State == FetchState.Requesting)
                    {
                        player.StatsFetchStatus.State = FetchState.Failed;
                        requestType = "overview";
                    }
                    DebugFetch("Request " + requestType + "(^b" + name + "^n): " + err);
                }
                DebugFetch("^2^bTIME^n took " + DateTime.Now.Subtract(since).TotalSeconds.ToString("F2") + " secs, cache lookup for ^b" + name);
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        private Boolean CheckSuccess(Hashtable json, out String err)
        {

            if (json == null)
            {
                err = "JSON response is null!";
                return false;
            }

            if (!json.ContainsKey("type"))
            {
                err = "JSON response malformed: does not contain 'type'!";
                return false;
            }

            String type = (String)json["type"];

            if (type == null)
            {
                err = "JSON response malformed: 'type' is null!";
                return false;
            }

            if (Regex.Match(type, @"success", RegexOptions.IgnoreCase).Success)
            {
                err = null;
                return true;
            }

            if (!json.ContainsKey("message"))
            {
                err = "JSON response malformed: does not contain 'message'!";
                return false;
            }

            String message = (String)json["message"];

            if (message == null)
            {
                err = "JSON response malformed: 'message' is null!";
                return false;
            }

            err = "Cache fetch failed (type: " + type + ", message: " + message + ")!";
            return false;
        }

        private Boolean FetchWebPage(ref String result, String url)
        {
            Boolean ret = false;
            try
            {
                String ua = "Mozilla/5.0 (compatible; PRoCon 1; " + GetPluginName() + ")";
                // XXX String ua = "Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0; .NET CLR 3.5.30729)";
                if (DebugLevel >= 8) DebugFetch("Using user-agent: " + ua);

                DateTime since = DateTime.Now;

                result = url
                    .WithHeader("User-Agent", ua)
                    .GetStringAsync()
                    .Result;

                /* TESTS
                String testUrl = "http://status.savanttools.com/?code=";
                html_data = testUrl.WithHeader("User-Agent", ua).GetStringAsync().Result;
                */

                DebugFetch("^2^bTIME^n took " + DateTime.Now.Subtract(since).TotalSeconds.ToString("F2") + " secs, url: " + url);

                if (Regex.Match(result, @"that\s+page\s+doesn't\s+exist", RegexOptions.IgnoreCase | RegexOptions.Singleline).Success)
                {
                    DebugFetch("^b" + url + "^n does not exist", 3);
                    result = String.Empty;
                    return false;
                }

                ret = true;
            }
            catch (FlurlHttpException e)
            {
                if (DebugLevel >= 3 && DebugLevel < 7) DebugFetch("FAILED for url: " + url, 3);
                if (e.InnerException is TaskCanceledException)
                {
                    if (DebugLevel >= 3) DebugFetch("WEB EXCEPTION: HTTP request timed-out", 3);
                }
                else
                {
                    if (DebugLevel >= 3) DebugFetch("WEB EXCEPTION: " + e.Message, 3);
                }
                DebugWrite("Full exception: " + e.ToString(), 7);
                ret = false;
            }
            catch (Exception ae)
            {
                if (DebugLevel >= 3 && DebugLevel < 7) DebugFetch("FAILED for url: " + url, 3);
                if (DebugLevel >= 3) DebugFetch("EXCEPTION: " + ae.Message, 3);
                DebugWrite("Full exception: " + ae.ToString(), 7);
                ret = false;
            }
            return ret;
        }

    } // end MULTIbalancer

} // end namespace PRoConEvents
