using Microsoft.Data.SqlClient;

internal class Program
{
    private const string CreateTableSql = @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Communication]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Communication](
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [Type] NVARCHAR(50) NOT NULL,
        [Destination] NVARCHAR(256) NOT NULL,
        [Subject] NVARCHAR(256) NULL,
        [Body] NVARCHAR(MAX) NULL,
        [RequestedAtUtc] DATETIMEOFFSET NOT NULL
    );
END";

    private static async Task<int> Main()
    {
        try
        {
            var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Communication");
            if (string.IsNullOrWhiteSpace(cs))
            {
                await Console.Error.WriteLineAsync("Connection string 'Communication' not found in environment.");
                return 2;
            }

            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand(CreateTableSql, conn);
            await cmd.ExecuteNonQueryAsync();

            Console.WriteLine("Database migration completed (Communication table ensured).");
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Migration failed: {ex}");
            return 1;
        }
    }
}
