FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_gcServer=0

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Shared/ClubReportHub.Shared/ClubReportHub.Shared.csproj", "src/Shared/ClubReportHub.Shared/"]
COPY ["src/Services/FinanceService/FinanceService.csproj", "src/Services/FinanceService/"]
RUN dotnet restore "src/Services/FinanceService/FinanceService.csproj"

COPY ["src/Shared/ClubReportHub.Shared/", "src/Shared/ClubReportHub.Shared/"]
COPY ["src/Services/FinanceService/", "src/Services/FinanceService/"]
WORKDIR "/src/src/Services/FinanceService"
RUN dotnet build "FinanceService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "FinanceService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FinanceService.dll"]
