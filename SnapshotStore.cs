using System.Globalization;
using Microsoft.Data.Sqlite;

internal sealed class SnapshotStore
{
	private readonly string _connectionString;

	public SnapshotStore()
	{
		var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		if (string.IsNullOrWhiteSpace(localAppData))
		{
			throw new IOException("Local application data directory is unavailable.");
		}

		var appDataDirectory = Path.Combine(localAppData, "GamingTimeTracker");
		Directory.CreateDirectory(appDataDirectory);
		DatabasePath = Path.Combine(appDataDirectory, "steam-playtime.db");
		_connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = DatabasePath,
			Mode = SqliteOpenMode.ReadWriteCreate
		}.ToString();
	}

	public string DatabasePath { get; }

	public async Task SaveSnapshotAsync(
		DateOnly snapshotDate,
		long playtimeMinutes,
		CancellationToken cancellationToken = default)
	{
		if (playtimeMinutes < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(playtimeMinutes));
		}

		await using var connection = new SqliteConnection(_connectionString);
		await connection.OpenAsync(cancellationToken);

		await using (var createTableCommand = connection.CreateCommand())
		{
			createTableCommand.CommandText = """
				CREATE TABLE IF NOT EXISTS SteamPlaytimeSnapshots (
					snapshot_date TEXT NOT NULL PRIMARY KEY,
					playtime_minutes INTEGER NOT NULL CHECK (playtime_minutes >= 0)
				);
				""";
			await createTableCommand.ExecuteNonQueryAsync(cancellationToken);
		}

		await using var saveCommand = connection.CreateCommand();
		saveCommand.CommandText = """
			INSERT INTO SteamPlaytimeSnapshots (snapshot_date, playtime_minutes)
			VALUES ($snapshotDate, $playtimeMinutes)
			ON CONFLICT (snapshot_date) DO UPDATE SET
				playtime_minutes = excluded.playtime_minutes;
			""";
		saveCommand.Parameters.AddWithValue("$snapshotDate", snapshotDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
		saveCommand.Parameters.AddWithValue("$playtimeMinutes", playtimeMinutes);
		await saveCommand.ExecuteNonQueryAsync(cancellationToken);
	}
}