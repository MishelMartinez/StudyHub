# Criterios verificables

- Importar el archivo original: 102 preguntas, 25 módulos, 78 única, 24 múltiple, 90 razonadas, 12 en revisión. Conservar textos, opciones, fuente y reglas.
- Reimportar sin duplicar. Actualizar versión sin perder historial. Detectar JSON malformado, ID duplicado, respuestas incompatibles y cardinalidad incoherente (por ejemplo pregunta 33).
- Pregunta 1: C correcta; A incorrecta. Pregunta 4: C+D correcta en cualquier orden; C sola o C+D+E incorrecta. Pregunta 30: visible con advertencia y excluida del examen por defecto.
- Generar 10 preguntas elegibles sin duplicados; rechazar 0, decimales o más del máximo; verificar filtros y composición tras recargar.
- Comprobar que claves y pistas no se exponen antes del momento permitido, incluso inspeccionando respuesta HTTP.
- Finalizar examen con pendientes; comprobar métricas, persistencia y revisión de errores; repetir errores; impedir doble finalización.
- Estudiar sin API de IA; simular error del proveedor y conservar contenido original. Fuentes faltantes no se presentan como verificadas.
- Agregar nuevo módulo mediante JSON sin editar frontend. Probar PDF legible y PDF no extraíble con revisión.
- Verificar vista móvil, teclado, contraste, iconos/textos de estado y diagramas sin recortes. Verificar aislamiento de perfiles si multiusuario.
- Ejecutar pruebas y documentar resultados reales; entregar instrucciones reproducibles.
