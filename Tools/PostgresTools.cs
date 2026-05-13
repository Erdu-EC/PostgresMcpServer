using System.ComponentModel;
using ModelContextProtocol.Server;
using PostgresMcpServer.Services;

namespace PostgresMcpServer.Tools;

[McpServerToolType]
public class PostgresTools
{
    [McpServerTool, Description("Lists all non-system schemas in the database.")]
    public async Task<string> GetSchemas(PostgresService db, CancellationToken ct)
        => await db.GetSchemas(ct);

    [McpServerTool, Description("Lists all tables, optionally filtered by schema.")]
    public async Task<string> GetTables(
        PostgresService db,
        [Description("Schema name to filter by (optional)")]
        string? schema = null,
        CancellationToken ct = default)
        => await db.GetTables(schema, ct);

    [McpServerTool, Description("Lists all views, optionally filtered by schema.")]
    public async Task<string> GetViews(
        PostgresService db,
        [Description("Schema name to filter by (optional)")]
        string? schema = null,
        CancellationToken ct = default)
        => await db.GetViews(schema, ct);

    [McpServerTool, Description("Lists all functions and stored procedures, optionally filtered by schema.")]
    public async Task<string> GetFunctions(
        PostgresService db,
        [Description("Schema name to filter by (optional)")]
        string? schema = null,
        CancellationToken ct = default)
        => await db.GetFunctions(schema, ct);

    [McpServerTool, Description("Gets detailed column information for a table or view (name, type, nullable, default, description).")]
    public async Task<string> GetTableColumns(
        PostgresService db,
        [Description("Schema name")] string schema,
        [Description("Table or view name")] string table,
        CancellationToken ct = default)
        => await db.GetTableColumns(schema, table, ct);

    [McpServerTool, Description("Generates the full DDL (CREATE TABLE) for a table including columns, PKs, FKs, constraints, comments and indexes.")]
    public async Task<string> GetTableDdl(
        PostgresService db,
        [Description("Schema name")] string schema,
        [Description("Table name")] string table,
        CancellationToken ct = default)
        => await db.GetTableDdl(schema, table, ct);

    [McpServerTool, Description("Gets the SELECT definition of a view.")]
    public async Task<string> GetViewDefinition(
        PostgresService db,
        [Description("Schema name")] string schema,
        [Description("View name")] string view,
        CancellationToken ct = default)
        => await db.GetViewDefinition(schema, view, ct);

    [McpServerTool, Description("Gets the complete source code of a function or stored procedure.")]
    public async Task<string> GetFunctionSource(
        PostgresService db,
        [Description("Schema name")] string schema,
        [Description("Function or procedure name")] string function,
        CancellationToken ct = default)
        => await db.GetFunctionSource(schema, function, ct);

    [McpServerTool, Description("Lists primary keys for tables, optionally filtered by schema and/or table.")]
    public async Task<string> GetPrimaryKeys(
        PostgresService db,
        [Description("Schema name to filter by (optional)")]
        string? schema = null,
        [Description("Table name to filter by (optional)")]
        string? table = null,
        CancellationToken ct = default)
        => await db.GetPrimaryKeys(schema, table, ct);

    [McpServerTool, Description("Lists foreign key relationships for tables, optionally filtered by schema and/or table.")]
    public async Task<string> GetForeignKeys(
        PostgresService db,
        [Description("Schema name to filter by (optional)")]
        string? schema = null,
        [Description("Table name to filter by (optional)")]
        string? table = null,
        CancellationToken ct = default)
        => await db.GetForeignKeys(schema, table, ct);

    [McpServerTool, Description("Lists indexes for tables, optionally filtered by schema and/or table.")]
    public async Task<string> GetIndexes(
        PostgresService db,
        [Description("Schema name to filter by (optional)")]
        string? schema = null,
        [Description("Table name to filter by (optional)")]
        string? table = null,
        CancellationToken ct = default)
        => await db.GetIndexes(schema, table, ct);

    [McpServerTool, Description("Executes a SQL SELECT query and returns results formatted as a table. Read-only queries only.")]
    public async Task<string> ExecuteQuery(
        PostgresService db,
        [Description("SQL SELECT query to execute")] string query,
        [Description("Maximum number of rows to return (optional)")]
        int? maxRows = null,
        CancellationToken ct = default)
        => await db.ExecuteQuery(query, maxRows, ct);

    [McpServerTool, Description("Gets a sample of data from a table (SELECT * with LIMIT).")]
    public async Task<string> GetTableSample(
        PostgresService db,
        [Description("Schema name")] string schema,
        [Description("Table name")] string table,
        [Description("Number of rows to fetch (default 10)")]
        int limit = 10,
        CancellationToken ct = default)
        => await db.GetTableSample(schema, table, limit, ct);
}
