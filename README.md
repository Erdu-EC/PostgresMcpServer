# PostgresMcpServer

MCP server para interactuar con bases de datos PostgreSQL. Proporciona herramientas para explorar esquemas, tablas, vistas, procedimientos almacenados, obtener DDL, ejecutar consultas y mas.

## Configuracion

Establece la variable de entorno `PGCONNECTIONSTRING` con la cadena de conexión a tu base de datos:

```powershell
$env:PGCONNECTIONSTRING = "Host=localhost;Port=5432;Database=mydb;Username=user;Password=pass"
```

## Herramientas

| Tool | Descripcion |
|------|-------------|
| `get_schemas` | Lista todos los esquemas no del sistema |
| `get_tables` | Lista tablas (filtro opcional por esquema) |
| `get_views` | Lista vistas (filtro opcional por esquema) |
| `get_functions` | Lista funciones y procedimientos almacenados (filtro opcional por esquema) |
| `get_table_columns` | Informacion detallada de columnas (nombre, tipo, nullable, default, descripcion) |
| `get_table_ddl` | Genera el DDL completo (CREATE TABLE con columnas, PKs, FKs, constraints, indices, comentarios) |
| `get_view_definition` | Obtiene la definicion (SELECT) de una vista |
| `get_function_source` | Obtiene el codigo fuente de una funcion o procedimiento |
| `get_primary_keys` | Lista llaves primarias (filtro opcional por esquema o tabla) |
| `get_foreign_keys` | Lista llaves foraneas con tablas referenciadas (filtro opcional) |
| `get_indexes` | Lista indices con su definicion (filtro opcional) |
| `execute_query` | Ejecuta consultas SELECT y devuelve resultados formateados en tabla |
| `get_table_sample` | Obtiene una muestra de datos de una tabla (SELECT * con LIMIT) |

## Uso con opencode

Agrega la siguiente configuracion a tu `opencode.jsonc`:

```jsonc
{
  "$schema": "https://opencode.ai/config.json",
  "mcp": {
    "PostgresMcpServer": {
      "type": "local",
      "command": ["dotnet", "run", "--project", "E:\\ruta\\al\\proyecto\\PostgresMcpServer"]
    }
  }
}
```

## Desarrollo local

```bash
dotnet run --project PostgresMcpServer.csproj
```

El servidor usa transporte stdio y se comunica mediante el protocolo MCP.

## Tecnologias

- [.NET 10](https://dotnet.microsoft.com/)
- [ModelContextProtocol C# SDK](https://www.nuget.org/packages/ModelContextProtocol)
- [Npgsql](https://www.npgsql.org/)
