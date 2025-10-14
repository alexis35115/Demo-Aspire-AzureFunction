using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

class Program
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

    static async Task<int> Main(string[] args)
    {
        try
        {
            var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Communication");
            if (string.IsNullOrWhiteSpace(cs))
            {
                Console.Error.WriteLine("Connection string 'Communication' not found in environment.");
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
            Console.Error.WriteLine($"Migration failed: {ex}");
            return 1;
        }
    }
}
