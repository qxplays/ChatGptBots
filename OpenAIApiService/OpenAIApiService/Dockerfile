FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["OpenAIApiService/OpenAIApiService.csproj", "OpenAIApiService/"]
RUN dotnet restore "OpenAIApiService/OpenAIApiService.csproj"
COPY . .
WORKDIR "/src/OpenAIApiService"
RUN dotnet build "OpenAIApiService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OpenAIApiService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OpenAIApiService.dll"]
