build:
    dotnet build

test:
    dotnet test

run:
    dotnet run --project src/Sholto.Host/Sholto.Host.csproj

watch:
    dotnet watch --project src/Sholto.Host/Sholto.Host.csproj run
