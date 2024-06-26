﻿using ScoreSaber.Core.Data;
using ScoreSaber.Core.Data.Wrappers;
using ScoreSaber.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ScoreSaber.Core.Utils {
    internal static class LeaderboardUtils {

        internal static bool LocalReplayExists(BeatmapLevel beatmapLevel, BeatmapKey beatmapKey, ScoreMap score) {

            if (File.Exists(GetReplayPath(beatmapLevel, beatmapKey, score))) {
                return true;
            }

            if (File.Exists(GetLegacyReplayPath(beatmapLevel, beatmapKey, score))) {
                return true;
            }
            return false;
        }

        internal static string GetReplayPath(BeatmapLevel beatmapLevel, BeatmapKey beatmapKey, ScoreMap scoreMap) {
            return $@"{Settings.replayPath}\{scoreMap.score.leaderboardPlayerInfo.id}-{beatmapLevel.songName.ReplaceInvalidChars().Truncate(155)}-{beatmapKey.difficulty.SerializedName()}-{beatmapKey.beatmapCharacteristic.serializedName}-{scoreMap.parent.songHash}.dat";
        }

        internal static string GetLegacyReplayPath(BeatmapLevel beatmapLevel, BeatmapKey beatmapKey, ScoreMap scoreMap) {
            return $@"{Settings.replayPath}\{scoreMap.score.leaderboardPlayerInfo.id}-{beatmapLevel.songName.ReplaceInvalidChars().Truncate(155)}-{scoreMap.parent.songHash}.dat";
        }
         
        internal static string GetFormattedName(ScoreMap scoreMap) {

            bool hasMods = !string.IsNullOrEmpty(scoreMap.score.modifiers);

            string name = $"<size=80%>{scoreMap.score.leaderboardPlayerInfo.name}</size>";
            string accuracy = $"<size=70%>(<color=#FFD42A>{scoreMap.accuracy}%</color>)</size>";
            string pp = $"<size=70%>(<color=#6772E5>{scoreMap.score.pp}<size=45%>pp</size></color>)</size>";
            string modifiers = $"<size=70%><color=#6F6F6F>[{scoreMap.score.modifiers}]</color></size>";

            string formattedName = $"{name} - {accuracy}";

            if (scoreMap.score.pp > 0 && Plugin.Settings.showScorePP) {
                formattedName = $"{formattedName} - {pp}";
            }

            if (hasMods) {
                formattedName = $"{formattedName} {modifiers}";
            }

            return formattedName;
        }

        internal static Tuple<string, string> GetCrownDetails(string playerId) {
            switch (playerId) {
                case PlayerIDs.woops:
                    return new Tuple<string, string>("ScoreSaber.Resources.crown-bronze.png", "Beat Saber Invitational 3rd place");
                case PlayerIDs.Jones:
                    return new Tuple<string, string>("ScoreSaber.Resources.crown-silver.png", "Beat Saber Invitational 2nd place");
                case PlayerIDs.Umbranox:
                    return new Tuple<string, string>("ScoreSaber.Resources.crown-umby.png", "Owner of ScoreSaber");
                case PlayerIDs.Rain:
                    return new Tuple<string, string>("ScoreSaber.Resources.crown-rain.png", "Owner of Umbranox's heart");
            }
            return new Tuple<string, string>("", "");
        }

        internal static GameplayModifiersMap GetModifierFromStrings(string[] modifiers, bool isPositiveModifiersEnabled) {

            double totalMultiplier = 1;
            var energyType = GameplayModifiers.EnergyType.Bar;
            var obstacleType = GameplayModifiers.EnabledObstacleType.All;
            var songSpeed = GameplayModifiers.SongSpeed.Normal;
            bool NF = false;
            bool IF = false;
            bool NB = false;
            bool DA = false;
            bool GN = false;
            bool NA = false;
            bool PM = false;
            bool SC = false;
            bool SA = false;
            foreach (string modifier in modifiers) {
                switch (modifier) {
                    case "BE":
                        totalMultiplier += 0;
                        energyType = GameplayModifiers.EnergyType.Battery;
                        break;
                    case "NF":
                        totalMultiplier += -0.5;
                        NF = true;
                        break;
                    case "IF":
                        totalMultiplier += 0;
                        IF = true;
                        break;
                    case "NO":
                        totalMultiplier += -0.05;
                        obstacleType = GameplayModifiers.EnabledObstacleType.NoObstacles;
                        break;
                    case "NB":
                        totalMultiplier += -0.10;
                        NB = true;
                        break;
                    case "DA":
                        if (isPositiveModifiersEnabled) {
                            totalMultiplier += 0.02;
                        }
                        DA = true;
                        break;
                    case "GN":
                        if (isPositiveModifiersEnabled) {
                            totalMultiplier += 0.04;
                        }
                        GN = true;
                        break;
                    case "NA":
                        totalMultiplier += 0;
                        NA = true;
                        break;
                    case "SS":
                        totalMultiplier += -0.3;
                        songSpeed = GameplayModifiers.SongSpeed.Slower;
                        break;
                    case "FS":
                        if (isPositiveModifiersEnabled) {
                            totalMultiplier += 0.08;
                        }
                        songSpeed = GameplayModifiers.SongSpeed.Faster;
                        break;
                    case "SF":
                        songSpeed = GameplayModifiers.SongSpeed.SuperFast;
                        break;
                    case "PM":
                        PM = true;
                        break;
                    case "SC":
                        SC = true;
                        break;
                    case "SA":
                        SA = true;
                        break;
                }
            }
            GameplayModifiers gameplayModifiers = new GameplayModifiers(energyType, NF, IF, false, obstacleType, NB, false, SA, DA, songSpeed, NA, GN, PM, false, SC);
            GameplayModifiersMap gameplayModifiersWrapper = new GameplayModifiersMap(gameplayModifiers);
            gameplayModifiersWrapper.totalMultiplier = totalMultiplier;
            return gameplayModifiersWrapper;
        }

        public static int OldMaxRawScoreForNumberOfNotes(int noteCount) {
            int num = 0;
            int num2 = 1;
            while (num2 < 8) {
                if (noteCount >= num2 * 2) {
                    num += num2 * num2 * 2 + num2;
                    noteCount -= num2 * 2;
                    num2 *= 2;
                    continue;
                }
                num += num2 * noteCount;
                noteCount = 0;
                break;
            }
            num += noteCount * num2;
            return num * 115;
        }

        internal static List<string> GetModifierList(LevelCompletionResults results) {
            List<string> result = new List<string>();
            if (results.gameplayModifiers.energyType == GameplayModifiers.EnergyType.Battery) {
                result.Add("BE");
            }
            if (results.gameplayModifiers.noFailOn0Energy && results.energy == 0) {
                result.Add("NF");
            }
            if (results.gameplayModifiers.instaFail) {
                result.Add("IF");
            }
            if (results.gameplayModifiers.failOnSaberClash) {
                result.Add("SC");
            }
            if (results.gameplayModifiers.enabledObstacleType == GameplayModifiers.EnabledObstacleType.NoObstacles) {
                result.Add("NO");
            }
            if (results.gameplayModifiers.noBombs) {
                result.Add("NB");
            }
            if (results.gameplayModifiers.strictAngles) {
                result.Add("SA");
            }
            if (results.gameplayModifiers.disappearingArrows) {
                result.Add("DA");
            }
            if (results.gameplayModifiers.ghostNotes) {
                result.Add("GN");
            }
            if (results.gameplayModifiers.songSpeed == GameplayModifiers.SongSpeed.Slower) {
                result.Add("SS");
            }
            if (results.gameplayModifiers.songSpeed == GameplayModifiers.SongSpeed.Faster) {
                result.Add("FS");
            }
            if (results.gameplayModifiers.songSpeed == GameplayModifiers.SongSpeed.SuperFast) {
                result.Add("SF");
            }
            if (results.gameplayModifiers.smallCubes) {
                result.Add("SC");
            }
            if (results.gameplayModifiers.proMode) {
                result.Add("PM");
            }
            if (results.gameplayModifiers.noArrows) {
                result.Add("NA");
            }
            return result;
        }
    }
}
