# Examen Parcial — Plataforma de Incidencias

Sistema de gestión de averías en estaciones de bicicletas compartidas.

## Stack
- ASP.NET Core MVC + Identity, EF Core, SQLite
- Búsqueda: Algolia
- Caché: Redis
- Tiempo real: WebSocket vía PieHost/PieSocket

## Commit desplegado en Render
`16b4644` — agrega Dockerfile para despliegue en Render

## URLs
- Render: https://examen-incidencias.onrender.com
- GitHub: https://github.com/anthonir30mp-lab/examen-incidencias

## Credenciales de prueba
- Supervisor: `supervisor@ejemplo.com` / `Supervisor123!`

## Ramas y fusiones
- `feature/busqueda-algolia` (PR #2) → búsqueda por estación/descripción vía Algolia, filtrando solo incidencias abiertas existentes en BD.
- `feature/cache-redis` (PR #3) → caché de 60s del listado general, invalidada al cerrar una incidencia. Logs indican HIT/MISS.
- `feature/websocket-piehost` (PR #1) → al cerrar una incidencia, se persiste el estado y se publica el evento `IncidenciaActualizada` vía PieSocket; el cliente actualiza sin recargar.

Orden de fusión: A (Algolia) → B (Redis) → C (WebSocket). Ambos merges de B y C hacia main generaron conflictos en la línea de título compartida (`<h1>`), resueltos manteniendo las funciones acumuladas de cada rama anterior.

## Pruebas realizadas
- Búsqueda con Algolia: filtra correctamente por texto y descarta incidencias cerradas.
- Caché Redis: primera carga hace MISS y guarda; siguientes cargas hacen HIT durante 60s; se invalida al cerrar una incidencia.
- WebSocket/PieHost: al cerrar una incidencia desde una sesión, otra sesión abierta ve el cambio sin recargar.
- Deploy en Render: build vía Docker, variables de entorno configuradas sin exponer claves en el repo.
