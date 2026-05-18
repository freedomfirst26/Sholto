build:
    dotnet build

test:
    dotnet test

run:
    dotnet run --project src/OpenDJ.App/OpenDJ.App.csproj

watch:
    dotnet watch --project src/OpenDJ.App/OpenDJ.App.csproj run
