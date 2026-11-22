# StudyHub · AWS re/Start

Portal de estudio que convierte el banco `AWS_reStart_102_preguntas.json` en una aplicación web: estudio por módulo, exámenes configurables, resultados, Error Log, reglas para memorizar, favoritas y progreso persistente por perfil.

> Las respuestas del banco son razonadas, **no son una clave oficial certificada**. Las 12 preguntas con estado `requiere_revision` (y la #33, cuya clave no coincide con «Seleccione DOS») se muestran con advertencia y se excluyen de los exámenes por defecto.

## Stack

| Capa | Tecnología |
|---|---|
| API | ASP.NET Core 8 (Domain / Application / Infrastructure / Api) + Swagger |
| Datos | EF Core 8 + SQLite (migraciones en `backend/src/StudyHub.Infrastructure/Migrations`) |
| Web | React 19 + TypeScript + Vite + Tailwind CSS + Lucide |
| Pruebas | xUnit (unitarias + integración con `WebApplicationFactory`) |

## Requisitos

- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/)

## Ejecutar en local

```bash
# 1) API (puerto 5020). Aplica migraciones y carga el banco semilla automáticamente.
cd backend/src/StudyHub.Api
dotnet run

# 2) Frontend en modo desarrollo (puerto 5173, con proxy a la API)
cd frontend
npm install
npm run dev
```

Abre http://localhost:5173. Swagger: http://localhost:5020/swagger.

### Una sola aplicación (producción local)

```bash
cd frontend && npm install && npm run build   # genera backend/src/StudyHub.Api/wwwroot
cd ../backend/src/StudyHub.Api && dotnet run  # sirve API + web en http://localhost:5020
```

### Docker

```bash
docker build -t studyhub .
docker run -p 8080:8080 -v studyhub-data:/data/db studyhub
```

## Pruebas

```bash
cd backend && dotnet test        # 33 pruebas: evaluación, dominio, validación, importación, API
cd frontend && npm run build && npm run lint
```

## Datos y configuración

- Banco semilla: `data/question-banks/AWS_reStart_102_preguntas.json` (copia intacta del original). Ruta configurable con `Seed:BankPath`.
- Base de datos: `ConnectionStrings:Default` (por defecto `studyhub.db` junto a la API). Para reiniciar el progreso, borra ese archivo.
- Perfiles: el frontend envía `X-Profile-Id`; sin cabecera se usa el primer perfil.
- No hay secretos en el repositorio; usa variables de entorno (`ConnectionStrings__Default`, `Seed__BankPath`).

## Reglas clave

- Selección única: exactamente una opción. Múltiple: igualdad de conjuntos (sin faltantes ni extras). Sin IA en la calificación.
- La API pública nunca envía claves, explicaciones ni reglas antes de comprobar (estudio/práctica) o finalizar (examen).
- «Dominada» = dos aciertos consecutivos. Las preguntas sin responder cuentan como incorrectas y se reportan aparte.
- Importar el mismo archivo es idempotente; los cambios crean nuevas versiones de pregunta y los exámenes guardan una instantánea.

## Pendiente (fases posteriores)

Importación desde PDF, enriquecimiento con IA (diagramas, explicaciones extendidas), pruebas E2E automatizadas y PostgreSQL.

La especificación original está en `docs/especificacion/`.
