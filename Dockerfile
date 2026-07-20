FROM node:22-bookworm-slim AS web-build
WORKDIR /src

RUN corepack enable

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY apps/web/package.json apps/web/package.json
COPY packages/template-contracts/package.json packages/template-contracts/package.json
COPY packages/template-tools/package.json packages/template-tools/package.json
COPY packages/ui/package.json packages/ui/package.json

RUN pnpm install --frozen-lockfile

COPY . .
RUN pnpm --filter @bankers-seat/web build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server-build
WORKDIR /src

COPY apps/server/server.csproj apps/server/
RUN dotnet restore apps/server/server.csproj

COPY . .
RUN dotnet publish apps/server/server.csproj -c Release -o /out/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=server-build /out/publish ./
COPY --from=web-build /src/apps/web/dist ./wwwroot

VOLUME ["/data", "/templates"]

EXPOSE 8080

ENTRYPOINT ["dotnet", "server.dll"]
