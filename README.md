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
- **Entity Framework Core** with SQLite
- **.NET Aspire** for orchestration and observability
- **OpenTelemetry** for distributed tracing and metrics
- **Scalar** for API documentation (development)

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

- **Provider**: SQLite
- **Database File**: `roadtomillion.db` (created in the API project root)
- **Migrations**: Applied automatically on application startup
- **Cascade Deletes**: Enabled for all relationships

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
   - Start the API on `https://localhost:7100`
   - Start the Blazor WASM app on `https://localhost:7200`
   - Launch the Aspire Dashboard for monitoring

3. **Run individually** (Development)

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
    "DefaultConnection": "Data Source=./roadtomillion.db"
  }
}
```

**CORS**: Configured to allow requests from `https://localhost:7200`

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

**Optional Columns**:
- `Balance` - Initial balance amount

**Features**:
- Auto-detection of CSV delimiter (comma, semicolon, tab)
- Case-insensitive column matching
- Duplicate detection
- Row-level validation with warnings
- Preview before import

**Example CSV**:
```csv
AccountGroup,AccountName,Balance
Retirement,401k,50000.00
Retirement,Roth IRA,25000.00
Savings,Emergency Fund,10000.00
```

## 🔍 Observability

### Aspire Dashboard

Access the Aspire Dashboard (typically at `http://localhost:15888`) to view:
- **Traces**: Distributed request traces across services
- **Metrics**: Performance and runtime metrics
- **Logs**: Structured logs from all services
- **Resources**: Service status and health

### Health Checks

- **Liveness**: `GET /alive` - Basic application availability
- **Readiness**: `GET /health` - Detailed health check including database connectivity

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
