using System.Net.Http;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
	.AddUserSecrets<Program>()
	.Build();

var apiKey = configuration["Steam:ApiKey"];
var steamId64 = configuration["Steam:SteamId64"];

if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(steamId64))
{
	Console.Error.WriteLine("Brak konfiguracji Steam w .NET User Secrets.");
	Console.Error.WriteLine("Ustaw wartości Steam:ApiKey i Steam:SteamId64 poleceniem dotnet user-secrets set.");
	Environment.ExitCode = 1;
	return;
}

if (!ulong.TryParse(steamId64, out _))
{
	Console.Error.WriteLine("Steam:SteamId64 musi być liczbowym identyfikatorem SteamID64.");
	Environment.ExitCode = 1;
	return;
}

using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
var steamApiClient = new SteamApiClient(httpClient);

try
{
	var response = await steamApiClient.GetRecentlyPlayedGamesAsync(apiKey, steamId64);
	var games = response.Response?.Games;
	if (games is not { Count: > 0 })
	{
		Console.WriteLine("Steam nie zwrócił gier z ostatnich 14 dni. Sprawdź widoczność szczegółów gier na profilu.");
		return;
	}

	var totalMinutes = games.Sum(game => game.PlaytimeTwoWeeks);
	var today = DateOnly.FromDateTime(DateTime.Today);
	var snapshotStore = new SnapshotStore();
	await snapshotStore.SaveSnapshotAsync(today, totalMinutes);

	Console.WriteLine($"Łączny czas gry z ostatnich 14 dni: {totalMinutes / 60.0:F2} godz.");
	Console.WriteLine($"Zapisano dzienny wynik do: {snapshotStore.DatabasePath}");
}
catch (HttpRequestException)
{
	Console.Error.WriteLine("Nie udało się pobrać danych ze Steam. Sprawdź klucz API, widoczność profilu i połączenie internetowe.");
	Environment.ExitCode = 1;
}
catch (TaskCanceledException)
{
	Console.Error.WriteLine("Przekroczono czas oczekiwania na odpowiedź Steam.");
	Environment.ExitCode = 1;
}
catch (JsonException)
{
	Console.Error.WriteLine("Steam zwrócił odpowiedź w nieoczekiwanym formacie.");
	Environment.ExitCode = 1;
}
catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
{
	Console.Error.WriteLine("Nie udało się zapisać wyniku w lokalnej bazie danych.");
	Environment.ExitCode = 1;
}
