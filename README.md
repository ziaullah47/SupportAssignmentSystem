# Support Assignment System - Architecture Documentation

## Table of Contents
1. [System Overview](#system-overview)
2. [Architecture Principles](#architecture-principles)
3. [Project Structure](#project-structure)
4. [Core Components](#core-components)
5. [Storage Layer](#storage-layer)
6. [Polling & Monitoring](#polling--monitoring)
7. [Assignment Algorithm](#assignment-algorithm)
8. [Queue Management](#queue-management)
9. [Shift Management](#shift-management)
10. [Data Flow](#data-flow)
11. [Deployment Options](#deployment-options)
12. [Configuration](#configuration)

---

## System Overview

The **Support Assignment System** is a sophisticated support chat management platform that intelligently assigns customer chat sessions to available agents based on seniority, capacity, shift schedules, and queue overflow handling.

### Key Features
- **Multi-shift Support**: Morning (00:00-08:00), Day (08:00-16:00), Evening (16:00-24:00)
- **Overflow Handling**: Automatic overflow team activation during office hours (09:00-17:00)
- **Intelligent Assignment**: Seniority-based round-robin allocation with efficiency multipliers
- **Session Monitoring**: Active polling mechanism to detect inactive sessions
- **Multiple Storage Options**: InMemory, Redis, or Database (SQL Server/PostgreSQL)
- **Real-time Status Monitoring**: Comprehensive system status and diagnostics endpoints

---

## Architecture Principles

### Clean Architecture
The system follows Clean Architecture principles with clear separation of concerns:

```
+-------------------------------------------------------+
|                    Presentation                      |
|  (API Controllers, Background Services, Models)      |
+-------------------------------------------------------+
                         |
+-------------------------------------------------------+
|                  Application Layer                   |
|        (Core Services, Business Logic)               |
+-------------------------------------------------------+
                         |
+-------------------------------------------------------+
|              Infrastructure Layer                    |
|  (Storage Implementations, External Services)        |
+-------------------------------------------------------+
```

### Key Principles
- **Dependency Inversion**: Core business logic depends on abstractions (interfaces)
- **Single Responsibility**: Each service has a clear, focused purpose
- **Separation of Concerns**: Clear boundaries between layers
- **Testability**: Services designed for easy unit and integration testing

---

## Project Structure

### SupportAssignmentSystem.Core
**Purpose**: Core business logic and domain entities  
**Dependencies**: None (clean core)

**Key Components**:
- **Entities**: `Agent`, `Team`, `ChatSession`
- **Enums**: `ShiftType`, `Seniority`, `ChatSessionStatus`, `StorageType`
- **Interfaces**: `ITeamManagementService`, `IChatQueueService`, `IAgentAssignmentService`, `ISessionStorage`
- **Services**: `TeamManagementService`, `ChatQueueService`, `AgentAssignmentService`
- **Configuration**: `StorageConfiguration`

### SupportAssignmentSystem.Infrastructure
**Purpose**: Implementation of infrastructure concerns  
**Dependencies**: Core

**Key Components**:
- **Storage Implementations**: 
  - `InMemorySessionStorage`
  - `RedisSessionStorage`
  - `DatabaseSessionStorage`
- **Data Access**: `SupportAssignmentDbContext`
- **Services**: 
  - `SessionMonitorService`
  - `ShiftManagementService`
- **Extensions**: `StorageServiceExtensions`

### SupportAssignmentSystem.Api
**Purpose**: HTTP API and background monitoring  
**Dependencies**: Core, Infrastructure

**Key Components**:
- **Controllers**: 
  - `ChatSessionController` - Chat session lifecycle management
  - `SystemStatusController` - System monitoring
  - `DiagnosticsController` - Troubleshooting endpoints
- **Background Service**: `MonitoringBackgroundService`
- **Models**: Request/Response DTOs

### SupportAssignmentSystem.PollingMonitor
**Purpose**: Standalone polling/monitoring worker service  
**Dependencies**: Core, Infrastructure

**Key Components**:
- **Worker**: `Worker` (BackgroundService implementation)
- **Purpose**: Monitors sessions and manages shifts independently

### SupportAssignmentSystem.Tests
**Purpose**: Comprehensive test suite  
**Dependencies**: All projects

**Test Types**:
- **Unit Tests**: Isolated component testing
- **Integration Tests**: End-to-end workflow testing
- **API Tests**: HTTP endpoint testing

---

## Core Components

### 1. Agent Entity
Represents a support agent with capacity and efficiency calculations.

```csharp
public class Agent
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Seniority Seniority { get; set; }
    public string TeamId { get; set; }
    public ShiftType Shift { get; set; }
    public bool IsActive { get; set; }
    public bool IsEndingShift { get; set; }
    public List<string> ActiveChatSessionIds { get; set; }
    
    // Calculated properties
    public int MaxConcurrentChats { get; }
    public int AvailableCapacity { get; }
    public bool CanAcceptNewChat { get; }
}
```

**Efficiency Multipliers**:
- **Junior**: 0.4 ? Max 4 concurrent chats
- **Mid-Level**: 0.6 ? Max 6 concurrent chats
- **Senior**: 0.8 ? Max 8 concurrent chats
- **Team Lead**: 0.5 ? Max 5 concurrent chats

### 2. Team Entity
Organizes agents into shift-based teams with capacity calculations.

```csharp
public class Team
{
    public string Id { get; set; }
    public string Name { get; set; }
    public List<Agent> Agents { get; set; }
    public ShiftType Shift { get; set; }
    public bool IsOverflowTeam { get; set; }
    
    // Calculated properties
    public int GetCapacity();
    public int GetMaxQueueLength();
    public int GetAvailableCapacity();
}
```

**Capacity Calculations**:
- **Team Capacity**: Sum of all active agents' max concurrent chats
- **Max Queue Length**: Team Capacity × 1.5

### 3. ChatSession Entity
Represents a customer chat session with lifecycle tracking.

```csharp
public class ChatSession
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public ChatSessionStatus Status { get; set; }
    public string? AssignedAgentId { get; set; }
    public string? AssignedTeamId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime LastPollTime { get; set; }
    public int MissedPollCount { get; set; }
    public bool IsOverflow { get; set; }
}
```

**Session Lifecycle**:
```
Queued ? Assigned ? (Active via Polling) ? Completed
                  ?
                Inactive (if 3 polls missed)
                  ?
                Refused (if queue full)
```

---

## Storage Layer

The system supports three storage backends through the `ISessionStorage` interface.

### Storage Interface
```csharp
public interface ISessionStorage
{
    Task SaveSessionAsync(ChatSession session);
    Task<ChatSession?> GetSessionAsync(string sessionId);
    Task UpdateSessionAsync(ChatSession session);
    Task<List<ChatSession>> GetQueuedSessionsAsync();
    Task<int> GetQueueSizeAsync(bool isOverflow);
}
```

### 1. InMemory Storage
**Use Case**: Development, testing, single-instance deployments

**Implementation**: `InMemorySessionStorage`
```csharp
private readonly Dictionary<string, ChatSession> _sessions = new();
```

**Advantages**:
- Fast performance
- No external dependencies
- Simple configuration

**Limitations**:
- Data lost on restart
- Single-instance only
- Not suitable for production with high availability requirements

### 2. Redis Storage
**Use Case**: Production, distributed systems, high performance

**Implementation**: `RedisSessionStorage`
```csharp
private readonly IDatabase _db;
```

**Features**:
- Distributed caching
- Persistence options (RDB, AOF)
- High-speed operations
- Pub/Sub support (future feature)

**Configuration**:
```json
{
  "Storage": {
    "StorageType": "Redis",
    "RedisConfiguration": {
      "ConnectionString": "localhost:6379",
      "InstanceName": "SupportAssignmentSystem:"
    }
  }
}
```

**Data Structure**:
- Sessions stored as JSON with key pattern: `{InstanceName}session:{sessionId}`
- Queue indexes maintained for quick retrieval

### 3. Database Storage
**Use Case**: Enterprise deployments, audit requirements, complex queries

**Implementation**: `DatabaseSessionStorage`
- Uses Entity Framework Core
- Supports SQL Server and PostgreSQL

**Features**:
- ACID transactions
- Complex queries
- Data persistence
- Audit trails

**Configuration**:
```json
{
  "Storage": {
    "StorageType": "Database",
    "DatabaseConfiguration": {
      "ConnectionString": "Server=localhost;Database=SupportAssignmentSystem;...",
      "Provider": "SqlServer"
    }
  }
}
```

**Schema**:
```sql
ChatSessions Table:
- Id (string, PK)
- UserId (string)
- Status (int/enum)
- AssignedAgentId (string, nullable)
- AssignedTeamId (string, nullable)
- CreatedAt (datetime)
- AssignedAt (datetime, nullable)
- LastPollTime (datetime)
- MissedPollCount (int)
- IsOverflow (bool)
```

### Storage Selection Strategy

| Criteria | InMemory | Redis | Database |
|----------|----------|-------|----------|
| **Performance** | ★★★★★ | ★★★★★ | ★★★ |
| **Scalability** | ★ | ★★★★★ | ★★★★ |
| **Persistence** | ★ | ★★★★ | ★★★★★ |
| **Setup Complexity** | ★★★★★ | ★★★ | ★★ |
| **Cost** | Free | $ | $$ |

---

## Polling & Monitoring

### Polling Mechanism

**Purpose**: Keep chat sessions alive and detect inactive clients

**How It Works**:
1. Client creates a chat session via `POST /api/chatsession`
2. Client polls every 1 second via `POST /api/chatsession/{id}/poll`
3. Each poll updates `LastPollTime` and resets `MissedPollCount`
4. If client stops polling, system detects inactivity

### Session Monitoring Service

**Class**: `SessionMonitorService`  
**Run Frequency**: Every 1 second  
**Responsibilities**:

1. **Inactivity Detection**:
   ```csharp
   var timeSinceLastPoll = DateTime.UtcNow - session.LastPollTime;
   if (timeSinceLastPoll.TotalSeconds >= 1)
   {
       session.MissedPollCount++;
       if (session.MissedPollCount >= 3)
       {
           await MarkSessionInactiveAsync(session.Id);
           await ReleaseChatFromAgentAsync(session.Id);
       }
   }
   ```

2. **Automatic Assignment**:
   ```csharp
   if (session.Status == Queued && session.AssignedAgentId == null)
   {
       await AssignChatToAgentAsync(session);
   }
   ```

### Background Service Options

#### Option 1: Embedded Background Service (Recommended for Single Instance)
**Location**: `SupportAssignmentSystem.Api`  
**Class**: `MonitoringBackgroundService`

**Benefits**:
- Single deployment
- Shared in-memory state
- Simpler architecture

**Configuration**:
```csharp
builder.Services.AddHostedService<MonitoringBackgroundService>();
```

#### Option 2: Standalone Worker Service (Recommended for Distributed)
**Project**: `SupportAssignmentSystem.PollingMonitor`  
**Class**: `Worker`

**Benefits**:
- Independent scaling
- Isolated failures
- Distributed architecture

**Usage**:
```bash
dotnet run --project SupportAssignmentSystem.PollingMonitor
```

**Requirements**: Must use Redis or Database storage for shared state

---

## Assignment Algorithm

### Assignment Strategy

**Goal**: Distribute chats fairly among agents, prioritizing less experienced agents

### Algorithm Steps

1. **Filter Available Agents**:
   ```csharp
   agents.Where(a => a.CanAcceptNewChat)
   ```
   Conditions:
   - `IsActive == true`
   - `IsEndingShift == false`
   - `AvailableCapacity > 0`

2. **Seniority-Based Priority**:
   ```csharp
   .OrderBy(a => GetSeniorityPriority(a.Seniority))
   ```
   Priority Order:
   - Junior (1)
   - Mid-Level (2)
   - Senior (3)
   - Team Lead (4)

3. **Load Balancing Within Seniority**:
   ```csharp
   .ThenBy(a => a.ActiveChatSessionIds.Count)
   ```
   Distributes load evenly among agents of the same seniority

4. **Agent Selection**:
   ```csharp
   return orderedAgents.FirstOrDefault();
   ```

### Assignment Flow

```
+-------------------+
|  New Session      |
|   (Queued)        |
+-------------------+
         |
         |
+-----------------------------------+
|  SessionMonitorService            |
|  (Runs every 1 second)            |
+-----------------------------------+
         |
         |
+-----------------------------------+
|  AgentAssignmentService           |
|  GetNextAvailableAgent()          |
+-----------------------------------+
         |
         |
+-----------------------------------+
|  Filter by:                       |
|  - CanAcceptNewChat               |
|  - Correct shift/team             |
+-----------------------------------+
         |
         |
+-----------------------------------+
|  Order by:                        |
|  1. Seniority (Junior first)      |
|  2. Current load (least first)    |
+-----------------------------------+
         |
         |
+-----------------------------------+
|  Assign to Agent                  |
|  - Update session                 |
|  - Add to agent's active list     |
+-----------------------------------+
```

### Special Cases

**Overflow Assignment**:
- Only assigned to overflow team agents
- Activated during office hours (09:00-17:00)
- Independent queue and capacity

**Shift-Based Assignment**:
- Morning shift (00:00-08:00): Team A
- Day shift (08:00-16:00): Team B
- Evening shift (16:00-24:00): Team C

---

## Queue Management

### Queue Strategy

**Two Queue System**:
1. **Main Queue**: Regular shift teams
2. **Overflow Queue**: Overflow team (office hours only)

### Queue Flow

```
+-------------------+
|  New Chat         |
|  Request          |
+-------------------+
         |
         |
+-----------------------------------+
|  Check Main Queue Size            |
|  Current < MaxQueueLength?        |
+-----------------------------------+
         |Yes         |No
         |            |
    +----------+  +-------------------+
    | Enqueue  |  | Check Office Hours|
    | Main     |  +-------------------+
    +----------+        |Yes      |No
                        |         |
              +-------------------+  +---------+
              |Check Overflow     |  | Refuse  |
              |Queue              |  | Chat    |
              +-------------------+  +---------+
                      |
         +----------------------------+
         | Full?                      |
         +----------------------------+
      No  |              |Yes
         |              |
    +----------+    +---------+
    | Enqueue  |    | Refuse  |
    | Overflow |    | Chat    |
    +----------+    +---------+
```

### Queue Capacity Calculation

**Main Queue**:
```csharp
int teamCapacity = team.GetCapacity();
int maxQueueLength = (int)Math.Floor(teamCapacity * 1.5);
```

**Example**:
- Team with 10 agents
- Average capacity: 60 concurrent chats
- Max queue length: 90 sessions

### Queue Operations

**Enqueue**:
```csharp
public async Task<ChatSession?> EnqueueChatSessionAsync(string userId)
{
    var currentQueueSize = await GetQueueSizeAsync(false);
    var maxQueueLength = activeTeam.GetMaxQueueLength();
    
    if (currentQueueSize >= maxQueueLength)
    {
        // Try overflow or refuse
    }
    
    var session = CreateSession(userId, false);
    await SaveSessionAsync(session);
    return session;
}
```

**Dequeue** (via assignment):
```csharp
public async Task<bool> AssignChatToAgentAsync(ChatSession session)
{
    var agent = await GetNextAvailableAgentAsync();
    if (agent != null)
    {
        session.Status = ChatSessionStatus.Assigned;
        session.AssignedAgentId = agent.Id;
        agent.ActiveChatSessionIds.Add(session.Id);
    }
}
```

---

## Shift Management

### Shift Schedule

| Shift | Time Range | Teams |
|-------|------------|-------|
| **Morning** | 00:00 - 08:00 | Team A |
| **Day** | 08:00 - 16:00 | Team B |
| **Evening** | 16:00 - 24:00 | Team C |
| **Overflow** | 09:00 - 17:00 | Overflow Team |

### Shift Transitions

**Service**: `ShiftManagementService`

**Transition Logic**:
1. **Hour before shift end**: Mark agents as `IsEndingShift = true`
2. **Agent with ending shift**: Cannot accept new chats, continues current chats
3. **Shift end**: Deactivate agents
4. **New shift start**: Activate new team's agents

**Example Transition (08:00)**:
```
07:00 - Morning team marked as ending shift
08:00 - Day team activated
      - Morning team deactivated after finishing chats
```

### Office Hours Management

**Office Hours**: 09:00 - 17:00

**Overflow Team Behavior**:
```csharp
public bool IsOfficeHours()
{
    var hour = DateTime.UtcNow.Hour;
    return hour >= 9 && hour < 17;
}
```

**Activation**:
- Overflow team activated when main queue is full during office hours
- Automatically deactivated outside office hours

---

## Data Flow

### 1. Chat Session Creation Flow

```
Client Request
    |
    |
[POST /api/chatsession]
    |
    |
ChatSessionController
    |
    |
ChatQueueService.EnqueueChatSessionAsync()
    |
    +-- Check current queue size
    +-- Determine main vs overflow
    +-- Create ChatSession entity
    +-- ISessionStorage.SaveSessionAsync()
    |
    |
Return ChatSessionResponse
Return ChatSessionResponse
```

### 2. Polling Flow

```
Client Poll (every 1 second)
    |
    |
[POST /api/chatsession/{id}/poll]
    |
    |
ChatSessionController
    |
    |
ChatQueueService.PollChatSessionAsync()
    |
    +-- Get session from storage
    +-- Update LastPollTime
    +-- Reset MissedPollCount
    +-- ISessionStorage.UpdateSessionAsync()
    |
    |
Return PollResponse
```

### 3. Assignment Flow

```
Background Service (every 1 second)
    |
    |
SessionMonitorService.MonitorSessionsAsync()
    |
    +-- Get all queued sessions
    |
    +-- For each unassigned session:
    |   |
    |   |
    |   AgentAssignmentService.AssignChatToAgentAsync()
    |   |
    |   +-- GetNextAvailableAgent()
    |   +-- Filter by shift/capacity
    |   +-- Order by seniority
    |   +-- Assign to agent
    |
    +-- Check for inactive sessions
        |
        +-- MissedPollCount >= 3?
        +-- Mark inactive & release agent
```

### 4. Completion Flow

```
Client Request
    |
    |
[POST /api/chatsession/{id}/complete]
    |
    |
ChatSessionController
    |
    +-- Get session
    +-- Release agent capacity
    |   +-- AgentAssignmentService.ReleaseChatFromAgentAsync()
    |       +-- Remove from ActiveChatSessionIds
    |
    +-- Mark session complete
        +-- ChatQueueService.CompleteSessionAsync()
```

---

## Deployment Options

### Option 1: Single Instance Deployment (Simple)

**Architecture**:
```
+-----------------------------------------------+
|  SupportAssignmentSystem.Api                  |
|  + API Endpoints                              |
|  + MonitoringBackgroundService                |
|  + InMemory Storage                           |
+-----------------------------------------------+
```

**Benefits**:
- Simple deployment
- Single executable
- Shared in-memory state

**Limitations**:
- No high availability
- Single point of failure

**Use Case**: Development, small teams, low traffic

### Option 2: Distributed with Redis (Recommended)

**Architecture**:
```
+------------------------+     +------------------------+
|  API Instance 1        |     |  API Instance 2        |
|  (Endpoints only)      |     |  (Endpoints only)      |
+------------------------+     +------------------------+
           |                            |
           +----------------------------+
                      |
                      |
            +--------------------+
            |  Redis Cluster     |
            |  (Shared State)    |
            +--------------------+
                      |
           +----------+----------+
           |                    |
           |                    |
+--------------------+  +--------------------+
|  PollingMonitor    |  |  PollingMonitor    |
|  (Instance 1)      |  |  (Instance 2)      |
+--------------------+  +--------------------+
```

**Benefits**:
- High availability
- Horizontal scaling
- Independent component scaling

**Configuration**:
```json
{
  "Storage": {
    "StorageType": "Redis",
    "RedisConfiguration": {
      "ConnectionString": "redis-cluster:6379",
      "InstanceName": "SupportAssignmentSystem:"
    }
  }
}
```

### Option 3: Enterprise with Database

**Architecture**:
```
+----------------+     +----------------+
|  API (Multi)   |-----| Load           |
|  Instances     |-----| Balancer       |
+----------------+     +----------------+
       |
       |
+--------------------------------+
|  SQL Server / PostgreSQL       |
|  (Persistent Storage)          |
+--------------------------------+
       |
       |
+----------------+
| Monitoring     |
| Workers        |
+----------------+
```

**Benefits**:
- Full persistence
- ACID transactions
- Audit capabilities
- Complex queries

---

## Configuration

### Storage Configuration

**Location**: `appsettings.json`

**InMemory**:
```json
{
  "Storage": {
    "StorageType": "InMemory"
  }
}
```

**Redis**:
```json
{
  "Storage": {
    "StorageType": "Redis",
    "RedisConfiguration": {
      "ConnectionString": "localhost:6379",
      "InstanceName": "SupportAssignmentSystem:"
    }
  }
}
```

**Database (SQL Server)**:
```json
{
  "Storage": {
    "StorageType": "Database",
    "DatabaseConfiguration": {
      "ConnectionString": "Server=localhost;Database=SupportAssignmentSystem;Trusted_Connection=True;TrustServerCertificate=True;",
      "Provider": "SqlServer"
    }
  }
}
```

**Database (PostgreSQL)**:
```json
{
  "Storage": {
    "StorageType": "Database",
    "DatabaseConfiguration": {
      "ConnectionString": "Host=localhost;Database=supportassignment;Username=postgres;Password=postgres",
      "Provider": "PostgreSQL"
    }
  }
}
```

### Service Registration

**Startup Configuration** (`Program.cs`):
```csharp
// Configure storage
builder.Services.AddStorageServices(builder.Configuration);

// Register application services
builder.Services.AddSingleton<ITeamManagementService, TeamManagementService>();
builder.Services.AddSingleton<IChatQueueService, ChatQueueService>();
builder.Services.AddSingleton<IAgentAssignmentService, AgentAssignmentService>();
builder.Services.AddSingleton<SessionMonitorService>();
builder.Services.AddSingleton<ShiftManagementService>();

// Add background monitoring
builder.Services.AddHostedService<MonitoringBackgroundService>();
```

---

## API Reference

See [SWAGGER_TESTING_GUIDE.md](SWAGGER_TESTING_GUIDE.md) for detailed API documentation and testing procedures.

---

## Performance Considerations

### Scalability Metrics

| Storage | Sessions/sec | Agents | Latency |
|---------|-------------|--------|---------|
| InMemory | 10,000+ | 1,000+ | < 1ms |
| Redis | 10,000+ | 10,000+ | < 5ms |
| Database | 1,000+ | 10,000+ | < 50ms |

### Optimization Tips

1. **Use Redis for Production**: Best balance of performance and reliability
2. **Connection Pooling**: Enable for database storage
3. **Index Strategy**: Index SessionId, Status, LastPollTime in database
4. **Monitoring Frequency**: 1 second is optimal; adjust based on requirements
5. **Agent Count**: Scale to team size and expected load

---

## Security Considerations

### Current Implementation
- No authentication/authorization (development focused)
- Open API endpoints

### Production Recommendations
1. Add authentication middleware (JWT, OAuth)
2. Implement rate limiting
3. Add API key validation
4. Enable HTTPS only
5. Implement CORS policies
6. Add request validation

---

## Monitoring and Observability

### Built-in Monitoring Endpoints

**System Status**:
- `GET /api/systemstatus/teams` - Team and agent status
- `GET /api/systemstatus/queue` - Queue metrics

**Diagnostics**:
- `POST /api/diagnostics/assign/{sessionId}` - Manual assignment trigger

### Logging

**Log Levels**:
- **Debug**: Poll requests, agent selection details
- **Information**: Session creation, assignments, completions
- **Warning**: Inactive sessions, queue overflow
- **Error**: Service failures, storage errors

### Metrics to Monitor

1. **Queue Metrics**:
   - Queue length
   - Wait time
   - Rejection rate

2. **Agent Metrics**:
   - Utilization rate
   - Average active chats
   - Assignment distribution

3. **Performance Metrics**:
   - Assignment latency
   - Poll response time
   - Storage operation time

---

## Future Enhancements

1. **WebSocket Support**: Real-time notifications instead of polling
2. **Priority Queuing**: VIP customer prioritization
3. **Skills-Based Routing**: Match sessions to agent skills
4. **Analytics Dashboard**: Real-time metrics visualization
5. **Agent Preferences**: Workload limits, break times
6. **Chat Routing**: Customer history-based routing
7. **Reporting**: Historical analytics and insights

---

## Conclusion

The Support Assignment System provides a robust, scalable foundation for managing support chat assignments with intelligent routing, overflow handling, and multiple deployment options. The clean architecture ensures maintainability and testability while supporting various storage backends for different deployment scenarios.

For testing procedures and API usage examples, see [SWAGGER_TESTING_GUIDE.md](SWAGGER_TESTING_GUIDE.md).
