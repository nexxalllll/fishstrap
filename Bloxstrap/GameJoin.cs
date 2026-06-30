using System.Web;

namespace Bloxstrap
{
    public class GameJoin
    {
        private long RegexMatchLong(string url, string query, string pattern)
        {
            Match match = Regex.Match(url, query + pattern);

            if (!match.Success)
                return 0;

            long.TryParse(match.Groups[1].Value, out long result);

            return result;
        }

        private string RegexMatchString(string url, string query, string pattern)
        {
            Match match = Regex.Match(url, query + pattern);

            if (!match.Success)
                return string.Empty;

            return match.Groups[1].Value;
        }

        public GameJoinData GetJoinDataByLaunchCommand(string launchCommandLine)
        {
            const string LOG_IDENT = "Bootstrapper::GetJoinDataByLaunchCommand";

            const string placelauncherPattern = @"placelauncherurl:(.+?)(\+|$)";
            const string requestTypePattern = @"request=(.+?)&";
            const string commonIntPattern = @"([0-9]+)";
            const string commonIdPattern = @"([a-zA-Z0-9-]+?)(&|\+|$)";

            string? url = null;
            string rawPlaceLancherUrl = null!;
            GameJoinData joinData = new(); // by default its unknown

            if (TryParseExperienceStartDeeplink(launchCommandLine, joinData))
            {
                App.Logger.WriteLine(LOG_IDENT, $"Join type: {joinData.JoinType}");
                return joinData;
            }

            if (!launchCommandLine.StartsWith("roblox-player:"))
                return joinData; // its either empty or a launch format we don't handle

            Match urlMatch = Regex.Match(launchCommandLine, placelauncherPattern);
            if (!urlMatch.Success || urlMatch.Groups.Count != 3) return joinData; // the regex failed

            rawPlaceLancherUrl = urlMatch.Groups[1].Value;
            joinData.PlaceLauncherUrl = rawPlaceLancherUrl;

            url = HttpUtility.UrlDecode(rawPlaceLancherUrl);
            if (string.IsNullOrEmpty(url)) return joinData;

            Match typeMatch = Regex.Match(url, requestTypePattern);
            if (!typeMatch.Success || typeMatch.Groups.Count != 2) return joinData;

            App.Logger.WriteLine(LOG_IDENT, "Detecting join type");

            // yuck
            switch (typeMatch.Groups[1].Value)
            {
                case "RequestGame":
                    {
                        joinData.JoinType = GameJoinType.RequestGame;

                        string joinOrigin = RegexMatchString(url, "joinAttemptOrigin=", commonIdPattern);
                        long placeId = RegexMatchLong(url, "placeId=", commonIntPattern);

                        if (placeId == 0) return joinData;

                        joinData.PlaceId = placeId;
                        joinData.JoinOrigin = joinOrigin;
                        break;
                    }
                case "RequestGameJob":
                    {
                        joinData.JoinType = GameJoinType.RequestGameJob;

                        string joinOrigin = RegexMatchString(url, "joinAttemptOrigin=", commonIdPattern);
                        string jobId = RegexMatchString(url, "gameId=", commonIdPattern);
                        long placeId = RegexMatchLong(url, "placeId=", commonIntPattern);

                        if (string.IsNullOrEmpty(jobId) || placeId == 0) return joinData;

                        joinData.PlaceId = placeId;
                        joinData.JobId = jobId;
                        joinData.JoinOrigin = joinOrigin;
                        break;
                    }
                case "RequestPrivateGame":
                    {
                        joinData.JoinType = GameJoinType.RequestPrivateGame;

                        string accessCode = RegexMatchString(url, "accessCode=", commonIdPattern);
                        long placeId = RegexMatchLong(url, "placeId=", commonIntPattern);

                        if (string.IsNullOrEmpty(accessCode) || placeId == 0) return joinData;

                        joinData.PlaceId = placeId;
                        joinData.AccessCode = accessCode;
                        break;
                    }
                case "RequestFollowUser":
                    {
                        joinData.JoinType = GameJoinType.RequestFollowUser;

                        long userId = RegexMatchLong(url, "userId=", commonIntPattern);

                        if (userId == 0) return joinData;

                        joinData.UserId = userId;
                        break;
                    }
                case "RequestPlayTogetherGame":
                    {
                        joinData.JoinType = GameJoinType.RequestPlayTogetherGame;

                        long placeId = RegexMatchLong(url, "placeId=", commonIntPattern);
                        string conversationId = RegexMatchString(url, "conversationId=", commonIdPattern);

                        if (string.IsNullOrEmpty(conversationId) || placeId == 0) return joinData;

                        joinData.PlaceId = placeId;
                        joinData.JobId = conversationId;
                        break;
                    }
            }

            App.Logger.WriteLine(LOG_IDENT, $"Join type: {joinData.JoinType}");

            return joinData;
        }

        private static bool TryParseExperienceStartDeeplink(string launchCommandLine, GameJoinData joinData)
        {
            const string launchPrefix = "roblox://experiences/start";

            if (!launchCommandLine.StartsWith(launchPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            string decodedLaunchCommand = WebUtility.UrlDecode(launchCommandLine);

            if (!Uri.TryCreate(decodedLaunchCommand, UriKind.Absolute, out Uri? uri))
                return false;

            var query = HttpUtility.ParseQueryString(uri.Query);

            if (!long.TryParse(query["placeId"], out long placeId) || placeId == 0)
                return false;

            string? gameInstanceId = query["gameInstanceId"] ?? query["gameId"];
            string? accessCode = query["accessCode"] ?? query["privateServerLinkCode"];

            joinData.PlaceLauncherUrl = decodedLaunchCommand;
            joinData.PlaceId = placeId;

            if (!String.IsNullOrEmpty(accessCode))
            {
                joinData.JoinType = GameJoinType.RequestPrivateGame;
                joinData.AccessCode = accessCode;
            }
            else if (!String.IsNullOrEmpty(gameInstanceId))
            {
                joinData.JoinType = GameJoinType.RequestGameJob;
                joinData.JobId = gameInstanceId;
            }
            else
            {
                joinData.JoinType = GameJoinType.RequestGame;
            }

            return true;
        }
    }
}
