# Identidad y diagramas educativos

Consultar `resources/design/referencia-diagramas.png` y `paleta-identidad.png`. El ejemplo de malware muestra iconos lineales sobre fondo claro, etiquetas, descripciones breves y flujo convergente con flechas. Reproducir esa gramática visual, adaptada al contenido y la paleta.

| Color | Uso |
|---|---|
| #221A40 | Texto principal, encabezados, navegación |
| #D95F98 | Acentos e iconos interactivos |
| #F2AEB4 | Fondos secundarios |
| #F29422 | Advertencia, concepto clave |
| #D9483B | Error, riesgo |

Usar texto morado oscuro sobre fondos claros y verificar contraste en botones; no depender solo de colores para acierto/error. SVG responsivo con viewBox, título y descripción accesible. Iconos Lucide o equivalentes, sin fotos, con espacio suficiente y conectores claros. Diagramas comparativos, de arquitectura, jerarquía, procesos y causas. Renderizar desde un esquema validado de nodos y aristas; no ejecutar SVG o HTML arbitrario generado por IA. Preferir componentes seguros o Mermaid en modo estricto. Guardar por concepto y versión, asociar a preguntas. Incluir alternativa textual.

Ejemplos SVG en `examples/`: responsabilidad compartida, flujo del portal y durabilidad S3. Son referencias visuales de implementación; el contenido educativo ampliado debe verificarse y guardar sus fuentes antes de marcarlo validado.
