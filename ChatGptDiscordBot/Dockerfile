FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["ChatGptDiscordBot.csproj", ""]
RUN dotnet restore "ChatGptDiscordBot.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "ChatGptDiscordBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ChatGptDiscordBot.csproj" -c Release -o /app/publish

FROM base AS final  
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "ChatGptDiscordBot.dll"]
