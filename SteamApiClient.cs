using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GamingTimeTracker;

internal sealed class SteamApiClient(HttpClient httpClient)
{
		private const string RecentlyPlayedGamesEndpoint =
			"https://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v1/";

		public async Task<RecentlyPlayedGamesResponse> GetRecentlyPlayedGamesAsync(
			string apiKey,
			string steamId64,
			CancellationToken cancellationToken = default)
		{
			var requestUri = $"{RecentlyPlayedGamesEndpoint}?key={Uri.EscapeDataString(apiKey)}&steamid={Uri.EscapeDataString(steamId64)}&format=json";
			using var response = await httpClient.GetAsync(requestUri, cancellationToken);
			response.EnsureSuccessStatusCode();

			return await response.Content.ReadFromJsonAsync<RecentlyPlayedGamesResponse>(cancellationToken: cancellationToken)
				?? throw new JsonException("Steam returned an empty response.");
		}
	}

	internal sealed class RecentlyPlayedGamesResponse
	{
		[JsonPropertyName("response")]
		public RecentlyPlayedGamesData? Response { get; init; }
	}

	internal sealed class RecentlyPlayedGamesData
	{
		[JsonPropertyName("games")]
		public List<RecentlyPlayedGame>? Games { get; init; }
	}

	internal sealed class RecentlyPlayedGame
	{
		[JsonPropertyName("playtime_2weeks")]
		public long PlaytimeTwoWeeks { get; init; }
	}

