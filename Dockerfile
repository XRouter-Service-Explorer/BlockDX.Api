FROM mcr.microsoft.com/dotnet/core/aspnet:3.0 AS base
WORKDIR /app
EXPOSE 8086

FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build
WORKDIR /app

# Copy everything else and build
COPY . ./
RUN dotnet publish BlockDX.Api -c Release -o BlockDX.Api/out
# Build runtime image

FROM base AS final
COPY --from=build /app/BlockDX.Api/out .


ENTRYPOINT ["dotnet", "BlockDX.Api.dll"]