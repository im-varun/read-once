# Read Once (Self-Destructing Secret Sharing Service)

Read Once is a self-destructing secret sharing service that lets people send sensitive information through a link that can only be opened once, then disappears forever. It is built with C#, .NET, ASP.NET Core, and uses a Redis database for temporary storage of secrets.

## Features

- Create a secret with a TTL
- Read it exactly once - atomic delete-on-read

## Design Choices

- GUID-based ids for unguessability (122 bits of randomness)
- Redis GETDEL for atomic read-and-delete, no application-level locking needed

## Limitations

- Security depends entirely on the id/link being kept secret between sender and recipient - this system has no way to distinguish an authorized recipient from anyone else who obtains the id
- No persistence beyond Redis's own TTL - if Redis restarts/loses data before a secret is read, it's gone
- No rate limiting - nothing currently prevents abuse (spamming secret creation)
- No authentication/user accounts

## Running the Project Locally

### Prerequisites
- .NET SDK 10.0
- Docker (for running Redis locally) - or any local Redis instance

### 1. Clone the repository
```bash
git clone https://github.com/im-varun/read-once.git
cd read-once
```

### 2. Start Redis
```bash
docker run -d --name read-once-redis -p 6379:6379 redis:latest
```
Verify it's running:
```bash
docker exec -it read-once-redis redis-cli ping
# Expected: PONG
```

### 3. Configure the Redis connection string
The default `appsettings.json` already points to `localhost:6379`, so no changes are needed for local development:
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

### 4. Run the app
```bash
dotnet run
```
The API will start on the port shown in the console output (e.g., `http://localhost:5092`).

### 5. Try it out
```bash
# Create a secret
curl -i -X POST http://localhost:5092/secrets \
  -H "Content-Type: application/json" \
  -d '{"content": "the launch codes are 1234", "ttlSeconds": 60}'

# Read it once (replace {id} with the id returned above)
curl -i http://localhost:5092/secrets/{id}

# Read it again - should now return 404
curl -i http://localhost:5092/secrets/{id}
```

## Documentation

### `POST /secrets`
Creates a new secret with a time-to-live.

**Request body**
```json
{
  "content": "string, required, non-empty",
  "ttlSeconds": "integer, required, must be greater than 0"
}
```

**Responses**

| Status | Meaning |
|---|---|
| `201 Created` | Secret created successfully; returns `{ "id": "string" }` and a `Location` header |
| `400 Bad Request` | `content` was empty, or `ttlSeconds` was zero/negative |

---

### `GET /secrets/{id}`
Retrieves and permanently deletes a secret in one atomic operation. Can only ever succeed once per secret.

**Path parameter**

| Name | Type | Description |
|---|---|---|
| `id` | string (GUID) | The id returned when the secret was created |

**Responses**

| Status | Meaning |
|---|---|
| `200 OK` | Secret found and returned; returns `{ "content": "string" }`. The secret no longer exists after this response. |
| `404 Not Found` | Secret doesn't exist - either it was already read, it expired via TTL, or the id was invalid |

---

### `GET /health/redis`
Health check confirming connectivity to the Redis backend.

**Responses**

| Status | Meaning |
|---|---|
| `200 OK` | Redis is reachable; returns `{ "status": "connected", "latencyMs": number }` |
| `503 Service Unavailable` | Redis is unreachable |