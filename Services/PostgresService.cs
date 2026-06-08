using Npgsql;
using System.Data;
using System.Text;

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

    public async Task<string> GetSchemas(CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT schema_name
            FROM information_schema.schemata
            WHERE schema_name NOT IN ('pg_catalog', 'information_schema')
              AND schema_name NOT LIKE 'pg_toast%'
              AND schema_name NOT LIKE 'pg_temp%'
            ORDER BY schema_name
            """, conn);

        var sb = new StringBuilder();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            sb.AppendLine(reader.GetString(0));

        return sb.Length > 0 ? sb.ToString() : "(no schemas found)";
    }

    public async Task<string> GetTables(string? schema, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        var sb = new StringBuilder();

        var sql = """
            SELECT n.nspname AS table_schema,
                   c.relname AS table_name,
                   pg_size_pretty(pg_total_relation_size(c.oid)) AS size
            FROM pg_catalog.pg_class c
            JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r'
              AND n.nspname NOT IN ('pg_catalog', 'information_schema')
            """;

        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            if (!string.IsNullOrEmpty(schema))
                cmd.Parameters.AddWithValue("schema", schema);

            cmd.CommandText += " ORDER BY n.nspname, c.relname";

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                sb.AppendLine($"{reader.GetString(0)}.{reader.GetString(1)}  ({reader.GetString(2)})");
        }

        return sb.Length > 0 ? sb.ToString() : "(no tables found)";
    }

    public async Task<string> GetViews(string? schema, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        var sb = new StringBuilder();

        var sql = """
            SELECT table_schema, table_name
            FROM information_schema.views
            WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
            """;

        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            if (!string.IsNullOrEmpty(schema))
                cmd.Parameters.AddWithValue("schema", schema);

            cmd.CommandText += " ORDER BY table_schema, table_name";

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                sb.AppendLine($"{reader.GetString(0)}.{reader.GetString(1)}");
        }

        return sb.Length > 0 ? sb.ToString() : "(no views found)";
    }

    public async Task<string> GetFunctions(string? schema, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        var sb = new StringBuilder();

        var sql = """
            SELECT n.nspname, p.proname,
                   pg_get_function_arguments(p.oid) AS args,
                   CASE p.prokind
                       WHEN 'f' THEN 'FUNCTION'
                       WHEN 'p' THEN 'PROCEDURE'
                       WHEN 'a' THEN 'AGGREGATE'
                       ELSE 'OTHER'
                   END AS kind,
                   l.lanname AS language
            FROM pg_catalog.pg_proc p
            JOIN pg_catalog.pg_namespace n ON p.pronamespace = n.oid
            JOIN pg_catalog.pg_language l ON p.prolang = l.oid
            WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
              AND p.prokind IN ('f', 'p')
            """;

        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            if (!string.IsNullOrEmpty(schema))
                cmd.Parameters.AddWithValue("schema", schema);

            cmd.CommandText += " ORDER BY n.nspname, p.proname";

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                sb.AppendLine($"{reader.GetString(0)}.{reader.GetString(1)}({reader.GetString(2)})  [{reader.GetString(3)}, {reader.GetString(4)}]");
        }

        return sb.Length > 0 ? sb.ToString() : "(no functions found)";
    }

    public async Task<string> GetTableColumns(string schema, string table, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        var sb = new StringBuilder();

        await using (var cmd = new NpgsqlCommand("""
            SELECT c.column_name, c.udt_name, c.character_maximum_length,
                   c.numeric_precision, c.numeric_scale, c.is_nullable,
                   c.column_default, c.ordinal_position,
                   pg_catalog.col_description(
                       quote_ident(c.table_schema)::regnamespace::oid,
                       c.ordinal_position
                   ) AS description
            FROM information_schema.columns c
            WHERE c.table_schema = @schema AND c.table_name = @table
            ORDER BY c.ordinal_position
            """, conn))
        {
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("table", table);

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (!reader.HasRows)
                return "(table or view not found)";

            sb.AppendLine($"Columns for {schema}.{table}:");
            sb.AppendLine(new string('-', 80));
            sb.AppendLine($"{"#",-3} {"Column",-25} {"Type",-20} {"Nullable",-10} {"Default",-20}");
            sb.AppendLine(new string('-', 80));

            while (await reader.ReadAsync(ct))
            {
                var typeName = FormatTypeName(
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    reader.IsDBNull(4) ? null : reader.GetInt32(4)
                );
                var nullable = reader.GetString(5) == "YES" ? "YES" : "NO";
                var defaultValue = reader.IsDBNull(6) ? "" : reader.GetString(6);
                var desc = reader.IsDBNull(8) ? "" : reader.GetString(8);

                sb.AppendLine(
                    $"{reader.GetInt32(7),-3} {reader.GetString(0),-25} {typeName,-20} {nullable,-10} {defaultValue,-20}");

                if (!string.IsNullOrEmpty(desc))
                    sb.AppendLine($"    -> {desc}");
            }
        }

        return sb.ToString();
    }

    public async Task<string> GetTableDdl(string schema, string table, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        var sb = new StringBuilder();

        sb.Append($"CREATE TABLE {QuoteIdentifier(schema)}.{QuoteIdentifier(table)} (\n");

        var columnDefs = new List<string>();

        // Build FK lookup: map column -> FK info for single-column FKs
        var colFkLookup = new Dictionary<string, (string conName, string fSchema, string fTable, string fCol)>(StringComparer.OrdinalIgnoreCase);
        var multiColumnFkGroups = new Dictionary<string, (List<string> cols, string fSchema, string fTable, List<string> fCols)>();

        await using (var fkCmd = new NpgsqlCommand("""
            SELECT c.conname AS constraint_name,
                   a.attname AS column_name,
                   nsp.nspname AS foreign_schema,
                   rel.relname AS foreign_table,
                   fa.attname AS foreign_column,
                   u.ord AS position
            FROM pg_catalog.pg_constraint c
            CROSS JOIN LATERAL unnest(c.conkey, c.confkey) WITH ORDINALITY AS u(conkey, confkey, ord)
            JOIN pg_catalog.pg_attribute a
              ON a.attrelid = c.conrelid AND a.attnum = u.conkey
            JOIN pg_catalog.pg_class rel ON rel.oid = c.confrelid
            JOIN pg_catalog.pg_namespace nsp ON nsp.oid = rel.relnamespace
            JOIN pg_catalog.pg_attribute fa
              ON fa.attrelid = c.confrelid AND fa.attnum = u.confkey
            WHERE c.conrelid = (quote_ident(@schema) || '.' || quote_ident(@table))::regclass
              AND c.contype = 'f'
            ORDER BY c.conname, u.ord
            """, conn))
        {
            fkCmd.Parameters.AddWithValue("schema", schema);
            fkCmd.Parameters.AddWithValue("table", table);

            await using var reader = await fkCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var fkName = reader.GetString(0);
                var col = reader.GetString(1);
                var fSchema = reader.GetString(2);
                var fTable = reader.GetString(3);
                var fCol = reader.GetString(4);

                if (!multiColumnFkGroups.ContainsKey(fkName))
                    multiColumnFkGroups[fkName] = (new List<string>(), fSchema, fTable, new List<string>());

                multiColumnFkGroups[fkName].cols.Add(col);
                multiColumnFkGroups[fkName].fCols.Add(fCol);
            }
        }

        // Separate single vs multi-column FKs; single go inline
        var singleColFkNames = new HashSet<string>();
        foreach (var (name, (cols, _, _, _)) in multiColumnFkGroups)
        {
            if (cols.Count == 1)
                singleColFkNames.Add(name);
        }

        foreach (var (name, (cols, fSchema, fTable, fCols)) in multiColumnFkGroups)
        {
            if (singleColFkNames.Contains(name))
                colFkLookup[cols[0]] = (name, fSchema, fTable, fCols[0]);
        }

        // Build PK lookup: single-column PKs go inline
        var pkColNames = new List<string>();
        var pkConName = "";
        await using (var pkCmd = new NpgsqlCommand("""
            SELECT c.conname AS constraint_name, a.attname AS column_name
            FROM pg_catalog.pg_constraint c
            CROSS JOIN LATERAL unnest(c.conkey) WITH ORDINALITY AS u(attnum, ord)
            JOIN pg_catalog.pg_attribute a
              ON a.attrelid = c.conrelid AND a.attnum = u.attnum
            WHERE c.conrelid = (quote_ident(@schema) || '.' || quote_ident(@table))::regclass
              AND c.contype = 'p'
            ORDER BY u.ord
            """, conn))
        {
            pkCmd.Parameters.AddWithValue("schema", schema);
            pkCmd.Parameters.AddWithValue("table", table);

            await using var reader = await pkCmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                pkConName = reader.GetString(0);
                pkColNames.Add(reader.GetString(1));
            }
        }

        var singleColPk = pkColNames.Count == 1 ? pkColNames[0] : null;

        // Columns
        await using (var cmd = new NpgsqlCommand("""
            SELECT column_name, udt_name, character_maximum_length,
                   numeric_precision, numeric_scale, is_nullable,
                   column_default, ordinal_position,
                   is_identity, identity_generation,
                   is_generated, generation_expression
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @table
            ORDER BY ordinal_position
            """, conn))
        {
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("table", table);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!reader.HasRows)
                return "(table not found)";

            while (await reader.ReadAsync(ct))
            {
                var colName = reader.GetString(0);
                var colDef = $"  {QuoteIdentifier(colName)} {FormatTypeName(
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    reader.IsDBNull(4) ? null : reader.GetInt32(4)
                )}";

                if (reader.GetString(5) == "NO")
                    colDef += " NOT NULL";

                var isIdentity = reader.IsDBNull(8) ? "NO" : reader.GetString(8);
                var identityGeneration = reader.IsDBNull(9) ? "" : reader.GetString(9);
                var isGenerated = reader.IsDBNull(10) ? "NEVER" : reader.GetString(10);
                var generationExpression = reader.IsDBNull(11) ? "" : reader.GetString(11);

                if (isIdentity == "YES")
                {
                    colDef += $" GENERATED {identityGeneration} AS IDENTITY";
                }
                else if (isGenerated == "ALWAYS")
                {
                    colDef += $" GENERATED ALWAYS AS ({generationExpression}) STORED";
                }
                else if (!reader.IsDBNull(6))
                {
                    var def = reader.GetString(6);
                    if (!def.StartsWith("nextval("))
                        colDef += $" DEFAULT {def}";
                }

                // Inline REFERENCES for single-column FKs
                if (colFkLookup.TryGetValue(colName, out var fkInfo))
                {
                    colDef += $" CONSTRAINT {QuoteIdentifier(fkInfo.conName)} REFERENCES {QuoteIdentifier(fkInfo.fSchema)}.{QuoteIdentifier(fkInfo.fTable)}";
                    if (!string.Equals(colName, fkInfo.fCol, StringComparison.OrdinalIgnoreCase))
                        colDef += $" ({QuoteIdentifier(fkInfo.fCol)})";
                }

                // Inline PRIMARY KEY for single-column PKs
                if (string.Equals(colName, singleColPk, StringComparison.OrdinalIgnoreCase))
                    colDef += $" CONSTRAINT {QuoteIdentifier(pkConName)} PRIMARY KEY";

                columnDefs.Add(colDef);
            }
        }

        // Primary key (multi-column only; single-column is inlined above)
        if (pkColNames.Count > 1)
            columnDefs.Add($"  CONSTRAINT {QuoteIdentifier(pkConName)} PRIMARY KEY ({string.Join(", ", pkColNames.Select(QuoteIdentifier))})");

        // Foreign keys (multi-column only; single-column are inlined above)
        foreach (var (name, (cols, fSchema, fTable, fCols)) in multiColumnFkGroups)
        {
            if (singleColFkNames.Contains(name))
                continue;

            columnDefs.Add($"  CONSTRAINT {QuoteIdentifier(name)} FOREIGN KEY ({string.Join(", ", cols.Select(QuoteIdentifier))}) REFERENCES {QuoteIdentifier(fSchema)}.{QuoteIdentifier(fTable)} ({string.Join(", ", fCols.Select(QuoteIdentifier))})");
        }

        // Unique constraints
        await using (var cmd = new NpgsqlCommand("""
            SELECT c.conname AS constraint_name, a.attname AS column_name
            FROM pg_catalog.pg_constraint c
            CROSS JOIN LATERAL unnest(c.conkey) WITH ORDINALITY AS u(attnum, ord)
            JOIN pg_catalog.pg_attribute a
              ON a.attrelid = c.conrelid AND a.attnum = u.attnum
            WHERE c.conrelid = (quote_ident(@schema) || '.' || quote_ident(@table))::regclass
              AND c.contype = 'u'
            ORDER BY c.conname, u.ord
            """, conn))
        {
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("table", table);

            var uniqueGroups = new Dictionary<string, List<string>>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var name = reader.GetString(0);
                var col = reader.GetString(1);
                if (!uniqueGroups.ContainsKey(name))
                    uniqueGroups[name] = new List<string>();
                uniqueGroups[name].Add(QuoteIdentifier(col));
            }

            foreach (var (name, cols) in uniqueGroups)
                columnDefs.Add($"  CONSTRAINT {QuoteIdentifier(name)} UNIQUE ({string.Join(", ", cols)})");
        }

        // Check constraints
        await using (var cmd = new NpgsqlCommand("""
            SELECT c.conname AS constraint_name, pg_get_constraintdef(c.oid) AS check_def
            FROM pg_catalog.pg_constraint c
            WHERE c.conrelid = (quote_ident(@schema) || '.' || quote_ident(@table))::regclass
              AND c.contype = 'c'
            ORDER BY c.conname
            """, conn))
        {
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("table", table);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                columnDefs.Add($"  CONSTRAINT {QuoteIdentifier(reader.GetString(0))} {reader.GetString(1)}");
        }

        sb.AppendLine(string.Join(",\n", columnDefs));
        sb.AppendLine(");");

        // Table comment
        await using (var cmd = new NpgsqlCommand("""
            SELECT pg_catalog.obj_description(
                (quote_ident(@schema) || '.' || quote_ident(@table))::regclass::oid,
                'pg_class'
            )
            """, conn))
        {
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("table", table);

            var tableComment = await cmd.ExecuteScalarAsync(ct);
            if (tableComment is string comment && !string.IsNullOrEmpty(comment))
                sb.AppendLine($"COMMENT ON TABLE {QuoteIdentifier(schema)}.{QuoteIdentifier(table)} IS '{comment.Replace("'", "''")}';");
        }

        // Comments
        await using (var cmd = new NpgsqlCommand("""
            SELECT c.column_name, pg_catalog.col_description(
                quote_ident(c.table_schema)::regnamespace::oid,
                c.ordinal_position
            ) AS description
            FROM information_schema.columns c
            WHERE c.table_schema = @schema AND c.table_name = @table
              AND pg_catalog.col_description(
                  quote_ident(c.table_schema)::regnamespace::oid,
                  c.ordinal_position
              ) IS NOT NULL
            ORDER BY c.ordinal_position
            """, conn))
        {
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("table", table);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var colName = reader.GetString(0);
                var comment = reader.GetString(1);
                sb.AppendLine($"COMMENT ON COLUMN {QuoteIdentifier(schema)}.{QuoteIdentifier(table)}.{QuoteIdentifier(colName)} IS '{comment.Replace("'", "''")}';");
            }
        }

        // Indexes
        sb.AppendLine();
        await using (var cmd = new NpgsqlCommand("""
            SELECT indexdef
            FROM pg_indexes
            WHERE schemaname = @schema AND tablename = @table
              AND indexname NOT LIKE '%_pkey'
            ORDER BY indexname
            """, conn))
        {
            cmd.Parameters.AddWithValue("schema", schema);
            cmd.Parameters.AddWithValue("table", table);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                sb.AppendLine(reader.GetString(0) + ";");
        }

        return sb.ToString();
    }

    public async Task<string> GetViewDefinition(string schema, string view, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT pg_get_viewdef(quote_ident(table_schema) || '.' || quote_ident(table_name), true)
            FROM information_schema.views
            WHERE table_schema = @schema AND table_name = @view
            """, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("view", view);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string s ? s : "(view not found)";
    }

    public async Task<string> GetFunctionSource(string schema, string function, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT pg_get_functiondef(p.oid)
            FROM pg_proc p
            JOIN pg_namespace n ON p.pronamespace = n.oid
            WHERE n.nspname = @schema AND p.proname = @function
            """, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("function", function);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string s ? s : "(function not found)";
    }

    public async Task<string> GetPrimaryKeys(string? schema, string? table, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        var sb = new StringBuilder();

        await using (var cmd = new NpgsqlCommand("""
            SELECT n.nspname AS table_schema,
                   c.relname AS table_name,
                   a.attname AS column_name,
                   con.conname AS constraint_name
            FROM pg_catalog.pg_constraint con
            JOIN pg_catalog.pg_class c ON c.oid = con.conrelid
            JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
            CROSS JOIN LATERAL unnest(con.conkey) WITH ORDINALITY AS u(attnum, ord)
            JOIN pg_catalog.pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = u.attnum
            WHERE con.contype = 'p'
              AND n.nspname NOT IN ('pg_catalog', 'information_schema')
              AND (@schema IS NULL OR n.nspname = @schema)
              AND (@table IS NULL OR c.relname = @table)
            ORDER BY n.nspname, c.relname, u.ord
            """, conn))
        {
            cmd.Parameters.AddWithValue("schema", (object?)schema ?? DBNull.Value);
            cmd.Parameters.AddWithValue("table", (object?)table ?? DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!reader.HasRows)
                return "(no primary keys found)";

            sb.AppendLine($"{"Schema",-15} {"Table",-25} {"Column",-25} {"Constraint",-30}");
            sb.AppendLine(new string('-', 95));
            while (await reader.ReadAsync(ct))
                sb.AppendLine($"{reader.GetString(0),-15} {reader.GetString(1),-25} {reader.GetString(2),-25} {reader.GetString(3),-30}");
        }

        return sb.ToString();
    }

    public async Task<string> GetForeignKeys(string? schema, string? table, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        var sb = new StringBuilder();

        await using (var cmd = new NpgsqlCommand("""
            SELECT n.nspname AS table_schema,
                   c.relname AS table_name,
                   a.attname AS column_name,
                   fn.nspname AS foreign_schema,
                   fc.relname AS foreign_table,
                   fa.attname AS foreign_column,
                   con.conname AS constraint_name
            FROM pg_catalog.pg_constraint con
            JOIN pg_catalog.pg_class c ON c.oid = con.conrelid
            JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
            CROSS JOIN LATERAL unnest(con.conkey, con.confkey) WITH ORDINALITY AS u(conkey, confkey, ord)
            JOIN pg_catalog.pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = u.conkey
            JOIN pg_catalog.pg_class fc ON fc.oid = con.confrelid
            JOIN pg_catalog.pg_namespace fn ON fn.oid = fc.relnamespace
            JOIN pg_catalog.pg_attribute fa ON fa.attrelid = con.confrelid AND fa.attnum = u.confkey
            WHERE con.contype = 'f'
              AND n.nspname NOT IN ('pg_catalog', 'information_schema')
              AND (@schema IS NULL OR n.nspname = @schema)
              AND (@table IS NULL OR c.relname = @table)
            ORDER BY n.nspname, c.relname, con.conname, u.ord
            """, conn))
        {
            cmd.Parameters.AddWithValue("schema", (object?)schema ?? DBNull.Value);
            cmd.Parameters.AddWithValue("table", (object?)table ?? DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!reader.HasRows)
                return "(no foreign keys found)";

            sb.AppendLine($"{"Schema",-15} {"Table",-20} {"Column",-20} {"-> Ref",-45} {"Constraint",-30}");
            sb.AppendLine(new string('-', 130));
            while (await reader.ReadAsync(ct))
            {
                var refStr = $"{reader.GetString(3)}.{reader.GetString(4)}({reader.GetString(5)})";
                sb.AppendLine($"{reader.GetString(0),-15} {reader.GetString(1),-20} {reader.GetString(2),-20} {refStr,-45} {reader.GetString(6),-30}");
            }
        }

        return sb.ToString();
    }

    public async Task<string> GetIndexes(string? schema, string? table, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        var sb = new StringBuilder();

        await using (var cmd = new NpgsqlCommand("""
            SELECT schemaname, tablename, indexname, indexdef
            FROM pg_indexes
            WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
              AND (@schema IS NULL OR schemaname = @schema)
              AND (@table IS NULL OR tablename = @table)
            ORDER BY schemaname, tablename, indexname
            """, conn))
        {
            cmd.Parameters.AddWithValue("schema", (object?)schema ?? DBNull.Value);
            cmd.Parameters.AddWithValue("table", (object?)table ?? DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!reader.HasRows)
                return "(no indexes found)";

            while (await reader.ReadAsync(ct))
            {
                sb.AppendLine($"-- {reader.GetString(0)}.{reader.GetString(1)} ({reader.GetString(2)})");
                sb.AppendLine(reader.GetString(3) + ";");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    public async Task<string> ExecuteQuery(string query, int? maxRows, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(query, conn);

        try
        {
            var sb = await FormatResults(cmd, maxRows, ct);
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: {ex.Message}";
        }
    }

    public async Task<string> GetTableSample(string schema, string table, int limit, CancellationToken ct)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(
            $"SELECT * FROM {QuoteIdentifier(schema)}.{QuoteIdentifier(table)} LIMIT @limit", conn);
        cmd.Parameters.AddWithValue("limit", limit);

        var sb = await FormatResults(cmd, limit, ct);
        return sb.Length > 0 ? sb.ToString() : "(no results)";
    }

    private async Task<StringBuilder> FormatResults(NpgsqlCommand cmd, int? maxRows, CancellationToken ct)
    {
        await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.Default, ct);
        var sb = new StringBuilder();

        if (!reader.HasRows && reader.FieldCount == 0)
        {
            sb.Append("(query executed successfully, no results returned)");
            return sb;
        }

        var columns = new List<string>(reader.FieldCount);
        for (var i = 0; i < reader.FieldCount; i++)
            columns.Add(reader.GetName(i));

        var widths = columns.Select(c => Math.Max(c.Length, 4)).ToArray();
        var rowCount = 0;
        var rows = new List<string[]>();

        while (await reader.ReadAsync(ct))
        {
            if (maxRows.HasValue && rows.Count >= maxRows.Value)
            {
                rowCount++;
                continue;
            }

            var values = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                values[i] = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "";
                if (values[i].Length > 100)
                    values[i] = values[i][..100] + "...";
                if (values[i].Length > widths[i])
                    widths[i] = Math.Min(values[i].Length, 100);
            }
            rows.Add(values);
            rowCount++;
        }

        var separator = "+";
        foreach (var w in widths) separator += new string('-', w + 2) + "+";

        sb.AppendLine(separator);
        sb.Append("|");
        for (var i = 0; i < columns.Count; i++)
            sb.Append($" {columns[i].PadRight(widths[i])} |");
        sb.AppendLine();
        sb.AppendLine(separator);

        foreach (var row in rows)
        {
            sb.Append("|");
            for (var i = 0; i < row.Length; i++)
                sb.Append($" {row[i].PadRight(widths[i])} |");
            sb.AppendLine();
        }

        sb.AppendLine(separator);
        sb.AppendLine($"({rowCount} row(s))");

        return sb;
    }

    private static string FormatTypeName(string udtName, int? charLength, int? numPrecision, int? numScale)
    {
        if (charLength.HasValue && (udtName is "varchar" or "bpchar"))
            return udtName == "bpchar" ? $"character({charLength})" : $"varchar({charLength})";

        if (udtName == "numeric" && numPrecision.HasValue)
            return numScale.HasValue && numScale > 0
                ? $"numeric({numPrecision},{numScale})"
                : $"numeric({numPrecision})";

        return udtName switch
        {
            "bpchar" => "char",
            "int4" => "integer",
            "int8" => "bigint",
            "float8" => "double precision",
            "float4" => "real",
            "bool" => "boolean",
            "timestamptz" => "timestamp with time zone",
            "timestamp" => "timestamp without time zone",
            "timetz" => "time with time zone",
            "time" => "time without time zone",
            _ => udtName
        };
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"") + "\"";
    }
}
