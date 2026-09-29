# Base de datos

La fuente de verdad del esquema son las migraciones en `src/NetWatch.Data/Migrations`.

- Con Docker, la API aplica migraciones al iniciar (`Database__AutoMigrate=true`).
- `001_initial.sql` permite inspeccionar o aplicar manualmente el esquema inicial.
- Los roles, tipos de dispositivo y usuarios iniciales se cargan por `DbInitializer`; por eso no forman parte del SQL de estructura.

Aplicación manual en una base vacía:

```bash
mysql -h SERVIDOR -u USUARIO -p netwatch < database/001_initial.sql
```

Para generar un script actualizado con el SDK y `dotnet-ef`:

```powershell
dotnet ef migrations script --project src\NetWatch.Data --startup-project src\NetWatch.API --output database\netwatch.sql
```

No edite una migración ya aplicada en producción; cree otra con `dotnet ef migrations add NombreDelCambio`.
