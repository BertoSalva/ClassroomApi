# Classroom API (.NET 8, Onion Architecture)

A Google Classroom-style API built in **.NET 8** with:
- Onion architecture (Api / Application / Domain / Infrastructure)
- JWT Authentication
- Roles: **SuperAdmin**, **Teacher**, **Learner**
- Grades & Subjects
- Classrooms (per grade + subject)
- Enrollments
- Upload/download **PDF resources** (stored on local disk via an abstraction that can later be swapped to S3/R2)

## Tech
- ASP.NET Core Web API (.NET 8)
- EF Core 8 + PostgreSQL (Neon)
- ASP.NET Identity
- Swagger

---

## 1) Configure
Update `src/Classroom.Api/appsettings.json`:
- `ConnectionStrings:DefaultConnection` (or set `DATABASE_URL` env var)
- `Jwt:SigningKey` (use a long random secret)

Optional seed SuperAdmin (recommended for first run):
- `Seed:SuperAdminEmail`
- `Seed:SuperAdminPassword`
- `Seed:SuperAdminName`

Example env vars:
```bash
export DATABASE_URL="Host=...;Port=5432;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"
export Seed__SuperAdminEmail="admin@school.co.za"
export Seed__SuperAdminPassword="StrongPass1"
export Seed__SuperAdminName="Nolu Admin"
export Jwt__SigningKey="REPLACE_WITH_32+_CHAR_RANDOM_SECRET"
```

## 2) Create DB + migrations
Run:
```bash
dotnet restore
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate -p src/Classroom.Infrastructure -s src/Classroom.Api
dotnet ef database update -p src/Classroom.Infrastructure -s src/Classroom.Api
```

## 3) Run
```bash
dotnet run --project src/Classroom.Api
```
Swagger:
- `http://localhost:5000/swagger`

---

## Auth flow
1. Seed SuperAdmin via env vars (or create one manually in DB).
2. Login:
`POST /api/v1/auth/login`
3. Use bearer token in Swagger.
4. SuperAdmin creates users via:
`POST /api/v1/auth/register`

---

## PDF storage
Default storage:
- `App_Data/uploads` (configure via `Storage:RootPath`)

For Fly.io, mount a volume and set:
- `Storage__RootPath=/data/uploads`

---

## Next enhancements (easy to add)
- Teacher invites learners with join codes
- Per-term resources and folders
- Signed URLs (if moving to R2/S3)
- Audit logs + download logs
