C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe Elmah.AzureTableStorage.csproj /p:Configuration=Release
C:\Tools\nuget\NuGet.exe pack -Prop Configuration=Release
Move-Item *.nupkg C:\temp\nuget -Force