# attendanceCollector.WebApp

Standalone TypeScript and Vite frontend for the attendance collector API.

## Development

Run the API at `http://localhost:5200` using the API `http` launch profile, then:

```powershell
npm ci
npm run dev
```

Open `http://localhost:4210`. Vite proxies authentication, API, health, and Keycloak callback paths to the API, so browser cookies stay on the frontend origin. Port `4200` is reserved for the HRM project.

## Production build

```powershell
npm ci
npm run build
```

The Dockerfile runs this build and copies `dist/` into the API static web root. In the deployed Compose stack the frontend is served from the same origin as the API.
