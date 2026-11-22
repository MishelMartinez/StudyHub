# Flujos funcionales

## Navegación
```mermaid
flowchart TD
 A[Inicio] --> B[Mis cursos]
 B --> C[Módulos]
 C --> D[Estudiar]
 C --> E[Configurar examen]
 A --> E
 A --> F[Banco e importación]
 A --> G[Resultados y Error Log]
 G --> E
 D --> H[Reglas y favoritos]
 E --> I[Responder examen]
 I --> G
```
## Importación
```mermaid
flowchart TD
 A[PDF o JSON] --> B[Extraer y normalizar]
 B --> C[Validar estructura y respuestas]
 C --> D[Vista previa e inconsistencias]
 D --> E[Confirmar importación]
 E --> F[Versionar y persistir]
 F --> G[Módulos dinámicos]
 F --> H[Investigar y enriquecer]
 H --> J[Revisión de discrepancias]
 J --> K[Guardar contenido ampliado]
 G --> L[Estudio y exámenes]
 K --> L
```
## Estudio
```mermaid
flowchart TD
 A[Seleccionar curso y módulo] --> B[Mostrar pregunta sin clave]
 B --> C[Seleccionar opciones]
 C --> D[Comprobar en servidor]
 D --> E[Correcta o incorrecta]
 E --> F[Explicación, opciones, regla y diagrama]
 F --> G[Guardar intento y progreso]
 G --> H[Siguiente pregunta o repasar]
 H --> B
```
## Examen
```mermaid
flowchart TD
 A[Elegir módulos, filtros y cantidad] --> B[Calcular elegibles]
 B --> C{Cantidad válida?}
 C -- No --> A
 C -- Sí --> D[Seleccionar sin repetir y guardar sesión]
 D --> E[Responder y navegar]
 E --> F[Finalizar]
 F --> G[Evaluar en servidor]
 G --> H[Resultados y explicación]
 H --> I[Guardar errores y progreso]
 I --> J[Repetir errores]
```
Modo práctica: comprobar cada pregunta y revelar explicación; modo examen: mantener claves ocultas hasta finalizar. La composición y orden no cambian al recargar. El enriquecimiento es independiente de la disponibilidad del banco.
