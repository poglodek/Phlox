# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Phlox is a full-stack web application with a modern React frontend and .NET 10 backend API.

**Frontend**: React Router 7 with server-side rendering (SSR)
**Backend**: ASP.NET Core 10 Web API
**Styling**: TailwindCSS v4

## Purpose of the Project
The purpose of this work is to present the process of building and implementing an AI Assistant integrated with modern technologies such as .NET, SQL, Qdrant, and OpenAI as a language model (LLM). The project focuses on creating an efficient solution aimed at improving access to information and interactions with users.

## System Architecture
The AI Assistant application consists of several key components:

- **Backend**: Developed using .NET, which manages the application logic and interactions with the SQL database and Qdrant system.
- **Database**: Utilizing SQL for storing user data and information necessary for the effective functioning of the AI Assistant.
- **Qdrant**: A vector storage system enabling fast and efficient information retrieval based on vector-formatted data.
- **OpenAI**: A language model responsible for processing and generating responses to user queries.

## Frontend
User interaction with the AI Assistant occurs through an interface created in React. The frontend provides a user-friendly and intuitive way to search for information and use the features of the AI Assistant.
## Project Structure

```
Phlox/
├── app/              # React Router frontend application
│   ├── app/          # Application source code
│   │   ├── root.tsx  # Root layout with ErrorBoundary
│   │   ├── routes.ts # Route configuration (file-based routing)
│   │   └── routes/   # Route components
│   ├── build/        # Build output (generated)
│   └── public/       # Static assets
└── server/           # .NET backend
    └── Phlox.API/    # ASP.NET Core Web API project
```

## Development Commands

### Frontend (React Router)

Navigate to the `app/` directory for all frontend commands:

```bash
cd app

# Install dependencies
npm install

# Start development server (http://localhost:5173)
npm run dev

# Build for production
npm run build

# Start production server
npm run start

# Type checking
npm run typecheck
```

### Backend (.NET API)

Navigate to the `server/Phlox.API/` directory for backend commands:

```bash
cd server/Phlox.API

# Run in development mode (https://localhost:7086, http://localhost:5273)
dotnet run

# Build the project
dotnet build

# Restore dependencies
dotnet restore

# Run with watch mode (hot reload)
dotnet watch run
```

Alternatively, use the solution file from the `server/` directory:

```bash
cd server
dotnet build Phlox.slnx
dotnet run --project Phlox.API/Phlox.API.csproj
```

## Architecture Notes

### Frontend

- **React Router 7** with SSR enabled by default (configured in `react-router.config.ts`)
- **Type generation**: Run `npm run typecheck` to generate route types in `.react-router/types/`
- **Path aliases**: `~/` maps to `./app/` (configured in `tsconfig.json`)
- **Routing**: File-based routing defined in `app/routes.ts`
- **Error handling**: Global ErrorBoundary in `root.tsx` handles 404s and runtime errors
- **Vite plugins**: TailwindCSS, React Router, and tsconfig-paths

### Backend

- **Target Framework**: .NET 10
- **API Documentation**: Scalar UI available at `/scalar/` in development mode
- **OpenAPI**: Enabled in development with endpoint at `/openapi/v1.json`
- **Launch profiles**: Two profiles configured (http on :5273, https on :7086)

## Key Configuration Files

- `app/react-router.config.ts` - React Router SSR and build configuration
- `app/vite.config.ts` - Vite build tool configuration
- `app/tsconfig.json` - TypeScript compiler options and path mappings
- `server/Phlox.API/Program.cs` - ASP.NET Core application setup
- `server/Phlox.API/appsettings.json` - API configuration

## Running Both Frontend and Backend

For full-stack development, run both servers concurrently:

1. Terminal 1: `cd app && npm run dev` (frontend at http://localhost:5173)
2. Terminal 2: `cd server/Phlox.API && dotnet run` (backend API at https://localhost:7086)
