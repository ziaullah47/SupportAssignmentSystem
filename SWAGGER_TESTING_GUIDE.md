# Support Assignment System - Swagger Testing Guide

## Table of Contents
1. [Getting Started](#getting-started)
2. [Swagger UI Overview](#swagger-ui-overview)
3. [API Endpoints Reference](#api-endpoints-reference)
4. [Testing Scenarios](#testing-scenarios)
5. [Common Use Cases](#common-use-cases)
6. [Troubleshooting](#troubleshooting)
7. [Advanced Testing](#advanced-testing)

---

## Getting Started

### Prerequisites
- .NET 9.0 SDK installed
- Visual Studio 2022 or VS Code (optional)
- Postman or similar tool (optional, for automated testing)

### Starting the API

**Option 1: Visual Studio**
1. Open the solution in Visual Studio
2. Set `SupportAssignmentSystem.Api` as startup project
3. Press F5 or click "Run"

**Option 2: Command Line**
```bash
cd src/SupportAssignmentSystem.Api
dotnet run
```

**Option 3: Watch Mode (auto-reload)**
```bash
cd src/SupportAssignmentSystem.Api
dotnet watch run
```

### Accessing Swagger UI

Once the API is running, navigate to:
- **HTTPS**: `https://localhost:5001`
- **HTTP**: `http://localhost:5000`

Swagger UI will open automatically at the root URL.

---

## Swagger UI Overview

### Interface Components

```
???????????????????????????????????????????????????????
?  Support Assignment System API                      ?
?  API for managing support chat sessions             ?
???????????????????????????????????????????????????????
?  Schemas ?                                          ?
?                                                     ?
?  ?? ChatSession                                    ?
?  ?   POST   /api/chatsession                       ? ?? Create new chat
?  ?   GET    /api/chatsession/{sessionId}           ? ?? Get session status
?  ?   POST   /api/chatsession/{sessionId}/poll      ? ?? Keep session alive
?  ?   POST   /api/chatsession/{sessionId}/complete  ? ?? Complete chat
?  ?                                                  ?
?  ?? SystemStatus                                   ?
?  ?   GET    /api/systemstatus/teams                ? ?? View team status
?  ?   GET    /api/systemstatus/queue                ? ?? View queue status
?  ?                                                  ?
?  ?? Diagnostics                                    ?
?  ?   POST   /api/diagnostics/assign/{sessionId}    ? ?? Manual assignment
???????????????????????????????????????????????????????
```

### Using Swagger UI

**Expanding Endpoints**:
- Click on any endpoint to expand details
- Shows request/response schemas
- Displays possible status codes

**Try It Out**:
1. Click "Try it out" button
2. Fill in parameters
3. Click "Execute"
4. View response below

**Understanding Responses**:
- **200**: Success
- **400**: Bad request (validation error)
- **404**: Resource not found
- **503**: Service unavailable (queue full)

---

## API Endpoints Reference

### 1. ChatSession Controller

#### POST /api/chatsession
**Purpose**: Create a new chat session and add to queue

**Request Body**:
```json
{
  "userId": "user123"
}
```

**Success Response (200)**:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Queued",
  "assignedAgentId": null,
  "createdAt": "2024-01-15T10:30:00Z",
  "assignedAt": null,
  "isOverflow": false
}
```

**Refused Response (503)**:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Refused",
  "assignedAgentId": null,
  "createdAt": "2024-01-15T10:30:00Z",
  "assignedAt": null,
  "isOverflow": false
}
```

**Validation Error (400)**:
```json
{
  "error": "UserId is required"
}
```

---

#### GET /api/chatsession/{sessionId}
**Purpose**: Retrieve current session status

**Path Parameters**:
- `sessionId` (string): The unique session identifier

**Success Response (200)**:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Assigned",
  "assignedAgentId": "agent-123",
  "createdAt": "2024-01-15T10:30:00Z",
  "assignedAt": "2024-01-15T10:30:05Z",
  "isOverflow": false
}
```

**Not Found (404)**:
```json
{
  "error": "Chat session not found"
}
```

---

#### POST /api/chatsession/{sessionId}/poll
**Purpose**: Keep session active by polling (call every 1 second)

**Path Parameters**:
- `sessionId` (string): The unique session identifier

**Success Response (200)**:
```json
{
  "success": true,
  "status": "Assigned",
  "assignedAgentId": "agent-123"
}
```

**Critical**: Client MUST call this endpoint every 1 second. Missing 3 consecutive polls will mark the session as inactive.

---

#### POST /api/chatsession/{sessionId}/complete
**Purpose**: Complete chat session and release agent

**Path Parameters**:
- `sessionId` (string): The unique session identifier

**Success Response (200)**:
```json
{
  "message": "Chat session completed successfully"
}
```

---

### 2. SystemStatus Controller

#### GET /api/systemstatus/teams
**Purpose**: Get current status of all teams and agents

**Success Response (200)**:
```json
{
  "currentTime": "2024-01-15T10:30:00Z",
  "currentHour": 10,
  "isOfficeHours": true,
  "teams": [
    {
      "id": "team-day",
      "name": "Team B - Day Shift",
      "shift": "Day",
      "isOverflow": false,
      "capacity": 60,
      "maxQueueLength": 90,
      "availableCapacity": 45,
      "agents": [
        {
          "id": "agent-1",
          "name": "Alice",
          "seniority": "Junior",
          "maxConcurrentChats": 4,
          "currentActiveChatCount": 2,
          "availableCapacity": 2,
          "isActive": true,
          "isEndingShift": false,
          "canAcceptNewChat": true,
          "efficiencyMultiplier": 0.4
        }
      ]
    }
  ]
}
```

**Use Cases**:
- Monitor system capacity
- View agent utilization
- Check shift status
- Identify bottlenecks

---

#### GET /api/systemstatus/queue
**Purpose**: Get current queue status and metrics

**Success Response (200)**:
```json
{
  "currentTime": "2024-01-15T10:30:00Z",
  "mainQueue": {
    "count": 15,
    "maxLength": 90,
    "utilizationPercentage": 16.67,
    "sessions": [
      {
        "id": "session-1",
        "userId": "user123",
        "status": "Queued",
        "waitTime": "00:00:05",
        "isOverflow": false
      }
    ]
  },
  "overflowQueue": {
    "count": 3,
    "maxLength": 30,
    "utilizationPercentage": 10.0,
    "isActive": true
  }
}
```

---

### 3. Diagnostics Controller

#### POST /api/diagnostics/assign/{sessionId}
**Purpose**: Manually trigger assignment for a session (debugging)

**Path Parameters**:
- `sessionId` (string): The unique session identifier

**Success Response (200)**:
```json
{
  "success": true,
  "message": "Session assigned successfully",
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "assignedAgentId": "agent-123",
  "assignedTeamId": "team-day",
  "assignedAt": "2024-01-15T10:30:05Z"
}
```

**No Agents Available (200)**:
```json
{
  "success": false,
  "message": "No available agents",
  "sessionId": "550e8400-e29b-41d4-a716-446655440000",
  "diagnostics": {
    "currentTime": "2024-01-15T10:30:00Z",
    "currentHour": 10,
    "currentShift": "Day",
    "isOfficeHours": true,
    "sessionIsOverflow": false,
    "availableAgents": []
  }
}
```

---

## Testing Scenarios

### Scenario 1: Basic Chat Session Flow

**Objective**: Test complete session lifecycle

**Steps**:

1. **Check System Status**
   ```
   GET /api/systemstatus/teams
   ```
   - Verify teams are active
   - Confirm available capacity > 0

2. **Create Chat Session**
   ```
   POST /api/chatsession
   Body: { "userId": "testuser1" }
   ```
   - Expected: Status 200, Status = "Queued"
   - Copy the `id` from response

3. **Start Polling** (repeat every 1 second)
   ```
   POST /api/chatsession/{id}/poll
   ```
   - Expected: Status 200, success = true
   - After 1-2 seconds, check for agent assignment

4. **Check Assignment**
   ```
   GET /api/chatsession/{id}
   ```
   - Expected: Status = "Assigned"
   - `assignedAgentId` should be populated

5. **Continue Polling**
   - Keep calling poll endpoint every 1 second
   - Simulates active chat

6. **Complete Session**
   ```
   POST /api/chatsession/{id}/complete
   ```
   - Expected: Status 200
   - Agent capacity is released

7. **Verify Agent Released**
   ```
   GET /api/systemstatus/teams
   ```
   - Agent's `currentActiveChatCount` should decrease

**Expected Duration**: ~10-15 seconds

---

### Scenario 2: Testing Inactive Session Detection

**Objective**: Verify system detects inactive clients

**Steps**:

1. **Create Chat Session**
   ```
   POST /api/chatsession
   Body: { "userId": "inactivetest" }
   ```

2. **Poll Once**
   ```
   POST /api/chatsession/{id}/poll
   ```

3. **Stop Polling** (simulate client disconnect)
   - Wait 5 seconds without polling

4. **Check Session Status**
   ```
   GET /api/chatsession/{id}
   ```
   - Expected: Status = "Inactive"
   - Agent should be released

5. **Verify in Queue Status**
   ```
   GET /api/systemstatus/queue
   ```
   - Session should not appear in active queue

**Expected Duration**: ~5-10 seconds

---

### Scenario 3: Queue Overflow Testing

**Objective**: Test overflow team activation

**Prerequisites**: 
- Understand current team capacity
- Calculate number of sessions needed to fill queue

**Steps**:

1. **Check Team Capacity**
   ```
   GET /api/systemstatus/teams
   ```
   - Note `maxQueueLength` for main team
   - Example: If maxQueueLength = 90, need 90+ sessions

2. **Fill Main Queue**
   ```
   Loop: Create 90+ sessions
   POST /api/chatsession
   Body: { "userId": "loadtest{i}" }
   ```

3. **Verify Overflow Activation**
   ```
   GET /api/systemstatus/teams
   ```
   - Overflow team should be active
   - Check `isActive: true` for overflow agents

4. **Create Overflow Session**
   ```
   POST /api/chatsession
   Body: { "userId": "overflowtest" }
   ```
   - Expected: `isOverflow: true` in response

5. **Verify Overflow Assignment**
   ```
   GET /api/chatsession/{overflowSessionId}
   ```
   - Should be assigned to overflow team agent

6. **Test Queue Full**
   - Fill overflow queue to max
   - Create one more session
   - Expected: Status 503 (Service Unavailable)

**Expected Duration**: Varies based on capacity

**Note**: This test requires time as assignments happen every 1 second

---

### Scenario 4: Shift Transition Testing

**Objective**: Verify shift changes work correctly

**Steps**:

1. **Check Current Shift**
   ```
   GET /api/systemstatus/teams
   ```
   - Note `currentHour` and active team

2. **Create Session Before Shift End**
   - If current hour is 7 (near shift end at 8):
   ```
   POST /api/chatsession
   Body: { "userId": "shifttest" }
   ```

3. **Monitor Shift Transition**
   - At hour 7 (1 hour before shift end):
   ```
   GET /api/systemstatus/teams
   ```
   - Check `isEndingShift: true` for current team agents

4. **Verify New Assignment Blocking**
   ```
   POST /api/chatsession
   Body: { "userId": "endingshifttest" }
   ```
   - Agents with `isEndingShift: true` should not receive new chats

5. **Wait for Shift Change**
   - At hour 8:
   ```
   GET /api/systemstatus/teams
   ```
   - Old team should be inactive
   - New team should be active

6. **Create Session in New Shift**
   ```
   POST /api/chatsession
   Body: { "userId": "newshifttest" }
   ```
   - Should be assigned to new shift team

**Expected Duration**: Requires testing near actual shift boundaries

**Tip**: Modify shift times in code for faster testing

---

### Scenario 5: Seniority-Based Assignment

**Objective**: Verify junior agents are prioritized

**Steps**:

1. **Check Agent Seniority**
   ```
   GET /api/systemstatus/teams
   ```
   - Identify junior agents
   - Note their IDs and current load

2. **Create Multiple Sessions**
   ```
   Loop 5 times:
   POST /api/chatsession
   Body: { "userId": "senioritytest{i}" }
   ```

3. **Wait for Assignments** (5-10 seconds)

4. **Check Assignment Distribution**
   ```
   GET /api/systemstatus/teams
   ```
   - Junior agents should have more assignments
   - Verify load is distributed

5. **Analyze Agent Loads**
   ```
   For each agent:
   - Junior agents: Should be near capacity
   - Mid-level: Partially loaded
   - Senior: Minimal load (unless juniors are full)
   ```

**Expected Behavior**:
- Junior agents (0.4 multiplier) get assignments first
- Load balanced within same seniority level

---

## Common Use Cases

### Use Case 1: Load Testing

**Objective**: Test system under high load

**Tools**: 
- Postman Collection Runner
- Apache JMeter
- Custom script

**Sample Postman Collection**:

1. Create Collection: "Load Test"

2. Add Request: "Create Session"
   ```
   POST {{baseUrl}}/api/chatsession
   Body: { "userId": "loadtest-{{$randomUUID}}" }
   ```

3. Add Request: "Poll Session"
   ```
   POST {{baseUrl}}/api/chatsession/{{sessionId}}/poll
   ```

4. Configure Runner:
   - Iterations: 100
   - Delay: 100ms
   - Data file: user_ids.csv

5. Monitor via:
   ```
   GET /api/systemstatus/queue
   GET /api/systemstatus/teams
   ```

**Metrics to Track**:
- Queue length over time
- Assignment rate
- Agent utilization
- Response times
- Error rates

---

### Use Case 2: Monitoring Dashboard Simulation

**Objective**: Build real-time monitoring display

**Implementation**:

```javascript
// Sample JavaScript for monitoring dashboard

setInterval(async () => {
  // Fetch team status
  const teamResponse = await fetch('/api/systemstatus/teams');
  const teamData = await teamResponse.json();
  
  // Fetch queue status
  const queueResponse = await fetch('/api/systemstatus/queue');
  const queueData = await queueResponse.json();
  
  // Update dashboard
  updateDashboard(teamData, queueData);
}, 5000); // Update every 5 seconds

function updateDashboard(teams, queue) {
  // Display metrics
  console.log(`Queue Length: ${queue.mainQueue.count}`);
  console.log(`Active Agents: ${teams.teams.flatMap(t => t.agents).filter(a => a.isActive).length}`);
  console.log(`Average Utilization: ${calculateUtilization(teams)}`);
}
```

---

### Use Case 3: Client Simulation

**Objective**: Simulate realistic client behavior

**Python Script Example**:

```python
import requests
import time
import threading

BASE_URL = "http://localhost:5000/api"

class ChatClient:
    def __init__(self, user_id):
        self.user_id = user_id
        self.session_id = None
        self.running = False
    
    def start_chat(self):
        # Create session
        response = requests.post(
            f"{BASE_URL}/chatsession",
            json={"userId": self.user_id}
        )
        data = response.json()
        self.session_id = data['id']
        print(f"Session created: {self.session_id}")
        
        # Start polling
        self.running = True
        threading.Thread(target=self.poll_loop).start()
    
    def poll_loop(self):
        while self.running:
            response = requests.post(
                f"{BASE_URL}/chatsession/{self.session_id}/poll"
            )
            data = response.json()
            
            if data.get('assignedAgentId'):
                print(f"Assigned to agent: {data['assignedAgentId']}")
            
            time.sleep(1)  # Poll every 1 second
    
    def end_chat(self):
        self.running = False
        requests.post(f"{BASE_URL}/chatsession/{self.session_id}/complete")
        print(f"Chat completed: {self.session_id}")

# Usage
client = ChatClient("pythonuser123")
client.start_chat()
time.sleep(30)  # Chat for 30 seconds
client.end_chat()
```

---

## Troubleshooting

### Issue 1: Sessions Not Getting Assigned

**Symptoms**:
- Sessions stay in "Queued" status
- No agent assignment after 10+ seconds

**Checks**:

1. **Verify Background Service Running**
   - Check console logs for "Monitoring Background Service started"
   - If missing, monitoring service may not be running

2. **Check Agent Availability**
   ```
   GET /api/systemstatus/teams
   ```
   - Verify `canAcceptNewChat: true` for at least one agent
   - Check `availableCapacity > 0`

3. **Verify Correct Shift**
   - Check `currentHour` and `shift` in system status
   - Ensure active team matches current shift

4. **Check for Ending Shift**
   - If near shift boundary, agents may be ending shift
   - Wait for next shift to start

**Solution**:
- Restart API if background service not running
- Adjust system time if testing shift transitions
- Complete some sessions to free capacity

---

### Issue 2: Polling Not Working

**Symptoms**:
- Session marked as inactive
- Agent released unexpectedly

**Checks**:

1. **Verify Poll Frequency**
   - Must poll within 1 second intervals
   - Check client-side timer

2. **Check Session ID**
   - Ensure correct session ID in poll request
   - Verify session exists: `GET /api/chatsession/{id}`

3. **Network Issues**
   - Check for network delays
   - Monitor request/response times

**Solution**:
- Increase poll frequency if network is slow
- Add retry logic for failed polls
- Use WebSocket connection (future feature)

---

### Issue 3: Queue Always Full

**Symptoms**:
- All new sessions get 503 status
- `status: "Refused"` in response

**Checks**:

1. **Check Queue Utilization**
   ```
   GET /api/systemstatus/queue
   ```
   - Main queue and overflow queue percentages

2. **Verify Office Hours**
   - Overflow only active 09:00-17:00
   - Check `isOfficeHours` in system status

3. **Check Agent Capacity**
   ```
   GET /api/systemstatus/teams
   ```
   - Verify agents have capacity
   - Check if agents are ending shift

**Solution**:
- Complete some sessions: `POST /api/chatsession/{id}/complete`
- Wait for session assignments to process
- Adjust team capacity in code if needed

---

### Issue 4: Overflow Not Activating

**Symptoms**:
- Main queue full but overflow not used
- Sessions refused despite overflow team

**Checks**:

1. **Verify Office Hours**
   ```
   GET /api/systemstatus/teams
   ```
   - Check `isOfficeHours: true`
   - Must be between 09:00-17:00

2. **Check Overflow Team**
   - Verify overflow team exists
   - Check agents are configured

3. **Check Current Time**
   - Overflow only activates during office hours

**Solution**:
- Test during office hours (09:00-17:00)
- Modify office hours logic for testing
- Verify overflow team initialization

---

## Advanced Testing

### Performance Testing

**Metrics to Measure**:

1. **Throughput**
   - Sessions created per second
   - Assignments per second

2. **Latency**
   - Session creation time
   - Assignment delay
   - Poll response time

3. **Capacity**
   - Maximum concurrent sessions
   - Maximum queue length
   - Agent utilization rate

**Testing Tools**:

**Apache JMeter Script**:
```xml
<ThreadGroup>
  <numThreads>100</numThreads>
  <rampUp>10</rampUp>
  <HTTPRequest>
    <path>/api/chatsession</path>
    <method>POST</method>
    <body>{"userId": "jmeter-${__threadNum}"}</body>
  </HTTPRequest>
</ThreadGroup>
```

**k6 Load Test**:
```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  vus: 100,
  duration: '30s',
};

export default function() {
  let payload = JSON.stringify({
    userId: `k6-${__VU}-${__ITER}`
  });
  
  let response = http.post(
    'http://localhost:5000/api/chatsession',
    payload,
    { headers: { 'Content-Type': 'application/json' } }
  );
  
  check(response, {
    'status is 200': (r) => r.status === 200,
  });
  
  sleep(1);
}
```

---

### Automated Test Suite

**Postman Collection Export**:

Save as `support-assignment-tests.json`:

```json
{
  "info": {
    "name": "Support Assignment System Tests",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "1. Create Session",
      "event": [
        {
          "listen": "test",
          "script": {
            "exec": [
              "pm.test('Status is 200', function() {",
              "  pm.response.to.have.status(200);",
              "});",
              "",
              "pm.test('Session ID returned', function() {",
              "  var json = pm.response.json();",
              "  pm.expect(json.id).to.be.a('string');",
              "  pm.collectionVariables.set('sessionId', json.id);",
              "});"
            ]
          }
        }
      ],
      "request": {
        "method": "POST",
        "header": [],
        "body": {
          "mode": "raw",
          "raw": "{\"userId\": \"{{$randomFullName}}\"}"
        },
        "url": "{{baseUrl}}/api/chatsession"
      }
    },
    {
      "name": "2. Poll Session",
      "event": [
        {
          "listen": "test",
          "script": {
            "exec": [
              "pm.test('Poll successful', function() {",
              "  var json = pm.response.json();",
              "  pm.expect(json.success).to.be.true;",
              "});"
            ]
          }
        }
      ],
      "request": {
        "method": "POST",
        "url": "{{baseUrl}}/api/chatsession/{{sessionId}}/poll"
      }
    },
    {
      "name": "3. Get Session Status",
      "event": [
        {
          "listen": "test",
          "script": {
            "exec": [
              "pm.test('Session found', function() {",
              "  pm.response.to.have.status(200);",
              "});",
              "",
              "pm.test('Has valid status', function() {",
              "  var json = pm.response.json();",
              "  pm.expect(['Queued', 'Assigned', 'Completed']).to.include(json.status);",
              "});"
            ]
          }
        }
      ],
      "request": {
        "method": "GET",
        "url": "{{baseUrl}}/api/chatsession/{{sessionId}}"
      }
    }
  ]
}
```

**Run via Newman (Postman CLI)**:
```bash
newman run support-assignment-tests.json \
  --environment env.json \
  --reporters cli,html \
  --reporter-html-export results.html
```

---

## Best Practices

### 1. Testing Workflow

**Recommended Order**:
1. Check system status first
2. Verify capacity available
3. Create sessions
4. Monitor assignment process
5. Test polling mechanism
6. Complete sessions
7. Verify cleanup

### 2. Data Management

**Session IDs**:
- Save session IDs for reuse
- Use collection variables in Postman
- Implement cleanup after tests

**Test Data**:
- Use unique user IDs for each test
- Include timestamp in user IDs
- Clear test data between test runs

### 3. Timing Considerations

**Poll Timing**:
- Must poll every 1 second
- Use timers/intervals
- Account for network latency

**Assignment Timing**:
- Background service runs every 1 second
- Allow 2-3 seconds for assignment
- Don't expect instant assignment

### 4. Error Handling

**Expected Errors**:
- 404: Session not found (expected for invalid IDs)
- 503: Queue full (expected during overflow testing)
- 400: Validation errors (expected for missing fields)

**Unexpected Errors**:
- 500: Internal server error (investigate logs)
- Timeout: Check background service
- Connection refused: Verify API is running

---

## Quick Reference

### HTTP Status Codes

| Code | Meaning | When It Occurs |
|------|---------|----------------|
| 200 | OK | Successful operation |
| 400 | Bad Request | Invalid input (missing userId) |
| 404 | Not Found | Session ID doesn't exist |
| 503 | Service Unavailable | All queues full (refused) |

### Session States

| Status | Description | Next State |
|--------|-------------|------------|
| Queued | Waiting for agent | Assigned or Inactive |
| Assigned | Agent assigned | Completed or Inactive |
| Completed | Chat finished | Terminal state |
| Inactive | Client disconnected | Terminal state |
| Refused | Queue full | Terminal state |

### Agent Seniority Capacity

| Seniority | Multiplier | Max Chats |
|-----------|------------|-----------|
| Junior | 0.4 | 4 |
| Mid-Level | 0.6 | 6 |
| Senior | 0.8 | 8 |
| Team Lead | 0.5 | 5 |

### Shift Schedule

| Shift | Hours (UTC) | Team |
|-------|-------------|------|
| Morning | 00:00 - 08:00 | Team A |
| Day | 08:00 - 16:00 | Team B |
| Evening | 16:00 - 24:00 | Team C |
| Overflow | 09:00 - 17:00 | Overflow Team |

---

## Conclusion

This guide provides comprehensive instructions for testing the Support Assignment System via Swagger UI. Follow the scenarios to understand system behavior, use the troubleshooting section to resolve issues, and apply best practices for reliable testing.

For architectural details and system design, see [ARCHITECTURE.md](ARCHITECTURE.md).

### Additional Resources

- **API Source Code**: `src/SupportAssignmentSystem.Api/Controllers/`
- **Service Logic**: `src/SupportAssignmentSystem.Core/Services/`
- **Test Examples**: `src/SupportAssignmentSystem.Tests/`

### Support

For issues or questions:
1. Check console logs for errors
2. Review system status endpoints
3. Verify configuration in appsettings.json
4. Check background service is running

Happy Testing! ??
