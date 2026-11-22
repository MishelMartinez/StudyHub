# Arquitectura de StudyHub (MVP)

## Capas

```
frontend (React + TS + Vite)  ──HTTP /api──►  StudyHub.Api (controladores, middleware, Swagger)
                                                   │
                                                   ▼
                                       StudyHub.Application (servicios, DTOs, validación, importación)
                                                   │
                                     ┌─────────────┴─────────────┐
                                     ▼                           ▼
                           StudyHub.Domain               StudyHub.Infrastructure
                   (entidades, Grader, Mastery)      (EF Core + SQLite, migraciones, seed)
```

- **Domain**: entidades y reglas puras (`Grader.IsCorrect`, `Mastery.From`). Sin dependencias.
- **Application**: `ImportService` + `BankValidator`, `StudyService`, `ExamService`, `CatalogService`, `AdminService`, `ProfileService`. Depende solo de `IAppDbContext` e `ICurrentProfile`.
- **Infrastructure**: `AppDbContext` (SQLite), migraciones y `DatabaseInitializer` (migra, crea el perfil inicial e importa el banco semilla si la base está vacía).
- **Api**: controladores delgados, `ProfileMiddleware` (`X-Profile-Id`), `ErrorMiddleware` (ProblemDetails en español) y archivos estáticos del frontend compilado.

## Modelo de datos

| Entidad | Propósito |
|---|---|
| `Course` | Curso generado desde `titulo` del JSON. |
| `BankVersion` | Cada importación: hash SHA-256, contenido crudo, informe. |
| `Module` | Generado dinámicamente desde `modulo`. |
| `Question` / `QuestionOption` | Versión vigente de cada pregunta (`OriginalId` = `id` del JSON). |
| `QuestionVersion` | Instantánea JSON de cada versión de pregunta. |
| `UserProfile` | Perfil local con umbral de aprobación. |
| `Attempt` | Cada respuesta (estudio, práctica o examen). Alimenta progreso y Error Log. |
| `QuestionMark` | Favoritas y «para repasar» por perfil. |
| `Exam` / `ExamQuestion` | Configuración, orden y opciones congeladas, respuestas y resultado. |

## Decisiones

- **Sin fugas de respuestas**: `QuestionPublicDto` y `ExamQuestionDto` no tienen clave; `FeedbackDto` solo se emite tras comprobar o finalizar.
- **Banco intacto**: las incoherencias (p. ej. #33) se reportan como hallazgos y la pregunta queda «en revisión»; nunca se corrige la clave.
- **Idempotencia y versiones**: mismo hash ⇒ `sin_cambios`; contenido distinto ⇒ nueva `QuestionVersion`; preguntas ausentes se conservan.
- **Exámenes reproducibles**: cada `ExamQuestion` guarda una instantánea; recargar no cambia composición ni orden de opciones; no se puede finalizar dos veces.
