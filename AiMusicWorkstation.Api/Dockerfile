FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["AiMusicWorkstation.Api/AiMusicWorkstation.Api.csproj", "AiMusicWorkstation.Api/"]
COPY ["AiMusicWorkstation.Application/AiMusicWorkstation.Application.csproj", "AiMusicWorkstation.Application/"]
COPY ["AiMusicWorkstation.Domain/AiMusicWorkstation.Domain.csproj", "AiMusicWorkstation.Domain/"]
COPY ["AiMusicWorkstation.Infrastructure/AiMusicWorkstation.Infrastructure.csproj", "AiMusicWorkstation.Infrastructure/"]
COPY ["AiMusicWorkstation.Shared/AiMusicWorkstation.Shared.csproj", "AiMusicWorkstation.Shared/"]

RUN dotnet restore "AiMusicWorkstation.Api/AiMusicWorkstation.Api.csproj"

COPY . .
WORKDIR "/src/AiMusicWorkstation.Api"
RUN dotnet publish "AiMusicWorkstation.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AiMusicWorkstation.Api.dll"]