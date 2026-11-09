FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/CaseFlow.Api/CaseFlow.Api.csproj src/CaseFlow.Api/
RUN dotnet restore src/CaseFlow.Api/CaseFlow.Api.csproj
COPY src/CaseFlow.Api/ src/CaseFlow.Api/
RUN dotnet publish src/CaseFlow.Api/CaseFlow.Api.csproj -c Release -o /publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /publish .
RUN mkdir -p /app/data
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CaseFlow.Api.dll"]
