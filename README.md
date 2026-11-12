# RutaViva · Backend

API REST y Worker en .NET 10 sobre PostgreSQL 17. Este documento explica, paso a paso, cómo instalar lo necesario y levantar el ambiente completo: base de datos, API y Worker.

Para el contexto funcional, el guion de demo y las decisiones de diseño, vea el [README principal](../README.md).

## Qué se levanta

| Pieza | Proyecto | Qué hace |
|---|---|---|
| Base de datos | `postgres:17-alpine` (Docker) | Guarda vehículos, rutinas, órdenes, lecturas, outbox y avisos |
| API | `src/RutaViva.Api` | Expone `/api/v1`, aplica las migraciones al arrancar y siembra datos de demo |
| Worker | `src/RutaViva.Worker` | Publica los eventos del outbox como avisos y ejecuta la revisión diaria |

```
 Navegador / telemetría ──HTTP──▶ API ──┐
                                        ├──▶ PostgreSQL
                         Worker ────────┘
```

La API y el Worker no se comunican entre sí: ambos trabajan sobre la misma base de datos. La API escribe los eventos en la tabla `outbox_messages` y el Worker los recoge cada 2 segundos.

Hay dos formas de levantarlo:

- **Opción A, todo en Docker:** solo necesita Docker. Es la más rápida.
- **Opción B, API y Worker en local:** la base de datos corre en Docker y el código se ejecuta con el SDK de .NET. Úsela para depurar o desarrollar.

---

## Requisitos

| Herramienta | Versión | Para qué | Comprobar |
|---|---|---|---|
| Docker Desktop o Docker Engine con Compose v2 | Reciente | Base de datos (A y B) y todo el ambiente (A) | `docker compose version` |
| SDK de .NET | 10.0.401 o posterior (fijado en `global.json`) | Solo opción B y pruebas | `dotnet --version` |

Las pruebas de integración también necesitan Docker, porque levantan su propio PostgreSQL con Testcontainers.

---

## Opción A: todo con Docker

Todos los comandos se ejecutan desde la **raíz del repositorio**, no desde `backend/`.

1. **Clonar el repositorio.**

   ```bash
   git clone <url-del-repositorio> rutaviva
   cd rutaviva
   ```

2. **(Opcional) Cambiar puertos o credenciales de demo.** Si el puerto 8080, 4200 o 5432 ya está ocupado en su máquina, copie el archivo de ejemplo y edite los valores:

   ```bash
   cp .env.example .env
   ```

   | Variable | Valor por defecto | Uso |
   |---|---|---|
   | `API_PORT` | `8080` | Puerto de la API en su máquina |
   | `WEB_PORT` | `4200` | Puerto de la interfaz |
   | `POSTGRES_PORT` | `5432` | Puerto de PostgreSQL en su máquina |
   | `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | `rutaviva`, `rutaviva`, `rutaviva_demo_only` | Credenciales de demo; nunca use credenciales reales aquí |

3. **Construir y levantar.**

   ```bash
   docker compose up --build -d
   ```

   Docker arranca los servicios en este orden y espera a que cada uno esté sano antes del siguiente:

   ```
   postgres (sano) ──▶ api (migra y siembra; sana) ──▶ worker y web
   ```

4. **Comprobar que todo está arriba.**

   ```bash
   docker compose ps
   ```

   `postgres` y `api` deben aparecer como `healthy`; `worker` y `web`, como `Up`. La primera construcción tarda unos minutos.

5. **Abrir el ambiente.** Si cambió `API_PORT` o `WEB_PORT`, use sus puertos.

   | Qué | URL |
   |---|---|
   | Interfaz | http://localhost:4200 |
   | Documentación interactiva de la API (Scalar) | http://localhost:8080/scalar |
   | Salud de la API | http://localhost:8080/health |

### Comandos útiles con Docker

| Para | Comando |
|---|---|
| Ver los logs de la API o del Worker | `docker compose logs -f api` · `docker compose logs -f worker` |
| Reconstruir tras cambiar código del backend | `docker compose up --build -d api worker` |
| Detener sin perder datos | `docker compose down` |
| Empezar de cero (borra la base de datos) | `docker compose down -v` y luego `docker compose up --build -d` |

---

## Opción B: API y Worker en local

La base de datos sigue en Docker; la API y el Worker se ejecutan con `dotnet run`, cada uno en su propia terminal.

### Paso 1. Levantar solo PostgreSQL

Desde la raíz del repositorio:

```bash
docker compose up -d postgres
```

### Paso 2. Restaurar y compilar

```bash
cd backend
dotnet tool restore
dotnet build
```

`dotnet tool restore` instala `dotnet-ef`, declarado en `dotnet-tools.json`. Solo hace falta para crear migraciones nuevas.

### Paso 3. Definir la cadena de conexión

La cadena de conexión **no** está en los `appsettings` versionados; se pasa como variable de entorno `ConnectionStrings__RutaViva`. Hay que definirla en **cada** terminal donde vaya a correr la API o el Worker.

Con los valores de demo por defecto:

PowerShell:

```powershell
$env:ConnectionStrings__RutaViva = "Host=localhost;Port=5432;Database=rutaviva;Username=rutaviva;Password=rutaviva_demo_only"
```

Bash, Git Bash o zsh:

```bash
export ConnectionStrings__RutaViva="Host=localhost;Port=5432;Database=rutaviva;Username=rutaviva;Password=rutaviva_demo_only"
```

Si cambió las credenciales o el puerto en `.env`, use esos valores. Si la variable falta, la API y el Worker se detienen al arrancar con un mensaje que lo indica.

### Paso 4. Arrancar la API (terminal 1)

```bash
cd backend
dotnet run --project src/RutaViva.Api --launch-profile http
```

En el perfil `http` la API corre en **http://localhost:5005** con el entorno `Development`, que:

- aplica las migraciones pendientes al arrancar,
- siembra los datos de demo si la base está vacía,
- habilita el reloj de demo y permite CORS desde `http://localhost:4200`.

Espere a ver `Now listening on: http://localhost:5005` antes de seguir.

### Paso 5. Arrancar el Worker (terminal 2)

Defina otra vez la variable del paso 3 en esta terminal y luego:

```bash
cd backend
dotnet run --project src/RutaViva.Worker
```

Arranque el Worker **después** de la API: el Worker nunca crea ni migra el esquema. Si arranca antes, no falla: registra una advertencia en cada vuelta hasta que las tablas existan.

### Paso 6 (opcional). Levantar la interfaz

```bash
cd frontend/rutaviva-web
npm ci
npm start
```

Necesita Node 22 o posterior. La interfaz queda en http://localhost:4200 y redirige `/api` hacia `http://localhost:5005`.

---

## Verificar que el ambiente funciona

Use el puerto de su opción: `8080` en la opción A (o su `API_PORT`) y `5005` en la opción B. Los ejemplos usan `5005`.

1. **La API responde y ve la base de datos.**

   ```bash
   curl http://localhost:5005/health
   ```

   Debe responder `Healthy`.

2. **Hay datos de demo.**

   ```bash
   curl http://localhost:5005/api/v1/vehicles
   ```

   Debe listar 4 vehículos (RVA101, RVB202, RVC303 y RVD404). Copie el `id` de uno para el paso siguiente.

3. **Una lectura de odómetro vence una rutina y abre una orden.** Envíe un kilometraje mayor que el próximo vencimiento del vehículo:

   ```bash
   curl -X POST http://localhost:5005/api/v1/vehicles/<id>/odometer-readings \
        -H "Content-Type: application/json" \
        -d '{"km": 200000}'
   ```

   La respuesta debe traer `"status": "Applied"` y `serviceOrdersOpened` mayor que 0.

4. **El Worker publicó los avisos.** Pocos segundos después:

   ```bash
   curl http://localhost:5005/api/v1/notifications
   ```

   Deben aparecer avisos de rutina vencida y orden creada. En los logs del Worker verá líneas `Outbox batch: N read, N published, 0 failed`.

También puede hacer todo esto desde Scalar (`/scalar`) con el botón **Test Request**, o con los archivos de [`docs/http/`](../docs/http/).

---

## Cómo trabaja el Worker

El Worker es un proceso sin endpoints HTTP que corre dos ciclos independientes. En cada vuelta crea su propio scope de inyección de dependencias, igual que una petición en la API.

| Ciclo | Frecuencia | Qué hace |
|---|---|---|
| `OutboxDispatcher` | Cada `Worker:OutboxPollSeconds` (2 s) | Toma hasta `Worker:OutboxBatchSize` (20) eventos pendientes con `FOR UPDATE SKIP LOCKED`, los convierte en avisos y los marca como procesados. Si un evento falla, lo reintenta en vueltas siguientes, hasta 10 intentos. |
| `DailyCheckScheduler` | Cada `Worker:DailyCheckPollSeconds` (30 s; 5 s en Development y en Docker) | Si la fecha de negocio cambió desde la última corrida, evalúa las rutinas por tiempo y abre las órdenes de las vencidas. |

Como el Worker comprueba la fecha en vez de esperar a la medianoche, al adelantar el reloj de demo con `POST /api/v1/system/clock/advance` la revisión diaria se ejecuta en la siguiente vuelta.

---

## Configuración

Cualquier clave se puede sobrescribir con una variable de entorno, cambiando `:` por `__` (por ejemplo, `Worker:OutboxPollSeconds` → `Worker__OutboxPollSeconds`).

| Clave | API | Worker | Por defecto | Development / Docker | Descripción |
|---|:-:|:-:|---|---|---|
| `ConnectionStrings:RutaViva` | ✔ | ✔ | vacía | por variable de entorno | Conexión a PostgreSQL; obligatoria |
| `Database:ApplyMigrationsOnStartup` | ✔ | | `false` | `true` | Aplica las migraciones al arrancar |
| `Demo:Enabled` | ✔ | ✔ | `false` | `true` | Reloj de demo y endpoints `/api/v1/system` |
| `Demo:SeedData` | ✔ | | `false` | `true` | Siembra datos de demo si la base está vacía |
| `Business:UtcOffsetHours` | ✔ | ✔ | `-5` | `-5` | Zona horaria que define la fecha de negocio |
| `Business:QuoteApprovalThresholdCop` | ✔ | ✔ | `1500000` | `1500000` | Por encima, el cliente debe aprobar la cotización |
| `Cors:AllowedOrigins` | ✔ | | vacío | `http://localhost:4200` | Orígenes permitidos para la interfaz |
| `Worker:OutboxPollSeconds` | | ✔ | `2` | `2` | Pausa entre vueltas del outbox |
| `Worker:OutboxBatchSize` | | ✔ | `20` | `20` | Eventos por vuelta |
| `Worker:DailyCheckPollSeconds` | | ✔ | `30` | `5` | Pausa entre comprobaciones de la fecha |

`Demo:Enabled` debe ser `false` en un ambiente productivo: permite adelantar el reloj del sistema.

---

## Base de datos y migraciones

- **El esquema lo crea la API**, con las migraciones de EF Core en `src/RutaViva.Infrastructure/Persistence/Migrations`, cuando `Database:ApplyMigrationsOnStartup` es `true`. El Worker nunca migra, para que dos procesos no lo hagan a la vez.
- **Sin migraciones automáticas**, por ejemplo en un ambiente donde la API no debe tocar el esquema, aplique el script idempotente [`database/rutaviva-schema.sql`](../database/rutaviva-schema.sql) con `psql` o su cliente SQL. Se puede ejecutar varias veces.
- **Conectarse con un cliente SQL** (DBeaver, pgAdmin, psql): host `localhost`, puerto `5432` (o su `POSTGRES_PORT`), base `rutaviva`, usuario `rutaviva`, contraseña `rutaviva_demo_only` o la de su `.env`.

Desde `backend/`, tras `dotnet tool restore`:

```bash
# Crear una migración nueva
dotnet ef migrations add <Nombre> -p src/RutaViva.Infrastructure -s src/RutaViva.Infrastructure -o Persistence/Migrations

# Regenerar el script SQL idempotente
dotnet ef migrations script --idempotent -p src/RutaViva.Infrastructure -s src/RutaViva.Infrastructure -o ../database/rutaviva-schema.sql
```

Estos comandos no se conectan a la base de datos, así que no necesitan la cadena de conexión.

Modelo de datos completo: [`docs/database.md`](../docs/database.md).

---

## Pruebas

Desde `backend/`:

```bash
dotnet test                                     # todas (las de integración necesitan Docker encendido)
dotnet test test/RutaViva.Test.UnitTest         # dominio y casos de uso, sin infraestructura
dotnet test test/RutaViva.Test.ArchTest         # reglas de dependencia entre capas
dotnet test test/RutaViva.Test.IntegrationTest  # API + PostgreSQL real en un contenedor
```

Estrategia y matriz de reglas cubiertas: [`docs/testing.md`](../docs/testing.md).

---

## Estructura

```
backend/
├── src/
│   ├── RutaViva.Domain           Entidades, reglas de negocio y eventos; sin dependencias
│   ├── RutaViva.Application      Casos de uso y puertos (repositorios, unidad de trabajo, reloj)
│   ├── RutaViva.Infrastructure   EF Core + PostgreSQL, outbox, reloj de demo, migraciones
│   ├── RutaViva.Api              Host HTTP: controllers /api/v1, ProblemDetails, Scalar
│   └── RutaViva.Worker           Host de fondo: outbox y revisión diaria
├── test/
│   ├── RutaViva.Test.UnitTest
│   ├── RutaViva.Test.ArchTest
│   └── RutaViva.Test.IntegrationTest
├── Dockerfile                    Una imagen por destino: --target api | worker
└── global.json                   Versión del SDK
```

---

## Problemas comunes

| Síntoma | Causa y solución |
|---|---|
| `Missing connection string 'ConnectionStrings:RutaViva'` | Falta la variable del paso 3 en esa terminal. Defínala y vuelva a ejecutar. |
| `port is already allocated` al levantar Docker | Otro programa usa el puerto. Cambie `API_PORT`, `WEB_PORT` o `POSTGRES_PORT` en `.env`. |
| `Failed to connect to 127.0.0.1:5432` | PostgreSQL no está arriba. Ejecute `docker compose up -d postgres` y espere a que `docker compose ps` lo muestre `healthy`. |
| `relation "outbox_messages" does not exist` en los logs del Worker | La API todavía no aplicó las migraciones. Arranque la API primero; el Worker se recupera solo. |
| Los avisos no aparecen | El Worker no está corriendo. Revise `docker compose ps` o la terminal del Worker. |
| Endpoints `/api/v1/system/...` responden 404 | `Demo:Enabled` está en `false`. |
| Quiere borrar todos los datos | `docker compose down -v` borra el volumen de PostgreSQL; al volver a levantar, la API recrea el esquema y la demo. |
