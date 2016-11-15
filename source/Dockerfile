FROM microsoft/dotnet:latest
COPY ./Calamari/project.json /app/Calamari/project.json
COPY ./NuGet.Config /app/Calamari/NuGet.Config
COPY ./Calamari.Tests/project.json /app/Calamari.Tests/project.json
COPY ./NuGet.Config /app/Calamari.Tests/NuGet.Config
WORKDIR /app

RUN ["dotnet", "restore", "Calamari"]
RUN ["dotnet", "restore", "Calamari.Tests"]
COPY ./Calamari /app/Calamari
COPY ./Calamari.Tests /app/Calamari.Tests
RUN ["dotnet", "build", "Calamari", "-f", "netcoreapp1.0"]
RUN ["dotnet", "build", "Calamari.Tests", "-f", "netcoreapp1.0"]
 
#ENTRYPOINT ["dotnet", "test", "-f", "netcoreapp1.0", "Calamari.Tests"]