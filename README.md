# PostgresMcpServer

MCP server para que tu agente de IA interactue con bases de datos PostgreSQL. Expone dos herramientas: una para consultas de lectura y otra para ejecutar sentencias de escritura/DDL.

## Requisitos

- [.NET 10 Runtime](https://dotnet.microsoft.com/) (o superior)
- Una base de datos PostgreSQL accesible

## Configuracion

Define la variable de entorno `PGCONNECTIONSTRING` con tu cadena de conexion:

```
Host=localhost;Port=5432;Database=mydb;Username=user;Password=pass
```

> **Nota de seguridad:** usa un usuario con los minimos permisos necesarios. La herramienta `execute` puede modificar o eliminar datos y esquemas.

## Herramientas

| Tool | Descripcion |
|------|-------------|
| `query` | Ejecuta una consulta SQL de lectura (SELECT). Corre en una transaccion `READ ONLY` por seguridad. Acepta `maxRows` (por defecto 100) para limitar las filas devueltas. |
| `execute` | Ejecuta una sentencia SQL que modifica datos o esquema (INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, TRUNCATE, etc.). Devuelve el numero de filas afectadas. **Puede causar cambios irreversibles.** |

Los resultados se devuelven como JSON. Si `query` trunca el resultado por `maxRows`, lo indica con un campo `truncated` y una nota para refinar la consulta.

Con `query` puedes explorar el esquema usando `information_schema` y `pg_catalog`: listar tablas, ver columnas, definiciones de vistas, funciones, indices, constraints, etc.

## Registro en tu cliente MCP

### opencode

Agrega esto a tu `opencode.jsonc`:

```jsonc
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "PostgresMcpServer": {
      "type": "local",
      "command": ["dotnet", "run", "--project", "E:\\ruta\\al\\proyecto\\PostgresMcpServer"],
      "environment": {
        "PGCONNECTIONSTRING": "Host=localhost;Port=5432;Database=mydb;Username=user;Password=pass"
      }
    }
  }
}
```

### Claude Desktop

Agrega esto a tu archivo de configuracion (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "PostgresMcpServer": {
      "command": "dotnet",
      "args": ["run", "--project", "E:\\ruta\\al\\proyecto\\PostgresMcpServer"],
      "env": {
        "PGCONNECTIONSTRING": "Host=localhost;Port=5432;Database=mydb;Username=user;Password=pass"
      }
    }
  }
}
```

### Ejecutar desde el codigo fuente

```bash
dotnet run --project PostgresMcpServer.csproj
```

El server se comunica por transporte stdio mediante el protocolo MCP.

## Tecnologias

- [.NET 10](https://dotnet.microsoft.com/)
- [ModelContextProtocol C# SDK](https://www.nuget.org/packages/ModelContextProtocol)
- [Npgsql](https://www.npgsql.org/)

## Licencia

[MIT](LICENSE)
