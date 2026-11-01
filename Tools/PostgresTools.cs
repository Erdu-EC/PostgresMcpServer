using System.ComponentModel;
using System.Data;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using Npgsql;

namespace PostgresMcpServer.Tools;

/// <summary>
/// MCP tools for querying and executing commands against a PostgreSQL database.
/// Connection string is read from the <c>PGCONNECTIONSTRING</c> environment variable.
/// </summary>
internal class PostgresTools
{
    private static string GetConnectionString()
    {
        var connStr = Environment.GetEnvironmentVariable("PGCONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(connStr))
        {
            throw new InvalidOperationException(
                "PGCONNECTIONSTRING environment variable is not set. " +
                "Example: Host=localhost;Port=5432;Database=mydb;Username=user;Password=pass;");
        }

        return connStr;
    }

    private const int DefaultMaxRows = 100;

    [McpServerTool]
    [Description("Execute a read-only SQL query and return results as JSON. Use for SELECT statements.")]
    public static string Query(
        [Description("SQL SELECT query to execute")]
        string query,
        [Description("Maximum number of rows to return (default: 100)")]
        int? maxRows = null)
    {
        using var conn = new NpgsqlConnection(GetConnectionString());
        conn.Open();

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.CommandType = CommandType.Text;
        cmd.CommandTimeout = 30;

        using var reader = cmd.ExecuteReader();

        var columnCount = reader.FieldCount;
        var columns = new string[columnCount];
        for (var i = 0; i < columnCount; i++)
        {
            columns[i] = reader.GetName(i);
        }

        var limit = maxRows ?? DefaultMaxRows;
        var buffer = new MemoryStream();
        using var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false });
        writer.WriteStartArray();

        var count = 0;
        while (reader.Read() && count < limit)
        {
            writer.WriteStartObject();
            for (var i = 0; i < columnCount; i++)
            {
                var value = reader.GetValue(i);
                WriteJsonValue(writer, columns[i], value);
            }

            writer.WriteEndObject();
            count++;
        }

        writer.WriteEndArray();
        writer.Flush();
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    [McpServerTool]
    [Description(
        "Execute a SQL command that modifies data (INSERT, UPDATE, DELETE, CREATE, ALTER, DROP). Returns rows affected.")]
    public static string Execute(
        [Description("SQL command to execute")]
        string query)
    {
        using var conn = new NpgsqlConnection(GetConnectionString());
        conn.Open();

        using var cmd = new NpgsqlCommand(query, conn);
        cmd.CommandType = CommandType.Text;
        cmd.CommandTimeout = 30;

        var rowsAffected = cmd.ExecuteNonQuery();
        return $"Rows affected: {rowsAffected}";
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, string name, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNull(name);
                break;
            case bool b:
                writer.WriteBoolean(name, b);
                break;
            case string s:
                writer.WriteString(name, s);
                break;
            case byte b:
                writer.WriteNumber(name, b);
                break;
            case sbyte sb:
                writer.WriteNumber(name, sb);
                break;
            case short s:
                writer.WriteNumber(name, s);
                break;
            case ushort us:
                writer.WriteNumber(name, us);
                break;
            case int i:
                writer.WriteNumber(name, i);
                break;
            case uint ui:
                writer.WriteNumber(name, ui);
                break;
            case long l:
                writer.WriteNumber(name, l);
                break;
            case ulong ul:
                writer.WriteNumber(name, ul);
                break;
            case float f:
                writer.WriteNumber(name, f);
                break;
            case double d:
                writer.WriteNumber(name, d);
                break;
            case decimal dc:
                writer.WriteNumber(name, dc);
                break;
            case DateTime dt:
                writer.WriteString(name, dt.ToString("yyyy-MM-ddTHH:mm:ss.fff"));
                break;
            case byte[] bytes:
                writer.WriteString(name, Convert.ToBase64String(bytes));
                break;
            case Guid g:
                writer.WriteString(name, g.ToString());
                break;
            case TimeSpan ts:
                writer.WriteString(name, ts.ToString("c"));
                break;
            case DateTimeOffset dto:
                writer.WriteString(name, dto.ToString("o"));
                break;
            default:
                writer.WriteString(name, value.ToString() ?? "");
                break;
        }
    }
}
