# ── Stage 1: build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution + project files first so layer cache is reused when only source changes
COPY FitTrack.sln ./
COPY FitTrack.Domain/FitTrack.Domain.csproj             FitTrack.Domain/
COPY FitTrack.Application/FitTrack.Application.csproj   FitTrack.Application/
COPY FitTrack.Infrastructure/FitTrack.Infrastructure.csproj FitTrack.Infrastructure/
COPY FitTrack.Web/FitTrack.Web.csproj                   FitTrack.Web/

RUN dotnet restore FitTrack.sln

# Copy the rest of the source and publish
COPY . .
RUN dotnet publish FitTrack.Web/FitTrack.Web.csproj \
      --configuration Release \
      --no-restore \
      --output /app/publish

# ── Stage 2: runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --from=build /app/publish .

# Blazor Server uses port 8080 by default in the ASP.NET runtime image
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FitTrack.Web.dll"]

