using System.ComponentModel;
using ModelContextProtocol.Server;
using PostgresMcpServer.Services;

namespace PostgresMcpServer.Tools;

[McpServerToolType]
public class PostgresTools
{
    [McpServerTool, Description("""
        Executes a read-only SQL query and returns results formatted as a table.
        Use this tool for any SELECT or introspection query.
        The query runs inside a READ ONLY transaction for safety.
        """)]
    public async Task<string> Query(
        PostgresService db,
        [Description("SQL SELECT query to execute")] string sql,
        [Description("Maximum number of rows to return (default: 100)")]
        int? maxRows = null,
        CancellationToken ct = default)
        => await db.Query(sql, maxRows, ct);

    [McpServerTool, Description("""
        Executes a SQL statement that modifies data or schema: INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, TRUNCATE, etc.
        Returns the number of rows affected or a success message.
        WARNING: this tool can cause irreversible changes. Use with care.
        """)]
    public async Task<string> Execute(
        PostgresService db,
        [Description("SQL statement to execute (INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, etc.)")] string sql,
        CancellationToken ct = default)
        => await db.Execute(sql, ct);
}
