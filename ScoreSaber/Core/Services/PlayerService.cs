using Newtonsoft.Json;
using ScoreSaber.Core.Data;
using ScoreSaber.Core.Data.Models;
using ScoreSaber.Core.Data.Wrappers;
using ScoreSaber.Extensions;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ScoreSaber.Core.Services {
    internal class PlayerService {

        public LocalPlayerInfo localPlayerInfo { get; set; } // localPlayerInfo get/set method which needs (String _playerId, String _playerName, String _playerFriends, String _authtype, String _playerNonce)
        public LoginStatus loginStatus { get; set; } // loginStatus get/set method which needs (LoginStatus _loginstatus) which would be something like (LoginStatus.Error) cause you are getting the value from the enum defined 2 lines below
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
                SignIn().RunTask();  //calls RunTask from SignIn method below
            }
        }

        private async Task SignIn() { //spooky async programing...

            ChangeLoginStatus(LoginStatus.Info, "Signing into ScoreSaber..."); //calls ChangeLoginStatus (line 29) sets to Info and a string

            //vars for things like authToken.this or userInfo.that
            var platformUserModel = Plugin.Container.TryResolve<IPlatformUserModel>(); // mystery, eh i'll contine tommorrow
            var authToken = await platformUserModel.GetUserAuthToken();
            var userInfo = await platformUserModel.GetUserInfo(CancellationToken.None);

            var nonce = string.Empty;
            var platform = string.Empty;

            switch (userInfo.platform) {
                case UserInfo.Platform.Steam:
                    nonce = authToken.token;
                    platform = "0";
                    break;
                case UserInfo.Platform.Oculus:
                    nonce = authToken.token + "," + (await platformUserModel.RequestXPlatformAccessToken(CancellationToken.None)).token;
                    platform = "1";
                    break;
            }

            var playerId = userInfo.platformUserId;
            var playerName = userInfo.userName;
            var friendIds = await platformUserModel.GetUserFriendsUserIds(false);
            var friends = string.Join(",", friendIds.Where(x => x != "0"));

            var playerInfo = new LocalPlayerInfo(playerId, playerName, friends, platform, nonce);

            int attempts = 1;

            while (attempts < 4) {

                var authenticated = await AuthenticateWithScoreSaber(playerInfo);

                if (authenticated) {
                    localPlayerInfo = playerInfo;
                    string successText = "Successfully signed into ScoreSaber!";
                    if (localPlayerInfo.playerId == PlayerIDs.Denyah)
                        successText = "Wagwan piffting wots ur bbm pin?";

                    ChangeLoginStatus(LoginStatus.Success, successText);
                    break;
                } else {
                    ChangeLoginStatus(LoginStatus.Error, $"Failed, attempting again ({attempts} of 3 tries...)");
                    attempts++;
                    await Task.Delay(4000);
                }

            }

            if (loginStatus != LoginStatus.Success) {
                ChangeLoginStatus(LoginStatus.Error, "Failed to authenticate with ScoreSaber! Please restart your game");
            }
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

        public async Task<byte[]> GetReplayData(BeatmapLevel beatmapLevel, BeatmapKey beatmapKey, int leaderboardId, ScoreMap scoreMap) {

            if (scoreMap.hasLocalReplay) {
                string replayPath = GetReplayPath(scoreMap.parent.songHash, beatmapKey.difficulty.SerializedName(), beatmapKey.beatmapCharacteristic.serializedName, scoreMap.score.leaderboardPlayerInfo.id, beatmapLevel.songName);
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
