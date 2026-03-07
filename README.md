# Road To Million 💰

A personal finance tracking application built with .NET Aspire, Blazor WebAssembly, and ASP.NET Core Web API. Track your journey to financial success by managing account groups, accounts, and balance snapshots over time.

## 🏗️ Architecture

This solution uses a modern distributed architecture orchestrated by .NET Aspire:

### Projects

- **RoadToMillion.Web** - Blazor WebAssembly client application
- **RoadToMillion.Api** - ASP.NET Core Web API backend
- **RoadToMillion.ServiceDefaults** - Shared Aspire service defaults (OpenTelemetry, health checks, service discovery)
- **RoadToMillion.AppHost** - Aspire orchestration host

### Technology Stack

- **.NET 10** with C# 14.0
- **Blazor WebAssembly** for the frontend
- **ASP.NET Core Web API** with minimal APIs
- **Entity Framework Core** with PostgreSQL
- **.NET Aspire** for orchestration and observability
- **OpenTelemetry** for distributed tracing and metrics
- **Scalar** for API documentation (development)
- **pgAdmin** for PostgreSQL database management (development)

## 📊 Data Model

The application uses a hierarchical structure to organize financial data:

```
AccountGroup (e.g., "Investments", "Savings")
  └─ Account (e.g., "401k", "Roth IRA")
      └─ BalanceSnapshot (timestamped balance records)
```

### Entities

- **AccountGroup**: Top-level categorization (e.g., Retirement, Investments, Cash)
  - Properties: `Id`, `Name`
  - Unique constraint on `Name`
  
- **Account**: Individual accounts with name and description
  - Properties: `Id`, `AccountGroupId`, `Name`, `Description`
  - Unique constraint on `AccountGroupId + Name`
  
- **BalanceSnapshot**: Point-in-time balance records with date and amount
  - Properties: `Id`, `AccountId`, `Amount`, `Date`, `RecordedAt`
  - Decimal precision: 18,2

### Database

- **Provider**: PostgreSQL (via Npgsql)
- **Orchestration**: Managed by .NET Aspire with automatic connection string injection
- **Container**: PostgreSQL runs in a Docker container orchestrated by Aspire
- **Migrations**: Applied automatically on application startup
- **Cascade Deletes**: Enabled for all relationships
- **Management**: pgAdmin available for database administration

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Visual Studio 2026](https://visualstudio.microsoft.com/) or later with Aspire workload
- (Optional) [.NET Aspire workload](https://learn.microsoft.com/dotnet/aspire/fundamentals/setup-tooling)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/toojla/RoadToMillion.git
   cd RoadToMillion
   ```

2. **Run with Aspire** (Recommended)
   ```bash
   dotnet run --project RoadToMillion.AppHost
   ```

   This will:
   - Start PostgreSQL in a Docker container
   - Start pgAdmin for database management
   - Start the API on `https://localhost:7100`
   - Start the Blazor WASM app on `https://localhost:7200`
   - Launch the Aspire Dashboard for monitoring

   **Note**: Docker Desktop must be running for PostgreSQL container orchestration.

3. **Run individually** (Development)

   **Prerequisites**: Start PostgreSQL manually
   ```bash
   docker run --name postgres-dev -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:latest
   ```

   Terminal 1 - API:
   ```bash
   dotnet run --project RoadToMillion.Api
   ```

   Terminal 2 - Web:
   ```bash
   dotnet run --project RoadToMillion.Web
   ```

## 🔧 Configuration

### API Configuration

**Connection String** (`appsettings.json`):
```json
{
  "ConnectionStrings": {
    "roadtomilliondb": "Host=localhost;Database=roadtomilliondb;Username=postgres;Password=postgres"
  }
}
```

**Note**: When running with Aspire, the connection string is automatically injected by the AppHost and doesn't need manual configuration.

**CORS**: Configured to allow requests from `https://localhost:7200`

### PostgreSQL with Aspire

The AppHost configures PostgreSQL with the following setup:

```csharp
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()  // Persists database data across container restarts
    .WithPgAdmin()
    .AddDatabase("roadtomilliondb");

var api = builder.AddProject<Projects.RoadToMillion_Api>("api")
    .WithReference(postgres)
    .WaitFor(postgres);
```

**Features**:
- Automatic PostgreSQL container provisioning
- **Persistent data storage** with Docker volumes (data survives container restarts)
- pgAdmin included for database management
- Connection string automatically injected into the API
- Health checks and observability integration

**Data Persistence**:
- Database data is stored in a Docker volume named `<app-name>-postgres-data`
- Data persists even when you stop or restart the AppHost
- To view volumes: `docker volume ls`
- To remove the volume (⚠️ deletes all data): `docker volume rm <volume-name>`

### Web Configuration

**API Base URL** (`appsettings.json`):
```json
{
  "ApiBaseUrl": "https://localhost:7100"
}
```

### Aspire Service Defaults

The `ServiceDefaults` project provides:
- **Service Discovery**: Automatic service-to-service communication
- **Resilience**: Standard retry and circuit breaker policies
- **Health Checks**: `/health` and `/alive` endpoints
- **OpenTelemetry**: Distributed tracing, metrics, and logging
  - ASP.NET Core instrumentation
  - HTTP client instrumentation
  - Runtime metrics

## 📁 Features

### API Endpoints

- **Portfolio** - Aggregate portfolio views
- **Account Groups** - CRUD operations for account groups
- **Accounts** - CRUD operations for accounts
- **Snapshots** - CRUD operations for balance snapshots
- **Import** - CSV import functionality

### CSV Import

The application supports importing financial data via CSV files with the following features:

**Required Columns**:
- `AccountGroup` - The account group name
- `AccountName` - The account name
- `Balance` - Balance amount (required for snapshots)

**Optional Columns**:
- `Date` - Date/time for the balance snapshot (if not provided, current date/time is used)

**Features**:
- Auto-detection of CSV delimiter (comma, semicolon, tab)
- Case-insensitive column matching
- **Multiple snapshots per account** - Same account can appear multiple times with different dates
- Row-level validation with warnings
- Preview before import
- Optional date/time support with multiple format detection
- **Historical data import** - Add balance snapshots to existing accounts

**Example CSV** (Multiple snapshots for the same account):
```csv
AccountGroup,AccountName,Balance,Date
Swedbank,Långtid,50000,2024-01-15
Swedbank,Långtid,25000,2025-01-15 10:30:00
Retirement,401k,75000.00,2024-06-01
Retirement,401k,78000.00,2024-12-01
Savings,Emergency Fund,10000.00
```

**Import Behavior**:
- **New accounts**: Creates the account group (if needed), account, and balance snapshot(s)
- **Existing accounts**: Adds new balance snapshot(s) without recreating the account
- **Multiple rows for same account**: Each row creates a separate balance snapshot
- This allows you to import historical balance data or update existing accounts with new balances

**Date Format Support**:
- ISO format: `2024-01-15` or `2024-01-15 10:30:00`
- Local formats (Swedish): `2024-01-15` or `15/01/2024`
- All dates are treated as UTC (timezone-aware) for consistency with PostgreSQL
- If Date is not provided or invalid, the current UTC date/time is used

## 🔍 Observability

### Aspire Dashboard

Access the Aspire Dashboard (typically at `http://localhost:15888`) to view:
- **Traces**: Distributed request traces across services
- **Metrics**: Performance and runtime metrics
- **Logs**: Structured logs from all services
- **Resources**: Service status and health
  - PostgreSQL container status
  - Database connection health

### pgAdmin

When running with Aspire, pgAdmin is automatically started and accessible from the Aspire Dashboard resources page.

**Access**:
1. Open Aspire Dashboard
2. Navigate to Resources
3. Click on the pgAdmin endpoint link

**Default credentials** (if needed):
- Email: `admin@admin.com`
- Password: `admin`

### Health Checks

- **Liveness**: `GET /alive` - Basic application availability
- **Readiness**: `GET /health` - Detailed health check including PostgreSQL database connectivity

## 🛠️ Development

### API Documentation

In development mode, API documentation is available via:
- **OpenAPI Spec**: `https://localhost:7100/openapi/v1.json`
- **Scalar UI**: `https://localhost:7100/scalar/v1`

### Database Migrations

To add a new migration:
```bash
cd RoadToMillion.Api
dotnet ef migrations add <MigrationName>
```

Migrations are applied automatically on startup via:
```csharp
db.Database.Migrate();
```

### Building the Solution

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

## 📝 Project Structure

```
RoadToMillion/
├── RoadToMillion.AppHost/          # Aspire orchestration
├── RoadToMillion.Api/              # Backend API
│   ├── Data/                       # DbContext and migrations
│   ├── Endpoints/                  # Minimal API endpoints
│   ├── Models/                     # Domain entities
│   └── Services/                   # Business logic (CSV import, etc.)
├── RoadToMillion.Web/              # Blazor WASM frontend
│   ├── Components/                 # Blazor components
│   ├── Services/                   # HTTP client services (ApiClient)
│   └── wwwroot/                    # Static assets
└── RoadToMillion.ServiceDefaults/  # Shared Aspire configuration
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is open source and available under the [MIT License](LICENSE).

## 🔗 Links

- [GitHub Repository](https://github.com/toojla/RoadToMillion)
- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Blazor Documentation](https://learn.microsoft.com/aspnet/core/blazor/)

---

**Built with ❤️ using .NET 10 and Aspire**
