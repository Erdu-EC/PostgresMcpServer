# PostgreSQL MCP Server

Connect your AI application to PostgreSQL using the Model Context Protocol (MCP). This server provides two tools to interact with your database: execute queries and modify data.

## Installation

### Option 1: Direct download (recommended)

1. Download the binary for your platform from [Releases](https://github.com/user/PostgresMcpServer/releases)
2. Extract the zip file
3. Ensure the executable has execute permissions (Linux): `chmod +x PostgresMcpServer`

### Option 2: NuGet

```json
{
  "servers": {
    "postgres": {
      "type": "stdio",
      "command": "dnx",
      "args": ["PostgresMcpServer", "--yes"],
      "env": {
        "PGCONNECTIONSTRING": "Host=...;Port=5432;Database=...;Username=...;Password=..."
      }
    }
  }
}
```

## Configuration

### Database connection

Set the `PGCONNECTIONSTRING` environment variable with your connection string:

```
PGCONNECTIONSTRING="Host=myserver;Port=5432;Database=mydb;Username=myuser;Password=mypassword;"
```

### MCP client configuration

#### With downloaded binary

```json
{
  "servers": {
    "postgres": {
      "type": "stdio",
      "command": "./PostgresMcpServer",
      "args": [],
      "env": {
        "PGCONNECTIONSTRING": "Host=...;Port=5432;Database=...;Username=...;Password=..."
      }
    }
  }
}
```

#### On Windows

```json
{
  "servers": {
    "postgres": {
      "type": "stdio",
      "command": ".\\PostgresMcpServer.exe",
      "args": [],
      "env": {
        "PGCONNECTIONSTRING": "Host=...;Port=5432;Database=...;Username=...;Password=..."
      }
    }
  }
}
```

## Available tools

### `query` — Read queries

Execute SELECT statements and return results as JSON.

**Parameters:**
- `query` (string): The SQL SELECT statement to execute
- `maxRows` (integer, optional): Maximum number of rows to return. Default: **100**. Use this to prevent returning millions of rows accidentally.

**Example:**
```sql
SELECT tablename FROM pg_tables WHERE schemaname = 'public' LIMIT 10
```

**Response:**
```json
[
  {"tablename": "users"},
  {"tablename": "orders"}
]
```

### `execute` — Data modification

Execute commands that modify data or structure (INSERT, UPDATE, DELETE, CREATE, ALTER, DROP).

**Parameter:**
- `query` (string): The SQL statement to execute

**Example:**
```sql
INSERT INTO users (name, email) VALUES ('Juan', 'juan@ejemplo.com')
```

**Response:**
```
Rows affected: 1
```

## Supported data types

The server automatically converts PostgreSQL types to JSON:

| PostgreSQL | JSON |
|---|---|
| `INT`, `BIGINT`, `SMALLINT` | Number |
| `FLOAT`, `NUMERIC`, `MONEY` | Number |
| `VARCHAR`, `TEXT`, `CHAR` | String |
| `BOOLEAN` | Boolean |
| `TIMESTAMP`, `DATE` | ISO 8601 string |
| `BYTEA` | Base64 |
| `UUID` | String |
| `INTERVAL` | ISO 8601 duration |
| `NULL` | `null` |

## Troubleshooting

### Connection error
- Verify that `PGCONNECTIONSTRING` is configured correctly
- Ensure the PostgreSQL server is accessible from where you run the MCP server
- For SSL connections, add `SSL Mode=Require;` to the connection string

### Long query timeout
- Queries have a default timeout of 30 seconds
- For longer queries, optimize your query or add indexes

### Authentication error
- Verify username and password in the connection string
- Ensure the PostgreSQL user has the required permissions for the database

## Supported platforms

- Windows x64
- Linux x64
- Linux x64 (musl, statically compiled)

## More information

- [Official MCP Documentation](https://modelcontextprotocol.io/)
