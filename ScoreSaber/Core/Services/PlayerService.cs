using Newtonsoft.Json;
using ScoreSaber.Core.Data;
using ScoreSaber.Core.Data.Models;
using ScoreSaber.Core.Data.Wrappers;
using ScoreSaber.Extensions;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ScoreSaber.Core.Services {
    internal class PlayerService {

        public LocalPlayerInfo localPlayerInfo { get; set; } //localPlayerInfo get/set method which needs (String _playerId, String _playerName, String _playerFriends, String _authtype, String _playerNonce)
        public LoginStatus loginStatus { get; set; } //loginStatus get/set method which needs (LoginStatus _loginstatus) which would be something like (LoginStatus.Error) cause you are getting the value from the enum defined 2 lines below
        public event Action<LoginStatus, string> LoginStatusChanged; //define event LoginStatusChanged, delegate also defined that takes LoginStatus Enum and String that passes its info to the event I guess?
        public enum LoginStatus {
            Info = 0,
            Error = 1,
            Success = 2
        } //defining enum, for example if enum is 2 login was a succes

        public PlayerService() {
            Plugin.Log.Debug("PlayerService Setup!"); //log setup of this
        }

        public void ChangeLoginStatus(LoginStatus _loginStatus, string status) {

            loginStatus = _loginStatus; //set the loginStatus variable to func variable which would be LoginStatus enum (like LoginStatus.Error)
            LoginStatusChanged?.Invoke(loginStatus, status); //Fire LoginStatusChanged event (line 18) after checking null (?.) and send LoginStatus enum (like LoginStatus.Error) and description of error string
        }

        public void GetLocalPlayerInfo() {

            if (localPlayerInfo == null) { //check if localPlayerInfo variable null
                if (File.Exists(Path.Combine(IPA.Utilities.UnityGame.InstallPath, "Beat Saber_Data", "Plugins", "x86_64", "steam_api64.dll"))) {  //if steam api exsits then use auth 1, gets install path from IPA
                    GetLocalPlayerInfo1().RunTask(); //auth with steam (line 46)
                } else { //else no steam api
                    GetLocalPlayerInfo2(); //auth with oculus (line 81)
                }
            } //else do nothing
        }

        private async Task GetLocalPlayerInfo1() {

            ChangeLoginStatus(LoginStatus.Info, "Signing into ScoreSaber..."); //changes login status (line 29)

            int attempts = 1; //sets variable attempts to 1

            while (attempts < 4) { //try 3 times cause counter "attempts will increase"
                LocalPlayerInfo steamInfo = await GetLocalSteamInfo(); //new LocalPlayerInfo variable set to what GetLocalSteamInfo (line 120) returns
                if (steamInfo != null) {
                    Plugin.Log.Info("Name: " + steamInfo.playerName);
                    Plugin.Log.Info("Nonce: " + steamInfo.playerNonce);
                    Plugin.Log.Info("Auth Type: " + steamInfo.authType);
                    Plugin.Log.Info("ID: " + steamInfo.playerId);
                    Plugin.Log.Info("Friends: " + steamInfo.playerFriends);
                    bool authenticated = await AuthenticateWithScoreSaber(steamInfo);
                    if (authenticated) {
                        localPlayerInfo = steamInfo;
                        string successText = "Successfully signed into ScoreSaber!";
                        if (localPlayerInfo.playerId == PlayerIDs.Denyah) {
                            successText = "Wagwan piffting wots ur bbm pin?";
                        }
                        ChangeLoginStatus(LoginStatus.Success, successText);
                        break;
                    } else {
                        ChangeLoginStatus(LoginStatus.Error, $"Failed, attempting again ({attempts} of 3 tries...)");
                        attempts++; //attempts increase
                        await Task.Delay(4000); //delay to look cool and probably wait for internet
                    }
                } else {
                    Plugin.Log.Error("Steamworks is not initialized!");
                    ChangeLoginStatus(LoginStatus.Error, "Failed to authenticate! Error getting steam info");
                    break;
                }
            }

            if (loginStatus != LoginStatus.Success) {
                ChangeLoginStatus(LoginStatus.Error, "Failed to authenticate with ScoreSaber! Please restart your game");
            }
        }

        private void GetLocalPlayerInfo2() {

            ChangeLoginStatus(LoginStatus.Info, "Signing into ScoreSaber...");

            Oculus.Platform.Users.GetLoggedInUser().OnComplete(delegate (Oculus.Platform.Message<Oculus.Platform.Models.User> loggedInMessage) {
                if (!loggedInMessage.IsError) {
                    Oculus.Platform.Users.GetLoggedInUserFriends().OnComplete(delegate (Oculus.Platform.Message<Oculus.Platform.Models.UserList> friendsMessage) {
                        if (!friendsMessage.IsError) {
                            Oculus.Platform.Users.GetUserProof().OnComplete(delegate (Oculus.Platform.Message<Oculus.Platform.Models.UserProof> userProofMessage) {
                                if (!userProofMessage.IsError) {
                                    Oculus.Platform.Users.GetAccessToken().OnComplete(async delegate (Oculus.Platform.Message<string> authTokenMessage) {
                                        string playerId = loggedInMessage.Data.ID.ToString();
                                        string playerName = loggedInMessage.Data.OculusID;
                                        string friends = playerId + ",";
                                        string nonce = userProofMessage.Data.Value + "," + authTokenMessage.Data;
                                        LocalPlayerInfo oculusInfo = new LocalPlayerInfo(playerId, playerName, friends, "1", nonce);
                                        bool authenticated = await AuthenticateWithScoreSaber(oculusInfo);
                                        if (authenticated) {
                                            localPlayerInfo = oculusInfo;
                                            ChangeLoginStatus(LoginStatus.Success, "Successfully signed into ScoreSaber!");
                                        } else {
                                            ChangeLoginStatus(LoginStatus.Error, "Failed to authenticate with ScoreSaber! Please restart your game");
                                        }
                                    });
                                   
                                } else {
                                    ChangeLoginStatus(LoginStatus.Error, "Failed to authenticate! Error getting oculus info");
                                }
                            });
                        } else {
                            ChangeLoginStatus(LoginStatus.Error, "Failed to authenticate! Error getting oculus info");
                        }
                    });
                } else {
                    ChangeLoginStatus(LoginStatus.Error, "Failed to authenticate! Error getting oculus info");
                }
            });
        }

        private async Task<LocalPlayerInfo> GetLocalSteamInfo() {

            await TaskEx.WaitUntil(() => SteamManager.Initialized);  //wait untill steamManager gets initilised or timeout

            string authToken = (await new SteamPlatformUserModel().GetUserAuthToken()).token;  //set authToken to token from steam

            LocalPlayerInfo steamInfo = await Task.Run(() => { //wait for this to happen until timeout
                Steamworks.CSteamID steamID = Steamworks.SteamUser.GetSteamID();
                string playerId = steamID.m_SteamID.ToString();
                string playerName = Steamworks.SteamFriends.GetPersonaName();
                string friends = playerId + ",";
                for (int i = 0; i < Steamworks.SteamFriends.GetFriendCount(Steamworks.EFriendFlags.k_EFriendFlagAll); i++) {
                    Steamworks.CSteamID friendSteamId = Steamworks.SteamFriends.GetFriendByIndex(i, Steamworks.EFriendFlags.k_EFriendFlagImmediate);
                    if (friendSteamId.m_SteamID.ToString() != "0") {
                        friends = friends + friendSteamId.m_SteamID.ToString() + ",";
                    }
                }
                friends = friends.Remove(friends.Length - 1);
                return new LocalPlayerInfo(playerId, playerName, friends, "0", authToken);
            });


            return steamInfo;
        }

        private async Task<bool> AuthenticateWithScoreSaber(LocalPlayerInfo playerInfo) {


            if (Plugin.HttpInstance.PersistentRequestHeaders.ContainsKey("Cookies")) {
                Plugin.HttpInstance.PersistentRequestHeaders.Remove("Cookies");
            }

            WWWForm form = new WWWForm();
            form.AddField("at", playerInfo.authType);
            form.AddField("playerId", playerInfo.playerId);
            form.AddField("nonce", playerInfo.playerNonce);
            form.AddField("friends", playerInfo.playerFriends);
            form.AddField("name", playerInfo.playerName);

            try {
                string response = await Plugin.HttpInstance.PostAsync("/game/auth", form);
                var authResponse = JsonConvert.DeserializeObject<AuthResponse>(response);
                playerInfo.playerKey = "76561198425334981";
                playerInfo.serverKey = "e4eb245dda2484b";

                Plugin.HttpInstance.PersistentRequestHeaders.Add("Cookies", $"connect.sid={playerInfo.serverKey}");
                return true;
            } catch (Exception ex) {
                Plugin.Log.Error($"Failed user authentication: {ex.Message}");
                return false;
            }
        }

        public async Task<PlayerInfo> GetPlayerInfo(string playerId, bool full) {

            string url = $"/player/{playerId}";

            if (full) {
                url += "/full";
            } else {
                url += "/basic";
            }

            var response = await Plugin.HttpInstance.GetAsync(url);
            var playerStats = JsonConvert.DeserializeObject<PlayerInfo>(response);
            return playerStats;
        }

        public async Task<byte[]> GetReplayData(IDifficultyBeatmap level, int leaderboardId, ScoreMap scoreMap) {

            if (scoreMap.hasLocalReplay) {
                string replayPath = GetReplayPath(scoreMap.parent.songHash, level.difficulty.SerializedName(), level.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName, scoreMap.score.leaderboardPlayerInfo.id, level.level.songName);
                if (replayPath != null) {
                    return File.ReadAllBytes(replayPath);
                }
            }

            byte[] response = await Plugin.HttpInstance.DownloadAsync($"/game/telemetry/downloadReplay?playerId={scoreMap.score.leaderboardPlayerInfo.id}&leaderboardId={leaderboardId}");

            if (response != null) {
                return response;
            } else {
                throw new Exception("Failed to download replay");
            }
        }

        private string GetReplayPath(string levelId, string difficultyName, string characteristic, string playerId, string songName) {

            songName = songName.ReplaceInvalidChars().Truncate(155);

            string path = $@"{Settings.replayPath}\{playerId}-{songName}-{difficultyName}-{characteristic}-{levelId}.dat";
            if (File.Exists(path)) {
                return path;
            }

            string legacyPath = $@"{Settings.replayPath}\{playerId}-{songName}-{levelId}.dat";
            if (File.Exists(legacyPath)) {
                return legacyPath;
            }

            return null;
        }

    }
}
