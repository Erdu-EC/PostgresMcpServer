using ModelContextProtocol;
using Npgsql;
using System.Data;
using System.Text;
using System.Text.Json;

namespace PostgresMcpServer.Services;

public sealed class PostgresService : IDisposable
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresService()
    {
        var connStr = Environment.GetEnvironmentVariable("PGCONNECTIONSTRING")
            ?? throw new InvalidOperationException(
                "PGCONNECTIONSTRING environment variable is not set. " +
                "Example: Host=localhost;Port=5432;Database=mydb;Username=user;Password=pass");

        _dataSource = new NpgsqlDataSourceBuilder(connStr).Build();
    }

    public void Dispose() => _dataSource.Dispose();

    public async Task<string> Query(string sql, int? maxRows, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await using var setReadOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", conn, tx);
        await setReadOnly.ExecuteNonQueryAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, conn, tx);

        try
        {
            var result = await BuildJsonResult(cmd, maxRows ?? 100, ct);
            await tx.RollbackAsync(ct);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new McpException(ex.Message);
        }
    }

    public async Task<string> Execute(string sql, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);

        try
        {
            var affected = await cmd.ExecuteNonQueryAsync(ct);

            using var ms = new MemoryStream();
            await using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();
            writer.WriteString("status", "ok");
            writer.WriteNumber("rowsAffected", affected);
            writer.WriteEndObject();
            await writer.FlushAsync(ct);
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new McpException(ex.Message);
        }
    }

    private static async Task<string> BuildJsonResult(NpgsqlCommand cmd, int maxRows, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.Default, ct);

        // Non-SELECT statements (e.g. CALL) that return no columns
        if (reader.FieldCount == 0)
            return """{"status":"ok","rows":[]}""";

        var columns = new string[reader.FieldCount];
        for (var i = 0; i < reader.FieldCount; i++)
            columns[i] = reader.GetName(i);

        using var ms = new MemoryStream();
        await using var writer = new Utf8JsonWriter(ms);

        writer.WriteStartObject();
        writer.WritePropertyName("rows");
        writer.WriteStartArray();

        var rowCount = 0;
        var truncated = false;

        while (await reader.ReadAsync(ct))
        {
            if (rowCount >= maxRows)
            {
                truncated = true;
                break;
            }

            writer.WriteStartObject();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                writer.WritePropertyName(columns[i]);
                if (reader.IsDBNull(i))
                    writer.WriteNullValue();
                else
                    WriteValue(writer, reader.GetValue(i));
            }
            writer.WriteEndObject();
            rowCount++;
        }

        writer.WriteEndArray();
        writer.WriteNumber("rowCount", rowCount);
        if (truncated)
        {
            writer.WriteBoolean("truncated", true);
            writer.WriteString("note", $"Results truncated at {maxRows} rows. Refine your query or increase maxRows.");
        }
        writer.WriteEndObject();
        await writer.FlushAsync(ct);

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    // Writes typed JSON values without reflection — AOT-safe.
    private static void WriteValue(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case bool v:            writer.WriteBooleanValue(v); break;
            case int v:             writer.WriteNumberValue(v); break;
            case long v:            writer.WriteNumberValue(v); break;
            case short v:           writer.WriteNumberValue(v); break;
            case float v:           writer.WriteNumberValue(v); break;
            case double v:          writer.WriteNumberValue(v); break;
            case decimal v:         writer.WriteNumberValue(v); break;
            case DateTime v:        writer.WriteStringValue(v.ToString("O")); break;
            case DateTimeOffset v:  writer.WriteStringValue(v.ToString("O")); break;
            case TimeSpan v:        writer.WriteStringValue(v.ToString("c")); break;
            case Guid v:            writer.WriteStringValue(v.ToString()); break;
            case byte[] v:          writer.WriteBase64StringValue(v); break;
            default:                writer.WriteStringValue(value.ToString()); break;
        }
    }
}
