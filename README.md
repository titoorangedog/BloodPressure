# BloodPressure — Monitoraggio Pressione (Microservizi + Blazor PWA)

Repository completo per monitoraggio e registrazione dei valori pressori con microservizi .NET 10, EF Core, SQL Server, Docker Compose e Blazor PWA.

## Architettura
- **AuthService**: Google OAuth2, gestione utenti/ruoli, emissione JWT, licenza iniziale.
- **WriteService**: CRUD rilevazioni.
- **ReadService**: lettura dati, UserSettings, opzioni, admin endpoints.
- **StatsService**: statistiche e dashboard con gating licenza.
- **Web (Blazor PWA)**: UI protetta con login obbligatorio, banner licenza, grafici Chart.js.
- **SQL Server**: database unico con migrazioni EF Core e seed opzioni.

## Avvio rapido (Docker Compose)
1. Verifica di avere Docker e Docker Compose installati.
2. Configura `.env` (già creato) con:
   - `GOOGLE_CLIENT_ID`
   - `GOOGLE_CLIENT_SECRET`
   - `GOOGLE_CALLBACK_URL`
   - `WEBCLIENT_BASE_URL`
   - `JWT_SIGNING_KEY`
   - `SQL_SA_PASSWORD`

3. Avvia tutto:
```bash
docker compose up --build
```
4. UI: `http://localhost:5172`
5. Swagger:
   - AuthService: `http://localhost:7001/swagger`
   - WriteService: `http://localhost:7002/swagger`
   - ReadService: `http://localhost:7003/swagger`
   - StatsService: `http://localhost:7004/swagger`

## Google OAuth2 (configurazione)
Nel Google Cloud Console:
1. Crea un progetto + OAuth Client (tipo **Web application**).
2. Authorized redirect URIs:
   - `http://localhost:7001/auth/callback`
3. Inserisci ClientId/ClientSecret in `docker-compose.yml` o nelle variabili d’ambiente:
```
GoogleOAuth__ClientId=...
GoogleOAuth__ClientSecret=...
GoogleOAuth__CallbackUrl=http://localhost:7001/auth/callback
WebClient__BaseUrl=http://localhost:5172
```

## Ruoli e licenze
- Ruoli: `User`, `SuperUser`, `Admin`
- Regola Admin:
  - email `ferrara.giuseppe@gmail.com` ⇒ `Admin` forzato
- Licenze:
  - Nuovo utente ⇒ `Free` (90 giorni)
  - Una sola licenza attiva

**Admin** può gestire utenti e licenze tramite `ReadService`:
- `GET /admin/users`
- `GET /admin/users/{id}`
- `PUT /admin/users/{id}/role`
- `POST /admin/users/{id}/licenses`
- `POST /admin/users/{id}/licenses/{licenseId}/terminate`

## Sicurezza (OBBLIGATORIA)
Tutti gli endpoint di **WriteService**, **ReadService**, **StatsService** richiedono JWT Bearer.
- Nessun endpoint dati è pubblico.
- Uniche eccezioni:
  - `/auth/login-url`
  - `/auth/callback`
  - `/health`

## Migrazioni e seed
Le migrazioni EF Core sono in `src/BloodPressure.Persistence/Migrations`.
Seed automatico:
- SymptomOptions
- TimeSlotOptions
- SportActivityOptions

All’avvio, i servizi applicano `Database.Migrate()`.

## Test
Esegui:
```bash
dotnet test
```

Test minimi inclusi:
- classificazione soglie
- validazioni letture
- calcolo giorni licenza

## Note
- Progetto basato su .NET 10 preview (packages preview).
- Swagger UI supporta token Bearer.
- Blazor PWA reindirizza a Login se token assente/scaduto.
