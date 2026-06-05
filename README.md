# PhotoSelect — Photographer Gallery Selection App

A full-stack photo proofing app where photographers upload galleries and clients star their favourite photos.

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core 8 Web API (C#) |
| Frontend | React 19 + TypeScript (Vite) |
| Database | SQL Server (Entity Framework Core 8) |
| Auth | JWT Bearer tokens |
| Images | SixLabors.ImageSharp (server-side thumbnails) |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- SQL Server (Express or Developer edition) — expects instance `.\sqlexpress` by default

---

## Quick Start

### 1. Clone & configure

Edit `PhotoUpload.Web/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "ConStr": "Data Source=.\\sqlexpress;Initial Catalog=PhotoUpload;Integrated Security=true;TrustServerCertificate=true;"
  },
  "Jwt": {
    "Secret": "REPLACE_WITH_A_32_CHAR_RANDOM_STRING_HERE"
  },
  "Admin": {
    "DefaultUsername": "admin",
    "DefaultPassword": "Admin@123!"
  }
}
```

> **Important:** Change `Jwt:Secret` to a long random string before deploying.

### 2. Install frontend dependencies

```bash
cd PhotoUpload.Web/ClientApp
npm install
```

### 3. Run the app (development)

From the repo root:

```bash
dotnet run --project PhotoUpload.Web
```

This automatically:
- Applies EF Core migrations and seeds the admin user
- Starts the .NET API on `http://localhost:5292`
- Launches the Vite dev server on `http://localhost:3000`

Open `http://localhost:3000` in your browser.

### 4. Admin login

- URL: `http://localhost:3000/admin/login`
- Username: `admin`  
- Password: `Admin@123!`

---

## Project Structure

```
PhotoUpload/
├── PhotoUpload.sln
├── PhotoUpload.Data/           # EF Core: models, DbContext, migrations
│   └── Models/
│       ├── Gallery.cs
│       ├── Photo.cs
│       ├── Selection.cs
│       └── AdminUser.cs
├── PhotoUpload.Web/            # ASP.NET Core API + SPA host
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── GalleriesController.cs    # Admin (JWT-protected)
│   │   └── PublicGalleryController.cs # Client (no auth)
│   ├── DTOs/
│   ├── Services/
│   │   ├── LocalFileStorageService.cs
│   │   ├── ThumbnailService.cs
│   │   ├── ZipService.cs
│   │   └── JwtService.cs
│   ├── ClientApp/              # React TypeScript (Vite)
│   │   └── src/
│   │       ├── api/            # Axios API client
│   │       ├── components/     # PhotoCard, Lightbox, UploadZone, ...
│   │       ├── contexts/       # AuthContext
│   │       ├── pages/
│   │       │   ├── admin/      # LoginPage, GalleryListPage, GalleryDetailPage
│   │       │   └── client/     # GalleryPage
│   │       └── types/          # Shared TypeScript interfaces
│   ├── uploads/                # Auto-created; stores originals + thumbnails
│   └── Program.cs
└── README.md
```

---

## API Reference

### Auth

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/login` | — | Returns JWT token |

### Admin (requires `Authorization: Bearer <token>`)

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/galleries` | List all galleries |
| POST | `/api/galleries` | Create gallery |
| GET | `/api/galleries/{id}` | Get gallery detail |
| DELETE | `/api/galleries/{id}` | Delete gallery + files |
| POST | `/api/galleries/{id}/upload` | Upload photos (multipart) |
| GET | `/api/galleries/{id}/photos` | List photos |
| DELETE | `/api/galleries/{id}/photos/{photoId}` | Delete photo |
| GET | `/api/galleries/{id}/selections` | View client selections |
| GET | `/api/galleries/{id}/download` | Download selections as ZIP |
| PUT | `/api/galleries/{id}/photos/order` | Reorder photos |
| POST | `/api/galleries/{id}/complete` | Mark gallery complete |

### Public (client-facing, no auth)

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/gallery/{token}` | Gallery info by token |
| POST | `/api/gallery/{token}/verify` | Verify password |
| GET | `/api/gallery/{token}/photos` | Get photos |
| POST | `/api/gallery/{token}/selections` | Submit selections |
| GET | `/api/gallery/{token}/selections` | Get existing selections |

---

## Features

### Admin
- Secure JWT login
- Create galleries with name, client info, max selections, optional password
- Bulk drag-and-drop photo upload with per-file progress bar
- Thumbnail generation on upload (JPEG, PNG, WebP, TIFF)
- RAW files (CR2, NEF, ARW) stored as-is (no thumbnail)
- View all galleries with status badges
- View client's selections with photo thumbnails
- Download selected photos as ZIP
- Delete individual photos or entire galleries
- Reorder photos (API available; drag-and-drop UI hookup point)
- Mark gallery as complete
- Auto-copy shareable link after gallery creation

### Client
- Access via unique token URL: `/gallery/{token}`
- Optional password protection
- Responsive photo grid with lazy-loaded thumbnails
- Loading skeleton placeholders
- Click to open full-screen lightbox (keyboard nav: ←/→/Esc/S)
- Star/unstar photos with smooth animations
- Live counter: "X of Y selected"
- Friendly warning when max is reached
- Confirmation dialog before submitting
- Summary page after submission

---

## Configuration

| Key | Default | Description |
|---|---|---|
| `ConnectionStrings:ConStr` | *(see above)* | SQL Server connection string |
| `Jwt:Secret` | *(change this!)* | Signing key (min 32 chars) |
| `Jwt:ExpiryHours` | `24` | Token lifetime |
| `Storage:RootPath` | `uploads` | Relative path for file storage |
| `Admin:DefaultUsername` | `admin` | Seeded admin username |
| `Admin:DefaultPassword` | `Admin@123!` | Seeded admin password |

---

## Swapping to Azure Blob Storage

Replace `LocalFileStorageService` with an implementation of `IFileStorageService` backed by `Azure.Storage.Blobs`. The interface contract (`SaveFileAsync`, `DeleteFileAsync`, `GetPublicUrl`, etc.) is already designed for this swap — register the new implementation in `Program.cs` and no other code changes are needed.

---

## Security Notes

- JWT tokens expire after 24 hours (configurable)
- Passwords hashed with BCrypt (work factor 10)
- File uploads: extension allowlist enforced server-side
- Gallery tokens are random 32-char hex strings (128-bit entropy)
- Change the default admin password after first run
- Set a strong `Jwt:Secret` in production (use environment variables / Azure Key Vault)
