# Read Once (Self-Destructing Secret Sharing Service)

Read Once is a self-destructing secret sharing service that lets people send sensitive information through a link that can only be opened once, then disappears forever. It is built with C#, .NET, ASP.NET Core, and uses a Redis database for temporary storage of secrets.

## Features

- Create a secret with a TTL
- Read it exactly once - atomic delete-on-read
- JWT-based authentication
- Per-user secret history

## Design Choices

- GUID-based ids for unguessability (122 bits of randomness)
- Redis GETDEL for atomic read-and-delete, no application-level locking needed
- Secret metadata stores an `ownerId` and uses the same TTL as the secret. The persistent `user:{userId}:secrets` set may contain ids whose metadata has expired, so history reads defensively skip stale set entries.

## Limitations

- Reading a secret remains intentionally unauthenticated: anyone with its id/link can redeem it. The link is the system's actual security boundary.
- No persistence beyond Redis's own TTL - if Redis restarts/loses data before a secret is read, it's gone
- No rate limiting - nothing currently prevents abuse (spamming secret creation)

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

### 4. Configure the JWT signing key
Keep the signing key out of `appsettings.json`. Store a key of at least 32 bytes in .NET User Secrets:
```bash
dotnet user-secrets set "Jwt:SigningKey" "replace-with-a-long-random-secret-at-least-32-bytes"
```

### 5. Run the app
```bash
dotnet run
```
The API will start on the port shown in the console output (e.g., `http://localhost:5092`).

### 6. Try it out
```bash
# Register and log in
curl -i -X POST http://localhost:5092/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username": "alice", "password": "correct horse battery staple"}'

curl -i -X POST http://localhost:5092/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username": "alice", "password": "correct horse battery staple"}'

# Create a secret
curl -i -X POST http://localhost:5092/secrets \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {token}" \
  -d '{"content": "the launch codes are 1234", "ttlSeconds": 60}'

# Read it once (replace {id} with the id returned above)
curl -i http://localhost:5092/secrets/{id}

# Read it again - should now return 404
curl -i http://localhost:5092/secrets/{id}
```

## Documentation

### `POST /auth/register`
Creates a Redis-backed user account. Authentication is not required.

**Request body**
```json
{
  "username": "string, required, non-empty",
  "password": "string, required, non-empty"
}
```

**Responses**

| Status | Meaning |
|---|---|
| `201 Created` | Account created; returns `{ "message": "User registered successfully." }` |
| `400 Bad Request` | Username/password was empty, or the username is already taken; returns a Problem Details response |

---

### `POST /auth/login`
Validates credentials and creates a JWT. Authentication is not required.

**Request body**
```json
{
  "username": "string, required",
  "password": "string, required"
}
```

**Responses**

| Status | Meaning |
|---|---|
| `200 OK` | Credentials accepted; returns `{ "token": "JWT string" }` |
| `401 Unauthorized` | Credentials are invalid; returns a Problem Details response |

---

### `POST /secrets`
Creates a new secret with a time-to-live and associates it with the authenticated user. Requires `Authorization: Bearer {token}`.

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
| `401 Unauthorized` | A valid bearer token was not supplied |

---

### `GET /secrets/{id}`
Retrieves and permanently deletes a secret in one atomic operation. Can only ever succeed once per secret. Authentication is deliberately not required.

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

### `GET /users/me/secrets`
Returns the authenticated user's unexpired secret history. Requires `Authorization: Bearer {token}`. Stale ids whose metadata has expired are omitted.

**Response body**
```json
[
  {
    "id": "string",
    "createdAt": "ISO-8601 timestamp",
    "expiresAt": "ISO-8601 timestamp",
    "isRead": "boolean"
  }
]
```

**Responses**

| Status | Meaning |
|---|---|
| `200 OK` | Returns the caller's `SecretSummary[]`; the array may be empty |
| `401 Unauthorized` | A valid bearer token was not supplied |

---

### `GET /health/redis`
Health check confirming connectivity to the Redis backend. Authentication is not required.

**Responses**

| Status | Meaning |
|---|---|
| `200 OK` | Redis is reachable; returns `{ "status": "connected", "latencyMs": number }` |
| `503 Service Unavailable` | Redis is unreachable |